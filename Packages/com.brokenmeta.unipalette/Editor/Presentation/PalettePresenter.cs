using System;
using UnityEngine;
using UnityEngine.UIElements;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Borrows the view/session. The host owns their lifetime and Editor-specific operations.
    internal sealed class PalettePresenter : IDisposable
    {
        private readonly PaletteView view;
        private readonly PaletteSession session;
        private readonly IPaletteBrowserVisitor<BrowserPresentation> presentations;
        private readonly Action close;
        private readonly Action<PaletteItem, PaletteItem> execute;
        private readonly Action beforeRender;
        private bool disposed;

        internal PalettePresenter(PaletteView view, PaletteSession session,
            IPaletteBrowserVisitor<BrowserPresentation> presentations, Action close,
            Action<PaletteItem, PaletteItem> execute, Action beforeRender)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.presentations = presentations ?? throw new ArgumentNullException(nameof(presentations));
            this.close = close ?? throw new ArgumentNullException(nameof(close));
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.beforeRender = beforeRender ?? throw new ArgumentNullException(nameof(beforeRender));
            view.BackRequested += GoBack;
            view.QueryChanged += UpdateQuery;
            view.RowSelectionChanged += SelectRow;
            view.RowActivated += ActivateRow;
            view.KeyPressed += OnKeyDown;
            session.CommandResultsChanged += OnCommandResultsChanged;
            session.ExecuteCommand += ExecuteCommand;
        }

        private void SelectRow(int index)
        { if (!disposed) session.Select(index); }

        private void OnCommandResultsChanged() => Render(false);

        private void ExecuteCommand(PaletteItem command, PaletteItem context)
        { if (!disposed) execute(command, context); }

        private void UpdateQuery(string query)
        {
            if (disposed || query == session.Query) return;
            session.SetQuery(query);
            view.RefreshResults(session);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (disposed) return;
            var enter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
            if (evt.keyCode == KeyCode.Escape)
            {
                Consume(evt);
                if (session.CanGoBack) GoBack(); else close();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.LeftArrow)
            {
                Consume(evt);
                GoBack();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.RightArrow)
            {
                Consume(evt);
                if (!evt.ctrlKey && !evt.commandKey && !evt.shiftKey) BrowseSelected();
            }
            else if (!evt.ctrlKey && !evt.commandKey && !evt.shiftKey &&
                     (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow))
            {
                Consume(evt);
                session.MoveSelection(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                view.RevealSelection(session.SelectedIndex);
                view.FocusSearch();
            }
            else if (enter)
            {
                Consume(evt);
                if (evt.altKey || evt.shiftKey || evt.commandKey) return;
                if (evt.ctrlKey) OpenContextCommands(); else ActivateSelected();
            }
            // Unmodified Left/Right are intentionally left to the text input.
        }

        private static void Consume(KeyDownEvent evt) => evt.StopImmediatePropagation();

        internal void ActivateSelected() => ApplyNavigation(session.ActivateSelected);
        internal void BrowseSelected() => ApplyNavigation(session.BrowseSelected);
        internal void OpenContextCommands() => ApplyNavigation(session.OpenContextCommands);

        private void ApplyNavigation(Func<bool> action)
        {
            if (disposed) return;
            var changed = action();
            // Native execution may synchronously close the host and dispose this presenter.
            if (disposed) return;
            if (changed) Render();
            else
            {
                view.UpdateMessage(session);
                ScheduleSearchFocus();
            }
            if (!disposed) view.FocusSearch();
        }

        private void ActivateRow(PaletteItem item, EventBase evt)
        {
            if (disposed) return;
            var pointer = evt as IPointerEvent;
            var mouse = evt as IMouseEvent;
            // A second click must not activate the row rebound beneath the first click.
            if ((pointer?.clickCount ?? mouse?.clickCount ?? 1) > 1) return;
            var index = session.Results.IndexOf(item);
            if (index < 0) return;
            session.Select(index);
            view.SelectRow(index);
            if (pointer?.ctrlKey ?? mouse?.ctrlKey ?? false) OpenContextCommands();
            else ActivateSelected();
        }

        internal void GoBack()
        {
            if (disposed) return;
            if (session.GoBack()) Render();
            else ScheduleSearchFocus();
        }

        internal void Render(bool focusSearch = true)
        {
            if (disposed) return;
            beforeRender();
            if (!disposed) view.Render(session, session.Browser.Accept(presentations), focusSearch);
        }

        internal void ScheduleSearchFocus()
        { if (!disposed) view.ScheduleSearchFocus(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.BackRequested -= GoBack;
            view.QueryChanged -= UpdateQuery;
            view.RowSelectionChanged -= SelectRow;
            view.RowActivated -= ActivateRow;
            view.KeyPressed -= OnKeyDown;
            session.CommandResultsChanged -= OnCommandResultsChanged;
            session.ExecuteCommand -= ExecuteCommand;
        }
    }
}
