using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;

[assembly: InternalsVisibleTo("BrokenMeta.UniPalette.Tests")]

namespace BrokenMeta.UniPalette.Editor.Application
{
    // Owns navigation history and transient state; each browser owns its level's behavior.
    internal sealed class PaletteSession : IDisposable
    {
        private enum ResultsUpdate
        {
            Navigation,
            Refresh,
            Search,
        }

        private readonly Signal<PaletteChange> _changed = new();

        private readonly Signal _contextConfirmed = new();

        private readonly Signal<(PaletteItem Command, PaletteItem Context)> _executionRequested = new();

        private readonly IDisposable _commandSubscription;

        private readonly ICommandSource _commandSource;

        private readonly IContextSelection _contextSelection;
        private readonly PaletteHistory _history;

        private readonly List<PaletteItem> _results = new();

        private readonly List<PaletteResult> _resultDetails = new();

        private readonly List<PaletteResult> _pendingResults = new();

        private bool _disposed;

        private bool _stagingResults;

        private bool _selectFirstWhenLoaded;

        private string _message = "";

        private string _resultsError;

        private string _refreshError;

        private PaletteItem SelectedItem => SelectedIndex >= 0 ? _results[SelectedIndex] : null;

        internal ReadOnlyCollection<PaletteItem> Results { get; }

        internal bool ConfirmingContext { get; private set; }

        internal bool CommandsLoading => _commandSource.IsLoading;

        internal string CommandSourceError => _commandSource.Error;

        internal PaletteBrowser Browser { get; private set; }

        internal PaletteItem Context => Browser.Context;

        internal bool CanGoBack => _history.CanGoBack;

        internal string Location => Browser.Location;

        internal SearchQuery Query { get; private set; } = SearchQuery.Empty;

        internal int SelectedIndex { get; private set; } = -1;

        internal string Message => _resultsError
            ?? (_message.Length > 0 ? _message : _refreshError ?? "");

        internal PaletteSession(
            ICommandSource commandSource,
            IContextSelection contextSelection,
            IContextSource contexts)
        {
            _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
            _contextSelection = contextSelection ?? throw new ArgumentNullException(nameof(contextSelection));
            Results = _results.AsReadOnly();
            Browser = new ContextBrowser(contexts);
            _history = new PaletteHistory(Browser);
            if (TryStageResults(() => Browser, Query, out var initial))
                ApplyResults(initial, Query, -1);

            _commandSubscription = commandSource.SubscribeChanged(OnCommandsChanged);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _commandSubscription.Dispose();
            _changed.Dispose();
            _contextConfirmed.Dispose();
            _executionRequested.Dispose();
        }

        internal IDisposable SubscribeChanged(Action<PaletteChange> callback) =>
            _changed.Subscribe(callback);

        internal IDisposable SubscribeContextConfirmed(Action callback) =>
            _contextConfirmed.Subscribe(callback);

        internal IDisposable SubscribeExecutionRequested(Action<PaletteItem, PaletteItem> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return _executionRequested.Subscribe(request => callback(request.Command, request.Context));
        }

        private PaletteItem LoadCommands(PaletteItem context) =>
            _commandSource.ForContext(context);

        private void OnCommandsChanged()
        {
            if (_disposed || ConfirmingContext || _stagingResults)
                return;

            var staged = TryStageResults(
                () => Browser.OnCommandsChanged(LoadCommands),
                Query,
                out var next,
                ResultsUpdate.Refresh
            );

            if (!staged || next == null)
                return;

            var selectedIndex = _selectFirstWhenLoaded && Query.IsEmpty ? 0 : -1;
            ApplyResults(next, Query, selectedIndex);
            _selectFirstWhenLoaded = false;
            _changed.Publish(PaletteChange.Screen);
        }

        private bool ReturnToPreviousBrowser()
        {
            var previous = _history.PeekBack(out var entriesToPop);

            if (!TryStageResults(
                () => previous._browser.RefreshCommands(LoadCommands),
                SearchQuery.Empty,
                out var next
            ))
            {
                return false;
            }

            var selectedIndex = previous.FindSelection(_pendingResults);
            _history.CommitBack(entriesToPop);
            ApplyResults(next, SearchQuery.Empty, selectedIndex);
            _message = "";
            _changed.Publish(PaletteChange.Screen);
            return true;
        }

        private bool ValidateNavigationTarget(PaletteItem selected)
        {
            if ((Context == null || Context.IsValid) && (selected == null || selected.IsValid))
                return true;

            ShowExecutionError("Context no longer exists.");

            return false;
        }

        private bool TryStageResults(
            Func<PaletteBrowser> prepareBrowser,
            SearchQuery query,
            out PaletteBrowser next,
            ResultsUpdate update = ResultsUpdate.Navigation)
        {
            next = null;

            if (_stagingResults)
                return false;

            _stagingResults = true;
            _pendingResults.Clear();

            try
            {
                next = prepareBrowser();

                if (next != null)
                    _pendingResults.AddRange(next.GetItems(query));

                return true;
            }
            catch (Exception exception)
            {
                next = null;
                _pendingResults.Clear();
                var previousMessage = Message;
                _resultsError = "Could not update palette results: " + exception.Message;

                if (update == ResultsUpdate.Refresh)
                    _refreshError = _resultsError;

                var restoreQuery = update == ResultsUpdate.Search;

                if (restoreQuery || Message != previousMessage)
                    _changed.Publish(restoreQuery ? PaletteChange.Screen : PaletteChange.Message);

                return false;
            }
            finally
            {
                _stagingResults = false;
            }
        }

        private void ApplyResults(
            PaletteBrowser browser,
            SearchQuery query,
            int selectedIndex,
            bool preserveRefreshError = false)
        {
            _results.Clear();
            _resultDetails.Clear();

            foreach (var result in _pendingResults)
            {
                _results.Add(result.Item);
                _resultDetails.Add(result);
            }

            _pendingResults.Clear();
            Browser = browser;
            Query = query;
            SetSelectedIndex(selectedIndex);
            _resultsError = null;

            if (!preserveRefreshError)
                _refreshError = null;
        }

        private bool PushPrepared(PaletteBrowser next, bool selectFirstWhenLoaded)
        {
            _history.Push(Browser, SelectedItem);
            ApplyResults(next, SearchQuery.Empty, 0);
            _message = "";
            _selectFirstWhenLoaded = selectFirstWhenLoaded;
            _changed.Publish(PaletteChange.Screen);
            return true;
        }

        private void SetSelectedIndex(int index) =>
            SelectedIndex = index >= 0 && index < _results.Count ? index : -1;

        internal void ShowExecutionError(string error)
        {
            var previousMessage = Message;
            _resultsError = null;
            _message = error;

            if (Message != previousMessage)
                _changed.Publish(PaletteChange.Message);
        }

        internal bool OpenCommands(PaletteItem context)
        {
            PaletteBrowser next;
            ConfirmingContext = true;

            try
            {
                if (context is ContextItem confirmed)
                {
                    if (!_contextSelection.TryConfirm(confirmed, out var error))
                    {
                        ShowExecutionError(error);
                        return false;
                    }

                    _contextConfirmed.Publish();
                }

                // Availability is read after Selection changes, never from cached search labels.
                if (!TryStageResults(
                    () => new CommandBrowser(context, LoadCommands(context)),
                    SearchQuery.Empty,
                    out next
                ))
                {
                    return false;
                }
            }
            finally
            {
                ConfirmingContext = false;
            }

            return PushPrepared(next, CommandsLoading);
        }

        // Editor notifications rebuild the current area without restoring an old query/selection.
        internal void Refresh()
        {
            if (Context != null && !Context.IsValid)
            {
                ReturnToPreviousBrowser();
                return;
            }

            if (!TryStageResults(
                () => Browser.RefreshCommands(LoadCommands),
                Query,
                out var next,
                ResultsUpdate.Refresh
            ))
            {
                return;
            }

            ApplyResults(next, Query, -1);
            _changed.Publish(PaletteChange.Screen);
        }

        internal void SetQuery(string text)
        {
            var query = new SearchQuery(text);

            if (query.Equals(Query))
                return;

            if (!TryStageResults(() => Browser, query, out var next, ResultsUpdate.Search))
                return;

            ApplyResults(next, query, -1, preserveRefreshError: true);
            _selectFirstWhenLoaded = false;
            _message = "";
            _changed.Publish(PaletteChange.Results);
        }

        internal PaletteResult GetResult(int index) =>
            _resultDetails[index];

        internal int FindResultIndex(Predicate<PaletteItem> match) =>
            _results.FindIndex(match);

        internal void Select(int index)
        {
            var previous = SelectedIndex;
            SetSelectedIndex(index);

            if (previous != SelectedIndex)
                _changed.Publish(PaletteChange.Selection);
        }

        internal void MoveSelection(int direction)
        {
            if (_results.Count == 0)
                return;

            Select(SelectedIndex < 0
                ? direction > 0 ? 0 : _results.Count - 1
                : Math.Max(0, Math.Min(_results.Count - 1, SelectedIndex + direction)));
        }

        internal bool ActivateSelected()
        {
            var selected = SelectedItem;

            return ValidateNavigationTarget(selected) && Browser.Activate(this, selected);
        }

        internal bool BrowseSelected() =>
            Browser.Browse(this, SelectedItem);

        internal bool OpenContextCommands()
        {
            var selected = SelectedItem;

            return ValidateNavigationTarget(selected) && Browser.OpenContextCommands(this, selected);
        }

        internal void DispatchCommand(PaletteItem command, PaletteItem context)
        {
            if (_executionRequested.SubscriberCount > 0)
                _executionRequested.Publish((command, context));
            else
                ShowExecutionError("Native execution is unavailable; nothing executed.");
        }

        internal bool GoBack()
        {
            if (!CanGoBack)
                return false;

            return ReturnToPreviousBrowser();
        }

        internal bool Push(PaletteBrowser next)
        {
            return TryStageResults(() => next, SearchQuery.Empty, out var prepared)
                && PushPrepared(prepared, _selectFirstWhenLoaded);
        }
    }
}