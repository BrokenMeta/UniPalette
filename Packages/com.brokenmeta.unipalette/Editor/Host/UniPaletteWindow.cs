using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Presentation;
using BrokenMeta.UniPalette.Editor.Services;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace BrokenMeta.UniPalette.Editor
{
    public class UniPaletteWindow : EditorWindow
    {
        private ICommandSource _commandSource;
        private UnityContextSelection _contextSelection;
        private bool _executing;
        private double _nextContextCheck;
        private Object _observedSelection;
        private int _observedSelectionCount;
        private PalettePresenter _presenter;
        private PaletteSession _session;
        private PaletteView _view;

        private void OnEnable()
        {
            _executing = false;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += RefreshContexts;
            EditorApplication.projectChanged += RefreshContexts;
            EditorApplication.update += CheckContextChanges;
        }

        private void OnDisable()
        {
            _executing = true;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= RefreshContexts;
            EditorApplication.projectChanged -= RefreshContexts;
            EditorApplication.update -= CheckContextChanges;
            DisposeView();
            DisposeSession();
        }

        private void CreateGUI()
        {
            DisposeView();
            DisposeSession();
            titleContent = new GUIContent("UniPalette");
            minSize = new Vector2(360, 240);
            _view = new PaletteView(rootVisualElement);
            _contextSelection = new UnityContextSelection();
            _commandSource = new MenuCommands(_contextSelection);
            _session = new PaletteSession(_commandSource, _contextSelection, new UnityContextSource());
            AttachPresenter();
            _presenter.Render();
        }

        private void OnFocus()
        {
            RefreshContexts();
        }

        private void OnSelectionChanged()
        {
            // A delayed notification for the context we just confirmed is already reflected in the UI.
            if (ReferenceEquals(_observedSelection, Selection.activeObject)
                && _observedSelectionCount == Selection.count)
                return;

            RefreshContexts();
        }

        private void CheckContextChanges()
        {
            if (_session == null || EditorApplication.timeSinceStartup < _nextContextCheck)
                return;

            _nextContextCheck = EditorApplication.timeSinceStartup + 0.1;

            // Unity 6.6 can change Selection through API without delivering selectionChanged.
            // Compare only the active object/count and current target, never search every tick.
            if (!ReferenceEquals(_observedSelection, Selection.activeObject)
                || _observedSelectionCount != Selection.count
                || (_session.Context != null
                    && !_session.Context.IsValid))
                RefreshContexts();
        }

        private void RefreshContexts()
        {
            if (!this || _session == null || _view?.IsAttached != true)
                return;

            ObserveSelection();

            if (_session.ConfirmingContext)
                return;

            _session.Refresh();
            _presenter?.Render(false);
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
            window._presenter?.ScheduleSearchFocus();
        }

        private void AttachPresenter()
        {
            _presenter = new PalettePresenter(
                _view,
                _session,
                new BrowserPresentationSelector(),
                Close,
                ExecuteCommand,
                ObserveSelection
            );
        }

        private void DisposePresenter()
        {
            _presenter?.Dispose();
            _presenter = null;
        }

        private void DisposeView()
        {
            DisposePresenter();
            _view?.Dispose();
            _view = null;
        }

        private void DisposeSession()
        {
            DisposePresenter();
            _session?.Dispose();
            _commandSource?.Dispose();
            _commandSource = null;
        }

        private void ExecuteCommand(PaletteItem command, PaletteItem context)
        {
            if (_executing)
                return;

            if (!NativeExecution.TryPrepare(_contextSelection, command, context, out var execution, out var error))
            {
                _session.ShowExecutionError(error);

                return;
            }

            _executing = true;
            Close();
            execution.ExecuteAfterClose();
        }

        private void ObserveSelection()
        {
            // Acknowledge API changes even when Unity omits selectionChanged.
            _observedSelection = Selection.activeObject;
            _observedSelectionCount = Selection.count;
        }
    }
}