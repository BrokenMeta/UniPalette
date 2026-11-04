using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using BrokenMeta.UniPalette.Editor;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("ContextActions")]
    [Category("EditorIntegration")]
    public class ContextActionTests : NativeTestFixture
    {
        private SceneView _sceneView;

        private SceneViewContextActions _source;

        private SceneObjectContext _context;

        private ContextActionItem Duplicate()
        {
            Assert.That(_source.TryGetItems(_context, out var items, out var error), Is.True, error);
            return items.Single(item => item.Name.StartsWith("Duplicate", StringComparison.Ordinal));
        }

        [SetUp]
        public void PrepareSource()
        {
            _context = new SceneObjectContext(_target);
            _source = new SceneViewContextActions(new UnityContextSelection());
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
            var item = Duplicate();
            Assert.That(item.Context, Is.SameAs(_context));
            Assert.That(item.IsEnabled, Is.True);
            Assert.That(Selection.activeGameObject, Is.SameAs(_target));
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focused));
            Assert.That(_testScene.rootCount, Is.EqualTo(1));

            Selection.objects = Array.Empty<Object>();
            Assert.That(_source.TryGetItems(_context, out var items, out var error), Is.False);
            Assert.That(items, Is.Empty);
            Assert.That(error, Does.Contain("Selection differs"));
            Assert.That(Selection.count, Is.Zero);
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

            Assert.That(_source.TryPrepare(item, _sceneView, out var execution, out var error), Is.False);
            Assert.That(execution, Is.Null);
            Assert.That(error, Does.Contain(deleteTarget ? "no longer exists" : "Selection differs"));
        }

        [Test]
        public void PreparationResolvesFreshActionInsteadOfTrustingDescriptor()
        {
            var item = Duplicate();
            var oldDisabledFlag = new ContextActionItem(_context, item.Name, false);
            Assert.That(_source.TryPrepare(oldDisabledFlag, _sceneView, out _, out var error), Is.True, error);

            var missing = new ContextActionItem(_context, "UniPalette missing action", true);
            Assert.That(_source.TryPrepare(missing, _sceneView, out _, out error), Is.False);
            Assert.That(error, Does.Contain("no longer present"));
            Assert.That(_source.TryPrepare(item, null, out _, out error), Is.False);
            Assert.That(error, Does.Contain("explicit Scene View"));
            _sceneView.Close();
            Assert.That(_source.TryPrepare(item, _sceneView, out _, out error), Is.False);
            Assert.That(error, Does.Contain("explicit Scene View"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreparedRequestRejectsStaleContextAndConsumesRepeatedInput(bool deleteTarget)
        {
            Assert.That(_source.TryPrepare(Duplicate(), _sceneView, out var execution, out var error), Is.True, error);

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

        [UnityTest]
        public IEnumerator DispatchAfterPaletteCloseFocusesChosenViewAndDuplicatesOnce()
        {
            var readyDeadline = EditorApplication.timeSinceStartup + 5;
            var readyFrames = 0;

            while (readyFrames < 2 && EditorApplication.timeSinceStartup < readyDeadline)
            {
                yield return null;
                readyFrames = EditorApplication.isCompiling || EditorApplication.isUpdating ? 0 : readyFrames + 1;
            }

            Assert.That(readyFrames, Is.EqualTo(2), "Editor did not finish importing the disposable fixture");
            Assert.That(_source.TryPrepare(Duplicate(), _sceneView, out var execution, out var error), Is.True, error);
            _window = ScriptableObject.CreateInstance<UniPaletteWindow>();
            _window.ShowPopup();
            _window.Focus();
            yield return null;
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(_window));
            _window.Close();
            execution.ExecuteAfterClose();
            execution.ExecuteAfterClose();
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(_sceneView));

            var deadline = EditorApplication.timeSinceStartup + 3;

            while (_testScene.rootCount == 1 && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(_testScene.rootCount, Is.EqualTo(2));
            yield return null;
            yield return null;
            Assert.That(_testScene.rootCount, Is.EqualTo(2), "Repeated input must not queue another action");
            Assert.That(_window == null, Is.True);
        }
    }
}
