using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Lifetime;
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
        private SubscriptionScope _editorSubscriptions;

        private ICommandSource _commandSource;
        private UnityContextSelection _contextSelection;

        private SceneViewActionCatalog _sceneViewActions;

        private AssetActionCatalog _assetActions;

        private SceneView _contextSceneView;
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
            _contextSceneView = SceneView.lastActiveSceneView;
            _editorSubscriptions?.Dispose();
            _editorSubscriptions = new SubscriptionScope();

            Subscription
                .Create(
                    () => Selection.selectionChanged += OnSelectionChanged,
                    () => Selection.selectionChanged -= OnSelectionChanged
                )
                .AddTo(_editorSubscriptions);

            Subscription
                .Create(
                    () => EditorApplication.hierarchyChanged += RefreshContexts,
                    () => EditorApplication.hierarchyChanged -= RefreshContexts
                )
                .AddTo(_editorSubscriptions);

            Subscription
                .Create(
                    () => EditorApplication.projectChanged += RefreshContexts,
                    () => EditorApplication.projectChanged -= RefreshContexts
                )
                .AddTo(_editorSubscriptions);

            Subscription
                .Create(
                    () => EditorApplication.update += CheckContextChanges,
                    () => EditorApplication.update -= CheckContextChanges
                )
                .AddTo(_editorSubscriptions);
        }

        private void OnDisable()
        {
            _executing = true;
            _editorSubscriptions?.Dispose();
            _editorSubscriptions = null;
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
            _sceneViewActions = new SceneViewActionCatalog(_contextSelection);
            _assetActions = new AssetActionCatalog(_contextSelection);
            _commandSource = new PaletteCommands(
                new MenuCommands(_contextSelection),
                new SceneViewContextActions(_sceneViewActions),
                new AssetContextActions(_assetActions)
            );
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

            if (command is ContextActionItem action)
            {
                if (!action.Context.SameIdentity(context))
                {
                    _session.ShowExecutionError("Contextual action does not belong to the confirmed context.");
                    return;
                }

                if (action.Context is AssetContext)
                {
                    if (!AssetActionExecution.TryPrepare(_assetActions, action, out var assetExecution, out var assetError))
                    {
                        _session.ShowExecutionError(assetError);
                        return;
                    }

                    CloseAndExecute(assetExecution.ExecuteAfterClose);
                    return;
                }

                if (!SceneViewActionExecution.TryPrepare(
                    _sceneViewActions,
                    action,
                    _contextSceneView,
                    out var contextual,
                    out var contextError
                ))
                {
                    _session.ShowExecutionError(contextError);
                    return;
                }

                CloseAndExecute(contextual.ExecuteAfterClose);
                return;
            }

            if (!NativeExecution.TryPrepare(_contextSelection, command, context, out var execution, out var error))
            {
                _session.ShowExecutionError(error);

                return;
            }

            CloseAndExecute(execution.ExecuteAfterClose);
        }

        private void CloseAndExecute(System.Action execute)
        {
            _executing = true;
            Close();
            execute();
        }

        private void ObserveSelection()
        {
            // Acknowledge API changes even when Unity omits selectionChanged.
            _observedSelection = Selection.activeObject;
            _observedSelectionCount = Selection.count;
        }
    }
}