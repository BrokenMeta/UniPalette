using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Editor
{
    public class UniPaletteWindow : EditorWindow
    {
        private PaletteView view;
        private PaletteState state;
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
            DisposeState();
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
            if (state == null || EditorApplication.timeSinceStartup < nextContextCheck) return;
            nextContextCheck = EditorApplication.timeSinceStartup + 0.1;
            // Unity 6.6 can change Selection through API without delivering selectionChanged.
            // Compare only the active object/count and current target, never search every tick.
            if (!ReferenceEquals(observedSelection, Selection.activeObject) || observedSelectionCount != Selection.count
                || (state.Context != null && !state.Context.IsValid)) RefreshContexts();
        }

        private void RefreshContexts()
        {
            if (!this || state == null || view?.IsAttached != true) return;
            observedSelection = Selection.activeObject;
            observedSelectionCount = Selection.count;
            if (state.ConfirmingContext) return;
            state.Refresh();
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
            DisposeState();
            titleContent = new GUIContent("UniPalette");
            minSize = new Vector2(360, 240);
            view = new PaletteView(rootVisualElement);
            state = new PaletteState();
            state.CommandResultsChanged += OnCommandResultsChanged;
            state.ExecuteCommand += ExecuteCommand;
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

        private void DisposeState()
        {
            if (state == null) return;
            state.CommandResultsChanged -= OnCommandResultsChanged;
            state.ExecuteCommand -= ExecuteCommand;
            state.Dispose();
        }

        private void SelectRow(int index) => state.Select(index);

        private void OnCommandResultsChanged() => Render(false);

        private void UpdateQuery(string query)
        {
            if (query == state.Query) return;
            state.SetQuery(query);
            view.RefreshResults(state);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            var enter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
            if (evt.keyCode == KeyCode.Escape)
            {
                Consume(evt);
                if (state.CanGoBack) GoBack(); else Close();
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
                state.MoveSelection(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                view.RevealSelection(state.SelectedIndex);
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

        private void ActivateSelected() => ApplyNavigation(state.ActivateSelected);
        private void BrowseSelected() => ApplyNavigation(state.BrowseSelected);
        private void OpenContextCommands() => ApplyNavigation(state.OpenContextCommands);

        private void ApplyNavigation(System.Func<bool> action)
        {
            if (executing || !this) return;
            if (action()) Render();
            else
            {
                if (executing || !this) return;
                view.UpdateMessage(state);
                ScheduleSearchFocus();
            }
            view.FocusSearch();
        }

        private void ExecuteCommand(PaletteItem command, PaletteItem context)
        {
            if (executing) return;
            if (!NativeExecution.TryPrepare(command, context, out var execution, out var error))
            {
                state.ShowExecutionError(error);
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
            var index = state.Results.IndexOf(item);
            if (index < 0) return;
            state.Select(index);
            view.SelectRow(index);
            if (pointer?.ctrlKey ?? mouse?.ctrlKey ?? false) OpenContextCommands();
            else ActivateSelected();
        }

        private void GoBack()
        {
            if (state.GoBack()) Render();
            else ScheduleSearchFocus();
        }

        private void Render(bool focusSearch = true)
        {
            // Acknowledge API changes even when Unity omits selectionChanged.
            observedSelection = Selection.activeObject;
            observedSelectionCount = Selection.count;
            view.Render(state, focusSearch);
        }

        private void ScheduleSearchFocus() => view?.ScheduleSearchFocus();
    }
}
