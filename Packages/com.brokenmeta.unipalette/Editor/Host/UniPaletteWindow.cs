using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Presentation;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor
{
    public class UniPaletteWindow : EditorWindow
    {
        private PaletteView view;
        private PaletteSession session;
        private ICommandSource commandSource;
        private UnityEngine.Object observedSelection;
        private int observedSelectionCount;
        private double nextContextCheck;
        private bool executing;

        private void OnEnable()
        {
            executing = false;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += RefreshContexts;
            EditorApplication.projectChanged += RefreshContexts;
            EditorApplication.update += CheckContextChanges;
        }

        private void OnDisable()
        {
            executing = true;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= RefreshContexts;
            EditorApplication.projectChanged -= RefreshContexts;
            EditorApplication.update -= CheckContextChanges;
            DisposeView();
            DisposeSession();
        }

        private void OnFocus() => RefreshContexts();

        private void OnSelectionChanged()
        {
            // A delayed notification for the context we just confirmed is already reflected in the UI.
            if (ReferenceEquals(observedSelection, Selection.activeObject) && observedSelectionCount == Selection.count) return;
            RefreshContexts();
        }

        private void CheckContextChanges()
        {
            if (session == null || EditorApplication.timeSinceStartup < nextContextCheck) return;
            nextContextCheck = EditorApplication.timeSinceStartup + 0.1;
            // Unity 6.6 can change Selection through API without delivering selectionChanged.
            // Compare only the active object/count and current target, never search every tick.
            if (!ReferenceEquals(observedSelection, Selection.activeObject) || observedSelectionCount != Selection.count
                || (session.Context != null && !session.Context.IsValid)) RefreshContexts();
        }

        private void RefreshContexts()
        {
            if (!this || session == null || view?.IsAttached != true) return;
            observedSelection = Selection.activeObject;
            observedSelectionCount = Selection.count;
            if (session.ConfirmingContext) return;
            session.Refresh();
            Render(false);
        }

        [UnityEditor.MenuItem("Window/UniPalette")]
        [Shortcut("UniPalette/Open", KeyCode.U, ShortcutModifiers.Control | ShortcutModifiers.Alt)]
        private static void Open()
        {
            var isNewWindow = !HasOpenInstances<UniPaletteWindow>();
            var window = GetWindow<UniPaletteWindow>();
            if (isNewWindow)
            {
                var bounds = window.position;
                bounds.size = new Vector2(760, 460);
                bounds.center = EditorGUIUtility.GetMainWindowPosition().center;
                window.position = bounds;
            }
            window.titleContent = new GUIContent("UniPalette");
            window.Show();
            window.Focus();
            window.ScheduleSearchFocus();
        }

        private void CreateGUI()
        {
            DisposeView();
            DisposeSession();
            titleContent = new GUIContent("UniPalette");
            minSize = new Vector2(360, 240);
            view = new PaletteView(rootVisualElement);
            commandSource = new MenuCommands();
            session = new PaletteSession(commandSource);
            session.CommandResultsChanged += OnCommandResultsChanged;
            session.ExecuteCommand += ExecuteCommand;
            AttachView();
            Render();
        }

        private void AttachView()
        {
            view.BackRequested += GoBack;
            view.QueryChanged += UpdateQuery;
            view.RowSelectionChanged += SelectRow;
            view.RowActivated += ActivateRow;
            view.KeyPressed += OnKeyDown;
        }

        private void DisposeView()
        {
            if (view == null) return;
            view.BackRequested -= GoBack;
            view.QueryChanged -= UpdateQuery;
            view.RowSelectionChanged -= SelectRow;
            view.RowActivated -= ActivateRow;
            view.KeyPressed -= OnKeyDown;
            view.Dispose();
            view = null;
        }

        private void DisposeSession()
        {
            if (session != null)
            {
                session.CommandResultsChanged -= OnCommandResultsChanged;
                session.ExecuteCommand -= ExecuteCommand;
                session.Dispose();
            }
            commandSource?.Dispose();
            commandSource = null;
        }

        private void SelectRow(int index) => session.Select(index);

        private void OnCommandResultsChanged() => Render(false);

        private void UpdateQuery(string query)
        {
            if (query == session.Query) return;
            session.SetQuery(query);
            view.RefreshResults(session);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            var enter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
            if (evt.keyCode == KeyCode.Escape)
            {
                Consume(evt);
                if (session.CanGoBack) GoBack(); else Close();
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

        private static void Consume(KeyDownEvent evt)
        {
            evt.StopImmediatePropagation();
        }

        private void ActivateSelected() => ApplyNavigation(session.ActivateSelected);
        private void BrowseSelected() => ApplyNavigation(session.BrowseSelected);
        private void OpenContextCommands() => ApplyNavigation(session.OpenContextCommands);

        private void ApplyNavigation(System.Func<bool> action)
        {
            if (executing || !this) return;
            if (action()) Render();
            else
            {
                if (executing || !this) return;
                view.UpdateMessage(session);
                ScheduleSearchFocus();
            }
            view.FocusSearch();
        }

        private void ExecuteCommand(PaletteItem command, PaletteItem context)
        {
            if (executing) return;
            if (!NativeExecution.TryPrepare(command, context, out var execution, out var error))
            {
                session.ShowExecutionError(error);
                return;
            }
            executing = true;
            Close();
            execution.ExecuteAfterClose();
        }

        private void ActivateRow(PaletteItem item, EventBase evt)
        {
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

        private void GoBack()
        {
            if (session.GoBack()) Render();
            else ScheduleSearchFocus();
        }

        private void Render(bool focusSearch = true)
        {
            // Acknowledge API changes even when Unity omits selectionChanged.
            observedSelection = Selection.activeObject;
            observedSelectionCount = Selection.count;
            view.Render(session, focusSearch);
        }

        private void ScheduleSearchFocus() => view?.ScheduleSearchFocus();
    }
}
