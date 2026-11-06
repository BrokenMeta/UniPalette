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
        private EditorContextObserver _contextObserver;

        private ICommandSource _commandSource;

        private SubscriptionScope _sessionSubscriptions;

        private CommandExecutionPreparation _executionPreparation;

        private SceneView _contextSceneView;
        private bool _executing;
        private PalettePresenter _presenter;
        private PaletteSession _session;
        private PaletteView _view;

        private void OnEnable()
        {
            _executing = false;
            _contextSceneView = SceneView.lastActiveSceneView;
            _contextObserver?.Dispose();
            _contextObserver = new EditorContextObserver(() => _session?.Context, RefreshContexts);
        }

        private void OnDisable()
        {
            _executing = true;
            _contextObserver?.Dispose();
            _contextObserver = null;
            DisposeSession();
            DisposeView();
            _executionPreparation = null;
        }

        private void CreateGUI()
        {
            DisposeSession();
            DisposeView();
            titleContent = new GUIContent("UniPalette");
            minSize = new Vector2(360, 240);
            _view = new PaletteView(rootVisualElement);
            var contextSelection = new UnityContextSelection();
            var sceneViewActions = new SceneViewActionCatalog(contextSelection);
            var assetActions = new AssetActionCatalog(contextSelection);
            var commandSource = new PaletteCommands(
                new MenuCommands(contextSelection),
                new ContextActionSource<SceneObjectContext>(sceneViewActions, includeEmptyGroup: true),
                new ContextActionSource<AssetContext>(assetActions, includeEmptyGroup: false)
            );
            _executionPreparation = new CommandExecutionPreparation(
                contextSelection,
                sceneViewActions,
                assetActions,
                _contextSceneView
            );
            ReplaceSession(new PaletteSession(commandSource, contextSelection, new UnityContextSource()), commandSource);
        }

        private void OnFocus()
        {
            RefreshContexts();
        }

        private void RefreshContexts()
        {
            if (!this || _session == null || _view?.IsAttached != true)
                return;

            _contextObserver?.AcknowledgeSelection();

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

        private void DisposeView()
        {
            _view?.Dispose();
            _view = null;
        }

        private void DisposeSession()
        {
            _presenter?.Dispose();
            _presenter = null;
            _sessionSubscriptions?.Dispose();
            _sessionSubscriptions = null;
            _session?.Dispose();
            _session = null;
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

        // The window takes ownership of the session and, when supplied, its command source.
        internal void ReplaceSession(PaletteSession session, ICommandSource ownedSource = null)
        {
            DisposeSession();
            _session = session;
            _commandSource = ownedSource;
            _presenter = new PalettePresenter(
                _view,
                _session,
                new BrowserPresentationSelector(),
                Close
            );
            _sessionSubscriptions = new SubscriptionScope();
            _session
                .SubscribeContextConfirmed(() => _contextObserver?.AcknowledgeSelection())
                .AddTo(_sessionSubscriptions);
            _session
                .SubscribeExecutionRequested(ExecuteCommand)
                .AddTo(_sessionSubscriptions);
            _contextObserver?.AcknowledgeSelection();
            _presenter.Render();
        }
    }
}