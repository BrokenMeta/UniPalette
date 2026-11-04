using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using BrokenMeta.UniPalette.Editor;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("ContextActions")]
    [Category("EditorIntegration")]
    public class ContextActionTests : NativeTestFixture
    {
        private SceneView _sceneView;

        private SceneViewActionCatalog _catalog;

        private SceneObjectContext _context;

        private PaletteSession _paletteSession;

        private ContextActionItem Duplicate()
        {
            Assert.That(_catalog.TryGetItems(_context, out var items, out var error), Is.True, error);
            return items.Single(item => item.Name.StartsWith("Duplicate", StringComparison.Ordinal));
        }

        [SetUp]
        public void PrepareSource()
        {
            _context = new SceneObjectContext(_target);
            _catalog = new SceneViewActionCatalog(new UnityContextSelection());
            _sceneView = ScriptableObject.CreateInstance<SceneView>();
            _sceneView.Show();
            Selection.activeGameObject = _target;
        }

        [TearDown]
        public void CloseSceneView()
        {
            if (_sceneView)
                _sceneView.Close();
        }

        [Test]
        public void CollectionIsPassiveAndRequiresConfirmedSelection()
        {
            var focused = EditorWindow.focusedWindow;
            using ICommandSource source = new SceneViewContextActions(_catalog);
            var tree = source.ForContext(_context);
            var item = tree.Children.Single().Children
                .OfType<ContextActionItem>()
                .Single(action => action.Name.StartsWith("Duplicate", StringComparison.Ordinal));
            Assert.That(source.IsLoading, Is.False);
            Assert.That(source.Error, Is.Empty);
            Assert.That(item.Context, Is.SameAs(_context));
            Assert.That(item.IsEnabled, Is.True);
            Assert.That(Selection.activeGameObject, Is.SameAs(_target));
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focused));
            Assert.That(_testScene.rootCount, Is.EqualTo(1));

            Selection.objects = Array.Empty<Object>();
            Assert.That(source.ForContext(_context).Children, Is.Empty);
            Assert.That(source.Error, Does.Contain("Selection differs"));
            Assert.That(Selection.count, Is.Zero);
            Assert.That(source.ForContext(new AssetsRootContext()).Children, Is.Empty);
            Assert.That(source.Error, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreparationRejectsChangedSelectionOrDeletedTarget(bool deleteTarget)
        {
            var item = Duplicate();

            if (deleteTarget)
                Object.DestroyImmediate(_target);
            else
                Selection.objects = Array.Empty<Object>();

            Assert.That(SceneViewActionExecution.TryPrepare(_catalog, item, _sceneView, out var execution, out var error), Is.False);
            Assert.That(execution, Is.Null);
            Assert.That(error, Does.Contain(deleteTarget ? "no longer exists" : "Selection differs"));
        }

        [Test]
        public void PreparationResolvesFreshActionInsteadOfTrustingDescriptor()
        {
            var item = Duplicate();
            var oldDisabledFlag = new ContextActionItem(_context, item.Name, false);
            Assert.That(SceneViewActionExecution.TryPrepare(_catalog, oldDisabledFlag, _sceneView, out _, out var error), Is.True, error);

            var missing = new ContextActionItem(_context, "UniPalette missing action", true);
            Assert.That(SceneViewActionExecution.TryPrepare(_catalog, missing, _sceneView, out _, out error), Is.False);
            Assert.That(error, Does.Contain("no longer present"));
            Assert.That(SceneViewActionExecution.TryPrepare(_catalog, item, null, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Scene View"));
            _sceneView.Close();
            Assert.That(SceneViewActionExecution.TryPrepare(_catalog, item, _sceneView, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Scene View"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreparedRequestRejectsStaleContextAndConsumesRepeatedInput(bool deleteTarget)
        {
            Assert.That(SceneViewActionExecution.TryPrepare(_catalog, Duplicate(), _sceneView, out var execution, out var error), Is.True, error);

            if (deleteTarget)
                Object.DestroyImmediate(_target);
            else
                Selection.objects = Array.Empty<Object>();

            LogAssert.Expect(LogType.Warning, new Regex("UniPalette contextual action:.*No retry was attempted"));
            execution.ExecuteAfterClose();
            execution.ExecuteAfterClose();
            Assert.That(_testScene.rootCount, Is.EqualTo(deleteTarget ? 0 : 1));
            Assert.That(Selection.count, Is.Zero);
        }

        private IEnumerator OpenActions()
        {
            var readyDeadline = EditorApplication.timeSinceStartup + 5;
            var readyFrames = 0;

            while (readyFrames < 2 && EditorApplication.timeSinceStartup < readyDeadline)
            {
                yield return null;
                readyFrames = EditorApplication.isCompiling || EditorApplication.isUpdating ? 0 : readyFrames + 1;
            }

            Assert.That(readyFrames, Is.EqualTo(2), "Editor did not finish importing the disposable fixture");
            _window = ScriptableObject.CreateInstance<UniPaletteWindow>();
            _window.ShowPopup();
            _window.Focus();
            yield return null;
            _paletteSession = (PaletteSession)typeof(UniPaletteWindow).GetField("_session", Private).GetValue(_window);
            _paletteSession.Select(_paletteSession._results.FindIndex(item => item.SelectionEntry));
            Assert.That(_paletteSession.OpenContextCommands(), Is.True);

            var deadline = EditorApplication.timeSinceStartup + 5;

            while (_paletteSession.CommandsLoading && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(_paletteSession.CommandsLoading, Is.False);
            _paletteSession.Select(_paletteSession._results.FindIndex(item => item is ContextActionCategoryItem));
            Assert.That(_paletteSession.SelectedIndex, Is.GreaterThanOrEqualTo(0));
            PaletteTestData.GetPresenter(_window).Render();
            Key(KeyCode.Return);
            Assert.That(((CommandBrowser)_paletteSession.Browser).Category, Is.TypeOf<ContextActionCategoryItem>());
            _paletteSession.SetQuery("Duplicate");
            Assert.That(_paletteSession._results.Single(), Is.TypeOf<ContextActionItem>());
            _paletteSession.Select(0);
            PaletteTestData.GetPresenter(_window).Render();
        }

        [Test]
        public void CombinedSourceSeparatesOriginsAndPreservesCategoryAcrossRefresh()
        {
            var nativeDuplicate = new MenuCommandItem("Duplicate", Duplicate().Path);
            var nativeCategory = new MenuCategoryItem(
                "Scene View Actions",
                "Scene View Actions",
                new PaletteItem[] { nativeDuplicate, }
            );
            var nativeRoot = new MenuCategoryItem("Commands", "", new PaletteItem[] { nativeCategory, });
            var menu = new TestCommandSource(nativeRoot);
            using var source = new PaletteCommands(menu, new SceneViewContextActions(_catalog));
            var notifications = 0;
            using var subscription = source.SubscribeChanged(() => notifications++);
            menu.Complete(nativeRoot);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(source.ForContext(new AssetsRootContext()).Children, Is.EqualTo(nativeRoot.Children));
            Assert.That(source.ForContext(new AssetContext("missing")).Children, Is.EqualTo(nativeRoot.Children));
            Assert.That(source.ForContext(new SceneRootContext()).Children, Is.EqualTo(nativeRoot.Children));

            using var session = new PaletteSession(
                source,
                new UnityContextSelection(),
                new TestContextSource(new PaletteItem[] { _context, })
            );
            session.Select(0);
            session.OpenContextCommands();
            Assert.That(session._results[0], Is.SameAs(nativeCategory));
            session.Select(1);
            Assert.That(session.BrowseSelected(), Is.True);
            session.Refresh();
            Assert.That(((CommandBrowser)session.Browser).Category, Is.TypeOf<ContextActionCategoryItem>());
            session.SetQuery("Duplicate");
            var action = session._results.Single();
            Assert.That(action.SameIdentity(nativeDuplicate), Is.False);
            Assert.That(nativeDuplicate.SameIdentity(action), Is.False);
            session.Select(0);
            Assert.That(session.BrowseSelected(), Is.False);
            Assert.That(session.GoBack(), Is.True);
            Assert.That(session._results[session.SelectedIndex], Is.TypeOf<ContextActionCategoryItem>());
        }

        [UnityTest]
        public IEnumerator ClosedSceneViewLeavesPaletteOpenWithPreparationError()
        {
            yield return OpenActions();
            _sceneView.Close();
            // Regaining focus refreshes results and clears row selection, as in the existing UI.
            _paletteSession.Select(_paletteSession._results.FindIndex(item => item is ContextActionItem));
            PaletteTestData.GetPresenter(_window).Render();
            Key(KeyCode.Return);
            Assert.That(_window != null, Is.True);
            Assert.That(_paletteSession.Message, Does.Contain("Scene View"));
            Assert.That(_testScene.rootCount, Is.EqualTo(1));
            Assert.That(Selection.activeGameObject, Is.SameAs(_target));
        }

        [UnityTest]
        public IEnumerator DispatchFromBrowserClosesPaletteFocusesChosenViewAndDuplicatesOnce()
        {
            yield return OpenActions();
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(_window != null, Is.True, "Alt navigation must not dispatch a leaf");
            Assert.That(_testScene.rootCount, Is.EqualTo(1));
            var search = _window.rootVisualElement.Q<UnityEngine.UIElements.VisualElement>("search");
            Key(KeyCode.Return);
            Assert.That(_window == null, Is.True);
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(_sceneView));

            // A repeated input on the detached view must not queue another action.
            using (var repeated = UnityEngine.UIElements.KeyDownEvent.GetPooled(
                new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, }
            ))
            {
                repeated.target = search;
                search.SendEvent(repeated);
            }

            var deadline = EditorApplication.timeSinceStartup + 3;

            while (_testScene.rootCount == 1 && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(_testScene.rootCount, Is.EqualTo(2));
            yield return null;
            yield return null;
            Assert.That(_testScene.rootCount, Is.EqualTo(2));
        }
    }
}
