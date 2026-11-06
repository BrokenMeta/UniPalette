using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Borrows the view/session. The host owns their lifetime and Editor-specific operations.
    internal sealed class PalettePresenter : IDisposable
    {
        private readonly SubscriptionScope _subscriptions = new();

        private readonly Action _close;
        private readonly Action<PaletteItem, PaletteItem> _execute;

        private readonly IPaletteBrowserVisitor<BrowserPresentation> _presentations;
        private readonly PaletteSession _session;
        private readonly PaletteView _view;

        private bool _disposed;

        internal PalettePresenter(
            PaletteView view,
            PaletteSession session,
            IPaletteBrowserVisitor<BrowserPresentation> presentations,
            Action close,
            Action<PaletteItem, PaletteItem> execute)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _presentations = presentations ?? throw new ArgumentNullException(nameof(presentations));
            _close = close ?? throw new ArgumentNullException(nameof(close));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            try
            {
                view
                    .SubscribeBackRequested(GoBack)
                    .AddTo(_subscriptions);
                view
                    .SubscribeQueryChanged(UpdateQuery)
                    .AddTo(_subscriptions);
                view
                    .SubscribeRowSelectionChanged(SelectRow)
                    .AddTo(_subscriptions);
                view
                    .SubscribeActivationRequested(Activate)
                    .AddTo(_subscriptions);
                view
                    .SubscribeContextCommandsRequested(OpenCommands)
                    .AddTo(_subscriptions);
                view
                    .SubscribeBrowseRequested(BrowseSelected)
                    .AddTo(_subscriptions);
                view
                    .SubscribeDismissRequested(Dismiss)
                    .AddTo(_subscriptions);
                view
                    .SubscribeSelectionMoveRequested(MoveSelection)
                    .AddTo(_subscriptions);
                session
                    .SubscribeChanged(OnSessionChanged)
                    .AddTo(_subscriptions);
                session
                    .SubscribeExecutionRequested(ExecuteCommand)
                    .AddTo(_subscriptions);
            }
            catch
            {
                _subscriptions.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _subscriptions.Dispose();
        }

        private void SelectRow(int index)
        {
            if (!_disposed)
                _session.Select(index);
        }

        private void OnSessionChanged(PaletteChange change)
        {
            if (_disposed)
                return;

            switch (change)
            {
                case PaletteChange.Screen:
                    Render(false);
                    break;
                case PaletteChange.Results:
                    var presentation = _session.Browser.Accept(_presentations);
                    _view.RefreshResults(DescribeRows(presentation), _session.SelectedIndex, GetMessage(presentation));
                    break;
                case PaletteChange.Selection:
                    _view.SelectRow(_session.SelectedIndex);
                    break;
                case PaletteChange.Message:
                    _view.UpdateMessage(GetMessage(_session.Browser.Accept(_presentations)));
                    break;
            }
        }

        private void ExecuteCommand(PaletteItem command, PaletteItem context)
        {
            if (!_disposed)
                _execute(command, context);
        }

        private void UpdateQuery(string query)
        {
            if (_disposed || query == _session.Query.Text)
                return;

            _session.SetQuery(query);
        }

        private void Dismiss()
        {
            if (_disposed)
                return;

            if (_session.CanGoBack)
                GoBack();
            else
                _close();
        }

        private void MoveSelection(int direction)
        {
            if (_disposed)
                return;

            _session.MoveSelection(direction);
            _view.ScrollToSelection(_session.SelectedIndex);
            _view.FocusSearch();
        }

        private void ApplyNavigation(Func<bool> action)
        {
            if (_disposed)
                return;

            action();

            // Native execution may synchronously close the host and dispose this presenter.
            if (_disposed)
                return;

            ScheduleSearchFocus();
            _view.FocusSearch();
        }

        private bool TrySelectActivationTarget(PaletteItem item)
        {
            if (_disposed)
                return false;

            if (item == null)
                return true;

            var index = _session.Results.IndexOf(item);

            if (index < 0)
                return false;

            _session.Select(index);
            return true;
        }

        private void Activate(PaletteItem item)
        {
            if (TrySelectActivationTarget(item))
                ActivateSelected();
        }

        private void OpenCommands(PaletteItem item)
        {
            if (TrySelectActivationTarget(item))
                OpenContextCommands();
        }

        private PaletteRow[] DescribeRows(BrowserPresentation presentation)
        {
            var results = _session.Results;
            var context = _session.Context;
            var rows = new PaletteRow[results.Count];

            for (var index = 0; index < rows.Length; index++)
                rows[index] = presentation.Describe(_session.GetResult(index), context);

            return rows;
        }

        private string GetMessage(BrowserPresentation presentation) =>
            _session.Message.Length > 0 ? _session.Message : presentation.GetNotice(_session);

        internal void ActivateSelected()
        {
            ApplyNavigation(_session.ActivateSelected);
        }

        internal void BrowseSelected()
        {
            ApplyNavigation(_session.BrowseSelected);
        }

        internal void OpenContextCommands()
        {
            ApplyNavigation(_session.OpenContextCommands);
        }

        internal void GoBack()
        {
            if (_disposed)
                return;

            _session.GoBack();
            ScheduleSearchFocus();
        }

        internal void Render(bool focusSearch = true)
        {
            if (_disposed)
                return;

            var browser = _session.Browser;
            var context = browser.Context;
            var presentation = browser.Accept(_presentations);
            var state = new PaletteScreenState(
                _session.Query.Text,
                presentation.SearchPlaceholder,
                presentation.Label,
                context?.ContextTitle ?? "Choose a context",
                presentation.GetBreadcrumb(browser),
                context?.Target,
                context?.IconName ?? "Search Icon",
                presentation.DataLabel,
                presentation.DataTooltip,
                presentation.ResultsTitle,
                presentation.Hints,
                _session.CanGoBack,
                DescribeRows(presentation),
                _session.SelectedIndex,
                GetMessage(presentation)
            );
            _view.Render(state, focusSearch);
        }

        internal void ScheduleSearchFocus()
        {
            if (!_disposed)
                _view.ScheduleSearchFocus();
        }
    }
}