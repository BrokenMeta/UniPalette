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
    [Category("AssetActions")]
    [Category("EditorIntegration")]
    public class AssetActionTests : NativeTestFixture
    {
        private AssetActionCatalog _catalog;

        private AssetActionProbe _probe;

        private AssetContext _context;

        private string _path;

        private ContextActionItem Record()
        {
            Assert.That(_catalog.TryGetItems(_context, out var items, out var error), Is.True, error);
            return items.Single(item => item.Name == "Record Invocation");
        }

        private AssetContext CreateMaterial()
        {
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var path = _folder + "/Sample.mat";
            AssetDatabase.CreateAsset(material, path);
            Selection.activeObject = material;
            return new AssetContext(AssetDatabase.AssetPathToGUID(path));
        }

        [SetUp]
        public void PrepareAsset()
        {
            _catalog = new AssetActionCatalog(new UnityContextSelection());
            _path = _folder + "/Probe.asset";
            _probe = ScriptableObject.CreateInstance<AssetActionProbe>();
            AssetDatabase.CreateAsset(_probe, _path);
            _context = new AssetContext(AssetDatabase.AssetPathToGUID(_path));
            Selection.activeObject = _probe;
        }

        [Test]
        public void SourceDiscoversMaterialActionsAndOmitsEmptyOrUnsupportedGroups()
        {
            using var source = new ContextActionSource<AssetContext>(_catalog, includeEmptyGroup: false);
            var material = CreateMaterial();
            var group = source.ForContext(material).Children.Single();
            Assert.That(group.Title, Is.EqualTo("Asset Actions"));
            Assert.That(group.Children.OfType<ContextActionItem>().Any(), Is.True);
            Assert.That(
                group.Children.All(item => item is ContextActionItem action && action.Context == material),
                Is.True
            );
            Assert.That(source.Error, Is.Empty);

            foreach (var path in new[] { _folder + "/Source.txt", _folder, })
            {
                var context = new AssetContext(AssetDatabase.AssetPathToGUID(path));
                Selection.activeObject = context.Target;
                Assert.That(_catalog.TryGetItems(context, out var actions, out var error), Is.True, error);
                Assert.That(source.ForContext(context).Children.Length, Is.EqualTo(actions.Count == 0 ? 0 : 1));
                Assert.That(source.Error, Is.Empty);
            }

            Assert.That(source.ForContext(new AssetsRootContext()).Children, Is.Empty);
            Assert.That(source.ForContext(new SceneObjectContext(_target)).Children, Is.Empty);
        }

        [Test]
        public void GuidMoveKeepsIdentityAndPreparationRequiresCurrentSelection()
        {
            var item = Record();
            var moved = _folder + "/Moved.asset";
            Assert.That(AssetDatabase.MoveAsset(_path, moved), Is.Empty);
            Assert.That(_context.AssetPath, Is.EqualTo(moved));
            Assert.That(AssetActionExecution.TryPrepare(_catalog, item, out _, out var error), Is.True, error);
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(_folder + "/Source.txt");
            Assert.That(AssetActionExecution.TryPrepare(_catalog, item, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection differs"));
        }

        [Test]
        public void PreparationRejectsDisabledAndMissingActions()
        {
            var material = CreateMaterial();
            Assert.That(_catalog.TryGetItems(material, out var items, out var error), Is.True, error);
            var disabled = items.First(item => !item.IsEnabled);
            Assert.That(AssetActionExecution.TryPrepare(_catalog, disabled, out _, out error), Is.False);
            Assert.That(error, Does.Contain("disabled or hidden"));
            var missing = new ContextActionItem(material, "UniPalette missing action", true, _catalog.GroupTitle);
            Assert.That(AssetActionExecution.TryPrepare(_catalog, missing, out _, out error), Is.False);
            Assert.That(error, Does.Contain("no longer present"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreparedRequestRejectsChangedSelectionOrDeletedGuidOnce(bool delete)
        {
            Assert.That(
                AssetActionExecution.TryPrepare(_catalog, Record(), out var execution, out var error),
                Is.True,
                error
            );

            if (delete)
                Assert.That(AssetDatabase.DeleteAsset(_path), Is.True);
            else
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(_folder + "/Source.txt");

            LogAssert.Expect(LogType.Warning, new Regex("UniPalette contextual action:.*No retry was attempted"));
            execution.ExecuteAfterClose();
            execution.ExecuteAfterClose();

            if (!delete)
            {
                Assert.That(_probe.Calls, Is.Zero);
                Assert.That(Selection.activeObject, Is.InstanceOf<TextAsset>());
            }
        }

        [UnityTest]
        public IEnumerator BrowserClosesPaletteFocusesProjectAndDispatchesOnce()
        {
            var readyDeadline = EditorApplication.timeSinceStartup + 5;
            var readyFrames = 0;

            while (readyFrames < 2 && EditorApplication.timeSinceStartup < readyDeadline)
            {
                yield return null;
                readyFrames = EditorApplication.isCompiling || EditorApplication.isUpdating ? 0 : readyFrames + 1;
            }

            Assert.That(readyFrames, Is.EqualTo(2));
            _window = ScriptableObject.CreateInstance<UniPaletteWindow>();
            _window.ShowPopup();
            _window.Focus();
            yield return null;
            var session = (PaletteSession)typeof(UniPaletteWindow).GetField("_session", Private).GetValue(_window);
            session.Select(session.FindResultIndex(item => item.SelectionEntry));
            Assert.That(session.OpenContextCommands(), Is.True);
            var deadline = EditorApplication.timeSinceStartup + 5;

            while (session.CommandsLoading && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(session.CommandsLoading, Is.False);
            Assert.That(
                session.Results.Any(item => item.Path == "Assets"),
                Is.True,
                "Main-menu commands stay available"
            );
            session.Select(session.FindResultIndex(item => item is ContextActionCategoryItem));
            Assert.That(session.SelectedIndex, Is.GreaterThanOrEqualTo(0));
            PaletteTestData.GetPresenter(_window).Render();
            Key(KeyCode.Return);
            session.Refresh();
            Assert.That(((CommandBrowser)session.Browser).Category.Title, Is.EqualTo("Asset Actions"));
            session.SetQuery("Record Invocation");
            session.Select(0);
            Assert.That(session.Results.Single(), Is.TypeOf<ContextActionItem>());
            PaletteTestData.GetPresenter(_window).Render();
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(_probe.Calls, Is.Zero);
            var search = _window.rootVisualElement.Q<VisualElement>("search");
            Key(KeyCode.Return);
            Assert.That(_window == null, Is.True);
            Assert.That(EditorWindow.focusedWindow.GetType().FullName, Is.EqualTo("UnityEditor.ProjectBrowser"));

            using (var repeated = KeyDownEvent.GetPooled(
                new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, }
            ))
            {
                repeated.target = search;
                search.SendEvent(repeated);
            }

            deadline = EditorApplication.timeSinceStartup + 3;

            while (_probe.Calls == 0 && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(_probe.Calls, Is.EqualTo(1));
            yield return null;
            yield return null;
            Assert.That(_probe.Calls, Is.EqualTo(1));
            Assert.That(Selection.activeObject, Is.SameAs(_probe));
        }
    }
}