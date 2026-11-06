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
        private readonly Signal<PaletteChange> _changed = new();

        private readonly Signal<(PaletteItem Command, PaletteItem Context)> _executionRequested = new();

        private readonly IDisposable _commandSubscription;

        private readonly ICommandSource _commandSource;

        private readonly IContextSelection _contextSelection;
        private readonly PaletteHistory _history;

        private readonly List<PaletteItem> _results = new();

        private bool _disposed;

        private bool _loadingCommands;

        private bool _selectFirstWhenLoaded;

        private PaletteItem SelectedItem => SelectedIndex >= 0 ? _results[SelectedIndex] : null;

        internal ReadOnlyCollection<PaletteItem> Results { get; }

        internal bool ConfirmingContext { get; private set; }

        internal bool CommandsLoading => _commandSource.IsLoading;

        internal string CommandNotice => _commandSource.Error.Length > 0
            ? _commandSource.Error
            : CommandsLoading
                ? "Loading Unity menu commands\u2026"
                : "Native execution \u00B7 Confirmed context required \u00B7 Unity determines applicability and result";

        internal PaletteBrowser Browser { get; private set; }

        internal PaletteItem Context => Browser.Context;

        internal bool CanGoBack => _history.CanGoBack;

        internal string Location => Browser.Location;

        internal SearchQuery Query { get; private set; } = SearchQuery.Empty;

        internal int SelectedIndex { get; private set; } = -1;

        internal string Message { get; private set; } = "";

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
            Rebuild();
            _commandSubscription = commandSource.SubscribeChanged(OnCommandsChanged);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _commandSubscription.Dispose();
            _changed.Dispose();
            _executionRequested.Dispose();
        }

        internal IDisposable SubscribeChanged(Action<PaletteChange> callback) =>
            _changed.Subscribe(callback);

        internal IDisposable SubscribeExecutionRequested(Action<PaletteItem, PaletteItem> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return _executionRequested.Subscribe(request => callback(request.Command, request.Context));
        }

        private PaletteItem LoadCommands(PaletteItem context)
        {
            _loadingCommands = true;

            try
            {
                return _commandSource.ForContext(context);
            }
            finally
            {
                _loadingCommands = false;
            }
        }

        private void OnCommandsChanged()
        {
            if (_disposed || ConfirmingContext || _loadingCommands)
                return;

            var next = Browser.OnCommandsChanged(LoadCommands);

            if (next == null)
                return;

            Browser = next;
            Rebuild();
            SetSelectedIndex(_selectFirstWhenLoaded && Query.IsEmpty ? 0 : -1);
            _selectFirstWhenLoaded = false;
            _changed.Publish(PaletteChange.Screen);
        }

        private void ReturnToPreviousBrowser()
        {
            var previous = _history.GoBack();
            Browser = previous._browser.RefreshCommands(LoadCommands);
            ResetTransient();
            SetSelectedIndex(previous.FindSelection(Results));
        }

        private bool ValidateNavigationTarget(PaletteItem selected)
        {
            if ((Context == null || Context.IsValid) && (selected == null || selected.IsValid))
                return true;

            ShowExecutionError("Context no longer exists.");

            return false;
        }

        private void ResetTransient()
        {
            Query = SearchQuery.Empty;
            SelectedIndex = -1;
            Message = "";
            Rebuild();
        }

        private void Rebuild()
        {
            _results.Clear();
            _results.AddRange(Browser.GetItems(Query));
        }

        private void SetSelectedIndex(int index) =>
            SelectedIndex = index >= 0 && index < _results.Count ? index : -1;

        internal void ShowExecutionError(string error)
        {
            if (Message == error)
                return;

            Message = error;
            _changed.Publish(PaletteChange.Message);
        }

        internal bool OpenCommands(PaletteItem context)
        {
            PaletteItem commands;
            ConfirmingContext = true;

            try
            {
                if (context is ContextItem confirmed && !_contextSelection.TryConfirm(confirmed, out var error))
                {
                    ShowExecutionError(error);

                    return false;
                }

                // Availability is read after Selection changes, never from cached search labels.
                commands = LoadCommands(context);
            }
            finally
            {
                ConfirmingContext = false;
            }

            _selectFirstWhenLoaded = CommandsLoading;

            return Push(new CommandBrowser(context, commands));
        }

        // Editor notifications rebuild the current area without restoring an old query/selection.
        internal void Refresh()
        {
            if (Context != null && !Context.IsValid)
            {
                ReturnToPreviousBrowser();
                _changed.Publish(PaletteChange.Screen);
                return;
            }

            Browser = Browser.RefreshCommands(LoadCommands);
            SelectedIndex = -1;
            Rebuild();
            _changed.Publish(PaletteChange.Screen);
        }

        internal void SetQuery(string text)
        {
            var query = new SearchQuery(text);

            if (query.Equals(Query))
                return;

            Query = query;
            _selectFirstWhenLoaded = false;
            SelectedIndex = -1;
            Message = "";
            Rebuild();
            _changed.Publish(PaletteChange.Results);
        }

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

            ReturnToPreviousBrowser();
            _changed.Publish(PaletteChange.Screen);
            return true;
        }

        internal bool Push(PaletteBrowser next)
        {
            _history.Push(Browser, SelectedItem);
            Browser = next;
            ResetTransient();
            SetSelectedIndex(0);
            _changed.Publish(PaletteChange.Screen);
            return true;
        }
    }
}