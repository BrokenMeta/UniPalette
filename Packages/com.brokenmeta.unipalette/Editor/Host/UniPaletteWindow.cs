using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Presentation;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor
{
    public class UniPaletteWindow : EditorWindow
    {
        private PalettePresenter presenter;
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
            ObserveSelection();
            if (session.ConfirmingContext) return;
            session.Refresh();
            presenter?.Render(false);
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
            window.presenter?.ScheduleSearchFocus();
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
            AttachPresenter();
            presenter.Render();
        }

        private void AttachPresenter() => presenter = new PalettePresenter(
            view, session, new BrowserPresentationSelector(), Close, ExecuteCommand, ObserveSelection);

        private void DisposePresenter()
        {
            presenter?.Dispose();
            presenter = null;
        }

        private void DisposeView()
        {
            DisposePresenter();
            view?.Dispose();
            view = null;
        }

        private void DisposeSession()
        {
            DisposePresenter();
            session?.Dispose();
            commandSource?.Dispose();
            commandSource = null;
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

        private void ObserveSelection()
        {
            // Acknowledge API changes even when Unity omits selectionChanged.
            observedSelection = Selection.activeObject;
            observedSelectionCount = Selection.count;
        }
    }
}
