using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Borrows the view/session. The host owns their lifetime and Editor-specific operations.
    internal sealed class PalettePresenter : IDisposable
    {
        private readonly SubscriptionScope _subscriptions = new();

        private readonly Action _beforeRender;
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
            Action<PaletteItem, PaletteItem> execute,
            Action beforeRender)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _presentations = presentations ?? throw new ArgumentNullException(nameof(presentations));
            _close = close ?? throw new ArgumentNullException(nameof(close));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _beforeRender = beforeRender ?? throw new ArgumentNullException(nameof(beforeRender));
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
                    .SubscribeRowActivated(ActivateRow)
                    .AddTo(_subscriptions);
                view
                    .SubscribeKeyPressed(OnKeyDown)
                    .AddTo(_subscriptions);
                session
                    .SubscribeCommandResultsChanged(OnCommandResultsChanged)
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

        private void OnCommandResultsChanged()
        {
            Render(false);
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
            _view.RefreshResults(_session);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_disposed)
                return;

            var enter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;

            if (evt.keyCode == KeyCode.Escape)
            {
                Consume(evt);

                if (_session.CanGoBack)
                    GoBack();
                else
                    _close();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.LeftArrow)
            {
                Consume(evt);
                GoBack();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.RightArrow)
            {
                Consume(evt);

                if (!evt.ctrlKey && !evt.commandKey && !evt.shiftKey)
                    BrowseSelected();
            }
            else if (!evt.ctrlKey
                && !evt.commandKey
                && !evt.shiftKey
                && (evt.keyCode == KeyCode.UpArrow
                    || evt.keyCode == KeyCode.DownArrow))
            {
                Consume(evt);
                _session.MoveSelection(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                _view.RevealSelection(_session.SelectedIndex);
                _view.FocusSearch();
            }
            else if (enter)
            {
                Consume(evt);

                if (evt.altKey || evt.shiftKey || evt.commandKey)
                    return;

                if (evt.ctrlKey)
                    OpenContextCommands();
                else
                    ActivateSelected();
            }
            // Unmodified Left/Right are intentionally left to the text input.
        }

        private static void Consume(KeyDownEvent evt)
        {
            evt.StopImmediatePropagation();
        }

        private void ApplyNavigation(Func<bool> action)
        {
            if (_disposed)
                return;

            var changed = action();

            // Native execution may synchronously close the host and dispose this presenter.
            if (_disposed)
                return;

            if (changed)
            {
                Render();
            }
            else
            {
                _view.UpdateMessage(_session);
                ScheduleSearchFocus();
            }

            if (!_disposed)
                _view.FocusSearch();
        }

        private void ActivateRow(PaletteItem item, EventBase evt)
        {
            if (_disposed)
                return;

            var pointer = evt as IPointerEvent;
            var mouse = evt as IMouseEvent;

            // A second click must not activate the row rebound beneath the first click.
            if ((pointer?.clickCount ?? mouse?.clickCount ?? 1) > 1)
                return;

            var index = _session.Results.IndexOf(item);

            if (index < 0)
                return;

            _session.Select(index);
            _view.SelectRow(index);

            if (pointer?.ctrlKey ?? mouse?.ctrlKey ?? false)
                OpenContextCommands();
            else
                ActivateSelected();
        }

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

            if (_session.GoBack())
                Render();
            else
                ScheduleSearchFocus();
        }

        internal void Render(bool focusSearch = true)
        {
            if (_disposed)
                return;

            _beforeRender();

            if (!_disposed)
                _view.Render(_session, _session.Browser.Accept(_presentations), focusSearch);
        }

        internal void ScheduleSearchFocus()
        {
            if (!_disposed)
                _view.ScheduleSearchFocus();
        }
    }
}