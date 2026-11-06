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

        private System.IDisposable _contextConfirmationSubscription;

        private CommandExecutionPreparation _executionPreparation;

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
            _executionPreparation = null;
        }

        private void CreateGUI()
        {
            DisposeView();
            DisposeSession();
            titleContent = new GUIContent("UniPalette");
            minSize = new Vector2(360, 240);
            _view = new PaletteView(rootVisualElement);
            var contextSelection = new UnityContextSelection();
            var sceneViewActions = new SceneViewActionCatalog(contextSelection);
            var assetActions = new AssetActionCatalog(contextSelection);
            _commandSource = new PaletteCommands(
                new MenuCommands(contextSelection),
                new SceneViewContextActions(sceneViewActions),
                new AssetContextActions(assetActions)
            );
            _executionPreparation = new CommandExecutionPreparation(
                contextSelection,
                sceneViewActions,
                assetActions,
                _contextSceneView
            );
            _session = new PaletteSession(_commandSource, contextSelection, new UnityContextSource());
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
                ExecuteCommand
            );
            _contextConfirmationSubscription = _session.SubscribeContextConfirmed(ObserveSelection);
            ObserveSelection();
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
            _contextConfirmationSubscription?.Dispose();
            _contextConfirmationSubscription = null;
            _session?.Dispose();
            _commandSource?.Dispose();
            _commandSource = null;
        }

        private void ExecuteCommand(PaletteItem command, PaletteItem context)
        {
            if (_executing)
                return;

            if (!_executionPreparation.TryPrepare(command, context, out var executeAfterClose, out var error))
            {
                _session.ShowExecutionError(error);
                return;
            }

            _executing = true;
            Close();
            executeAfterClose();
        }

        private void ObserveSelection()
        {
            // Acknowledge API changes even when Unity omits selectionChanged.
            _observedSelection = Selection.activeObject;
            _observedSelectionCount = Selection.count;
        }
    }
}