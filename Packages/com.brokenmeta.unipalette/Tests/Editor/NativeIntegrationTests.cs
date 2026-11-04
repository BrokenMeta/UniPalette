using System;
using System.Collections;
using System.IO;
using System.Linq;
using BrokenMeta.UniPalette.Editor;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("NativeIntegration")]
    [Category("EditorIntegration")]
    public class NativeIntegrationTests : NativeTestFixture
    {
        private IEnumerator Run(string kind, string path, bool click, bool withOtherComponent = false)
        {
            if (withOtherComponent)
                _target.AddComponent<Rigidbody>();

            if (kind == "OtherAsset")
            {
                File.WriteAllBytes(_folder + "/Source.bytes", new byte[] { 1, 2, 3, });
                AssetDatabase.ImportAsset(_folder + "/Source.bytes", ImportAssetOptions.ForceSynchronousImport);
            }

            var context = Context(kind);
            var copyPath = path == "Assets/Copy Path";

            var filesBefore = Directory
                .GetFiles(_temporaryRoot, "*", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .Select(
                    f => f
                        + ":"
                        + Convert.ToBase64String(File.ReadAllBytes(f))
                )
                .ToArray();

            if (copyPath)
                EditorGUIUtility.systemCopyBuffer = "UniPalette clipboard sentinel";

            GameObject child = null;
            Selection.activeObject = context.Target;
            _window = EditorWindow.GetWindow<UniPaletteWindow>();

            typeof(UniPaletteWindow)
                .GetMethod("CreateGUI", Private)
                .Invoke(_window, null);

            _window.Show();
            _window.Focus();

            var session = (PaletteSession)typeof(UniPaletteWindow)
                .GetField("_session", Private)
                .GetValue(_window);

            session.Select(session._results.FindIndex(i => i.SelectionEntry));
            Key(KeyCode.Return);
            var deadline = EditorApplication.timeSinceStartup + 5;

            while (session.CommandsLoading && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            _window
                .rootVisualElement
                .Q<VisualElement>("search")
                .Q<TextField>()
                .value = path;

            var index = session._results.FindIndex(i => i.Path == path);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Supported command must be discoverable");
            session.Select(index);
            var list = _window.rootVisualElement.Q<ListView>("results");
            list.SetSelectionWithoutNotify(new[] { index, });

            yield return null;
            Assert.That(new UnityContextSelection().MatchesSelection(context), Is.True);
            var renderedRow = list.GetRootElementForIndex(index);
            Assert.That(renderedRow, Is.Not.Null);

            Assert.That(
                renderedRow.tooltip,
                Does
                    .Contain("Native execution")
                    .And.Not
                    .Contain("Unsupported")
            );

            Action activate = PaletteTestData.GetPresenter(_window).ActivateSelected;

            if (click)
            {
                var row = list.GetRootElementForIndex(index);
                Assert.That(row, Is.Not.Null);
                Click(row, 2);
                Assert.That(_window, Is.Not.Null, "Second click alone must not execute");
                Click(row, 1);
            }
            else
            {
                Key(KeyCode.Return);
            }

            Assert.That(
                !_window,
                Is.True,
                "Native dispatch closes the palette; message="
                + session.Message
                + "; selected="
                + session.SelectedIndex
                + "; query="
                + session.Query
            );

            // A stale UI callback cannot dispatch a second time after OnDisable.
            activate();

            var expected = kind == "GameObject"
                ? "Unity.Hierarchy.Editor.HierarchyWindow"
                : "UnityEditor.ProjectBrowser";

            // Focus is a dispatch precondition, not a promise about Unity's next frame.
            Assert.That(EditorWindow.focusedWindow, Is.Not.Null);
            Assert.That(EditorWindow.focusedWindow.GetType().FullName, Is.EqualTo(expected));

            yield return null;

            if (kind == "GameObject")
            {
                if (path == "Edit/Duplicate")
                {
                    Assert.That(_testScene.rootCount, Is.EqualTo(2), "Exactly one duplicate");
                }
                else if (path == "GameObject/Create Empty")
                {
                    Assert.That(_testScene.rootCount, Is.EqualTo(2));
                }
                else if (path == "GameObject/Create Empty Parent")
                {
                    Assert.That(_target.transform.parent, Is.Not.Null);
                    Assert.That(_target.transform.parent.childCount, Is.EqualTo(1));
                    Assert.That(_testScene.rootCount, Is.EqualTo(1));
                }
                else if (path == "GameObject/Create Empty Child")
                {
                    Assert.That(_target.transform.childCount, Is.EqualTo(1));
                    child = _target.transform.GetChild(0).gameObject;
                    Assert.That(child.scene, Is.EqualTo(_testScene));
                    Assert.That(Selection.activeGameObject, Is.EqualTo(child));
                    Assert.That(_testScene.rootCount, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(_target.GetComponents<BoxCollider>().Length, Is.EqualTo(1));
                    Assert.That(_target.GetComponents<Component>().Length, Is.EqualTo(withOtherComponent ? 3 : 2));
                    Assert.That(Selection.activeGameObject, Is.EqualTo(_target));
                    Assert.That(_testScene.rootCount, Is.EqualTo(1));
                }

                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(_testScene.rootCount, Is.EqualTo(1));
                Assert.That(_target.transform.parent, Is.Null, "GameObject Undo restores original hierarchy");
                Assert.That(_target.transform.childCount, Is.Zero);
                Assert.That(_target.GetComponents<BoxCollider>(), Is.Empty);
                Undo.PerformRedo();

                if (path == "GameObject/Create Empty Child")
                {
                    Assert.That(_target.transform.childCount, Is.EqualTo(1));
                    Assert.That(_target.transform.GetChild(0).gameObject, Is.EqualTo(child));
                }
                else if (path == "Component/Physics/Box Collider")
                {
                    Assert.That(_target.GetComponents<BoxCollider>().Length, Is.EqualTo(1));
                }
                else if (path == "Edit/Duplicate" || path == "GameObject/Create Empty")
                {
                    Assert.That(_testScene.rootCount, Is.EqualTo(2));
                }
                else
                {
                    Assert.That(_target.transform.parent, Is.Not.Null);
                }
            }
            else if (copyPath)
            {
                Assert.That(
                    EditorGUIUtility.systemCopyBuffer,
                    Is.EqualTo(AssetDatabase.GetAssetPath(context.Target))
                );

                Assert.That(Selection.activeObject, Is.EqualTo(context.Target));
                Assert.That(_testScene.rootCount, Is.EqualTo(1));

                var filesAfter = Directory
                    .GetFiles(_temporaryRoot, "*", SearchOption.AllDirectories)
                    .OrderBy(f => f)
                    .Select(
                        f => f
                            + ":"
                            + Convert.ToBase64String(File.ReadAllBytes(f))
                    )
                    .ToArray();

                Assert.That(
                    filesAfter,
                    Is.EqualTo(filesBefore),
                    "Clipboard command must not create or change files, including meta files"
                );
            }
            else if (kind == "Asset")
            {
                var files = Directory.GetFiles(_folder, "*.txt");
                Assert.That(files.Length, Is.EqualTo(2), "Exactly one asset duplicate");
                Assert.That(files.All(f => File.ReadAllText(f) == "Native execution fixture"), Is.True);
            }
            else
            {
                var copy = _folder + " 1";

                try
                {
                    Assert.That(AssetDatabase.IsValidFolder(copy), Is.True, "Folder duplicate exists");
                    Assert.That(File.ReadAllText(copy + "/Source.txt"), Is.EqualTo("Native execution fixture"));
                    Assert.That(AssetDatabase.IsValidFolder(_folder + " 2"), Is.False, "No second duplicate");
                }
                finally
                {
                    AssetDatabase.DeleteAsset(copy);
                }
            }

            EditorApplication.ExecuteMenuItem("Window/UniPalette");
            _window = EditorWindow.GetWindow<UniPaletteWindow>();

            yield return null;

            var reopened = (PaletteSession)typeof(UniPaletteWindow)
                .GetField("_session", Private)
                .GetValue(_window);

            Assert.That(reopened.Browser, Is.TypeOf<ContextBrowser>());
            Assert.That(reopened.Context, Is.Null, "Reopened window has a new navigation lifecycle");
        }

        private IEnumerator RunRoot(string kind, string path, bool click)
        {
            var sceneRoot = kind == "SceneRoot";
            var createFolder = path == "Assets/Create/Folder";
            var foldersBefore = AssetDatabase.GetSubFolders("Assets");

            if (!sceneRoot)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = Context("Folder").Target;
                Assert.That(AssetDatabase.OpenAsset(Selection.activeObject), Is.True);

                for (var i = 0; i < 4; i++)
                    yield return null;
            }

            Selection.activeObject = sceneRoot ? _target : Context("Asset").Target;
            _window = EditorWindow.GetWindow<UniPaletteWindow>();

            typeof(UniPaletteWindow)
                .GetMethod("CreateGUI", Private)
                .Invoke(_window, null);

            _window.Show();
            _window.Focus();

            var session = (PaletteSession)typeof(UniPaletteWindow)
                .GetField("_session", Private)
                .GetValue(_window);

            var rootIndex = session._results.FindIndex(
                i => i.GetType()
                    == CreateRoot(kind)
                        .GetType()
            );

            Assert.That(rootIndex, Is.GreaterThanOrEqualTo(0));
            session.Select(rootIndex);
            Key(KeyCode.Return, EventModifiers.Control);
            Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());

            Assert.That(
                session.Context.GetType(),
                Is.EqualTo(
                    CreateRoot(kind)
                        .GetType()
                )
            );

            Assert.That(session.Context.Target, Is.Null);
            Assert.That(new UnityContextSelection().MatchesSelection(session.Context), Is.True);

            if (sceneRoot)
                Assert.That(Selection.count, Is.Zero);
            else
                Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo("Assets"));

            var deadline = EditorApplication.timeSinceStartup + 5;

            while (session.CommandsLoading && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            _window
                .rootVisualElement
                .Q<VisualElement>("search")
                .Q<TextField>()
                .value = path;

            var index = session._results.FindIndex(i => i.Path == path);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Root command must be discovered");
            session.Select(index);
            var list = _window.rootVisualElement.Q<ListView>("results");
            list.SetSelectionWithoutNotify(new[] { index, });

            yield return null;
            var row = list.GetRootElementForIndex(index);
            Assert.That(row, Is.Not.Null);
            Assert.That(row.tooltip, Does.Contain("Native execution"));
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Key(KeyCode.Return, EventModifiers.Control);
            Assert.That(_window, Is.Not.Null, "Alt+Right and Ctrl+Enter do not dispatch a leaf");
            Assert.That(new UnityContextSelection().MatchesSelection(session.Context), Is.True);

            if (createFolder)
            {
                _rootFolderPath = AssetDatabase.GenerateUniqueAssetPath("Assets/New Folder");
                Assert.That(Directory.Exists(_rootFolderPath), Is.False);
            }
            else if (!sceneRoot)
            {
                EditorGUIUtility.systemCopyBuffer = "root clipboard sentinel";
            }

            Action activate = PaletteTestData.GetPresenter(_window).ActivateSelected;

            if (click)
            {
                Click(row, 2);
                Assert.That(_window, Is.Not.Null);
                Click(row, 1);
            }
            else
            {
                Key(KeyCode.Return);
            }

            Assert.That(!_window, Is.True, "Dispatch closes the palette");
            activate();

            yield return null;

            var expectedWindow = sceneRoot
                ? "Unity.Hierarchy.Editor.HierarchyWindow"
                : "UnityEditor.ProjectBrowser";

            Assert.That(EditorWindow.focusedWindow.GetType().FullName, Is.EqualTo(expectedWindow));

            if (sceneRoot)
            {
                Assert.That(_testScene.rootCount, Is.EqualTo(2), "Exactly one new root object");

                var created = _testScene
                    .GetRootGameObjects()
                    .Single(go => go != _target);

                Assert.That(created.transform.parent, Is.Null);
                Assert.That(created.scene, Is.EqualTo(_testScene));
                Assert.That(Selection.activeGameObject, Is.EqualTo(created));
                Assert.That(_target.transform.childCount, Is.Zero, "Previous Selection was not used as parent");

                if (path.EndsWith("/Cube", StringComparison.Ordinal))
                    Assert.That(created.GetComponent<MeshFilter>(), Is.Not.Null);

                Undo.PerformUndo();
                Assert.That(_testScene.rootCount, Is.EqualTo(1));
                Undo.PerformRedo();
                Assert.That(_testScene.rootCount, Is.EqualTo(2));
            }
            else if (createFolder)
            {
                Assert.That(
                    AssetDatabase.IsValidFolder(_rootFolderPath),
                    Is.False,
                    "Unity is still editing the name after dispatch"
                );

                var project = EditorWindow.focusedWindow;
                project.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, });
                project.SendEvent(new Event { type = EventType.KeyUp, keyCode = KeyCode.Return, });

                for (var i = 0; i < 8; i++)
                    yield return null;

                if (AssetDatabase.IsValidFolder(_rootFolderPath))
                    _rootFolderGuid = AssetDatabase.AssetPathToGUID(_rootFolderPath);

                Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo(_rootFolderPath));
                Assert.That(_rootFolderGuid, Is.Not.Null.And.Not.Empty);

                Assert.That(
                    AssetDatabase
                        .GetSubFolders("Assets")
                        .Except(foldersBefore),
                    Is.EqualTo(new[] { _rootFolderPath, }),
                    "Exactly one folder in Assets, not the previous Project folder"
                );

                Assert.That(AssetDatabase.GetSubFolders(_folder), Is.Empty);
                Assert.That(File.ReadAllText(_folder + "/Source.txt"), Is.EqualTo("Native execution fixture"));
            }
            else
            {
                Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo("Assets"));
                Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo("Assets"));
                Assert.That(AssetDatabase.GetSubFolders("Assets"), Is.EqualTo(foldersBefore));
            }

            Assert.That(_target.transform.childCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DuplicateGameObjectEnter() =>
            Run("GameObject", "Edit/Duplicate", false);

        [UnityTest]
        public IEnumerator DuplicateGameObjectClick() =>
            Run("GameObject", "Edit/Duplicate", true);

        [UnityTest]
        public IEnumerator ParentGameObjectEnter() =>
            Run("GameObject", "GameObject/Create Empty Parent", false);

        [UnityTest]
        public IEnumerator DuplicateAssetEnter() =>
            Run("Asset", "Edit/Duplicate", false);

        [UnityTest]
        public IEnumerator DuplicateFolderEnter() =>
            Run("Folder", "Edit/Duplicate", false);

        [UnityTest]
        public IEnumerator ChildGameObjectEnter() =>
            Run("GameObject", "GameObject/Create Empty Child", false);

        [UnityTest]
        public IEnumerator BoxColliderEnter() =>
            Run("GameObject", "Component/Physics/Box Collider", false);

        [UnityTest]
        public IEnumerator CopyAssetPathEnter() =>
            Run("Asset", "Assets/Copy Path", false);

        [UnityTest]
        public IEnumerator CopyAssetPathClick() =>
            Run("Asset", "Assets/Copy Path", true);

        [UnityTest]
        public IEnumerator CopyFolderPathEnter() =>
            Run("Folder", "Assets/Copy Path", false);

        [UnityTest]
        public IEnumerator CreateEmptyEnter() =>
            Run("GameObject", "GameObject/Create Empty", false);

        [UnityTest]
        public IEnumerator CopyNonTxtAssetPathEnter() =>
            Run("OtherAsset", "Assets/Copy Path", false);

        [UnityTest]
        public IEnumerator BoxColliderWithRigidbodyEnter() =>
            Run(
                "GameObject",
                "Component/Physics/Box Collider",
                false,
                true
            );

        [UnityTest]
        public IEnumerator PreCloseFailureKeepsPaletteAndSelection()
        {
            Selection.activeObject = _target;
            _window = EditorWindow.GetWindow<UniPaletteWindow>();

            typeof(UniPaletteWindow)
                .GetMethod("CreateGUI", Private)
                .Invoke(_window, null);

            _window.Show();
            _window.Focus();

            var session = (PaletteSession)typeof(UniPaletteWindow)
                .GetField("_session", Private)
                .GetValue(_window);

            session.Select(session._results.FindIndex(i => i.SelectionEntry));
            Key(KeyCode.Return);
            var deadline = EditorApplication.timeSinceStartup + 5;

            while (session.CommandsLoading && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            session.SetQuery("Edit/Duplicate");
            session.Select(session._results.FindIndex(i => i.Path == "Edit/Duplicate"));
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(_folder + "/Source.txt");
            // Direct state activation preserves the stale row to exercise the final preflight check.
            session.ActivateSelected();
            Assert.That(_window, Is.Not.Null);
            Assert.That(session.Message, Does.Contain("Selection"));
            Assert.That(_testScene.rootCount, Is.EqualTo(1));
            Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo(_folder + "/Source.txt"));
        }

        [UnityTest]
        public IEnumerator SceneRootEmptyEnter() =>
            RunRoot("SceneRoot", "GameObject/Create Empty", false);

        [UnityTest]
        public IEnumerator SceneRootEmptyClick() =>
            RunRoot("SceneRoot", "GameObject/Create Empty", true);

        [UnityTest]
        public IEnumerator SceneRootCubeEnter() =>
            RunRoot("SceneRoot", "GameObject/3D Object/Cube", false);

        [UnityTest]
        public IEnumerator AssetsRootCopyPathEnter() =>
            RunRoot("AssetsRoot", "Assets/Copy Path", false);

        [UnityTest]
        public IEnumerator AssetsRootCopyPathClick() =>
            RunRoot("AssetsRoot", "Assets/Copy Path", true);

        [UnityTest]
        public IEnumerator AssetsRootCreateFolderEnter() =>
            RunRoot("AssetsRoot", "Assets/Create/Folder", false);
    }
}