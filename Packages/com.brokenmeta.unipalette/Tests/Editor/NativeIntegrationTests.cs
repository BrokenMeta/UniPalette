using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BrokenMeta.UniPalette.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            if (withOtherComponent) target.AddComponent<Rigidbody>();
            if (kind == "OtherAsset")
            {
                File.WriteAllBytes(folder + "/Source.bytes", new byte[] { 1, 2, 3 });
                AssetDatabase.ImportAsset(folder + "/Source.bytes", ImportAssetOptions.ForceSynchronousImport);
            }
            var context = Context(kind);
            var copyPath = path == "Assets/Copy Path";
            var filesBefore = Directory.GetFiles(temporaryRoot, "*", SearchOption.AllDirectories).OrderBy(f => f)
                .Select(f => f + ":" + Convert.ToBase64String(File.ReadAllBytes(f))).ToArray();
            if (copyPath) EditorGUIUtility.systemCopyBuffer = "UniPalette clipboard sentinel";
            GameObject child = null;
            Selection.activeObject = context.Target;
            window = EditorWindow.GetWindow<UniPaletteWindow>();
            typeof(UniPaletteWindow).GetMethod("CreateGUI", Private).Invoke(window, null);
            window.Show(); window.Focus();
            var state = (PaletteState)typeof(UniPaletteWindow).GetField("state", Private).GetValue(window);
            state.Select(state.Results.FindIndex(i => i.SelectionEntry));
            Key(KeyCode.Return);
            var deadline = EditorApplication.timeSinceStartup + 5;
            while (state.CommandsLoading && EditorApplication.timeSinceStartup < deadline) yield return null;
            window.rootVisualElement.Q<VisualElement>("search").Q<TextField>().value = path;
            var index = state.Results.FindIndex(i => i.Path == path);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Supported command must be discoverable");
            state.Select(index);
            var list = window.rootVisualElement.Q<ListView>("results");
            list.SetSelectionWithoutNotify(new[] { index });
            yield return null;
            Assert.That(MenuCommands.MatchesSelection(context), Is.True);
            var renderedRow = list.GetRootElementForIndex(index);
            Assert.That(renderedRow, Is.Not.Null);
            Assert.That(renderedRow.tooltip, Does.Contain("Native execution").And.Not.Contain("Unsupported"));
            var activate = typeof(UniPaletteWindow).GetMethod("ActivateSelected", Private);
            if (click)
            {
                var row = list.GetRootElementForIndex(index);
                Assert.That(row, Is.Not.Null);
                Click(row, 2);
                Assert.That(window, Is.Not.Null, "Second click alone must not execute");
                Click(row, 1);
            }
            else Key(KeyCode.Return);
            Assert.That(!window, Is.True, "Native dispatch closes the palette");
            // A stale UI callback cannot dispatch a second time after OnDisable.
            activate.Invoke(window, null);
            var expected = kind == "GameObject" ? "Unity.Hierarchy.Editor.HierarchyWindow" : "UnityEditor.ProjectBrowser";
            // Focus is a dispatch precondition, not a promise about Unity's next frame.
            Assert.That(EditorWindow.focusedWindow, Is.Not.Null);
            Assert.That(EditorWindow.focusedWindow.GetType().FullName, Is.EqualTo(expected));
            yield return null;
            if (kind == "GameObject")
            {
                if (path == "Edit/Duplicate") Assert.That(testScene.rootCount, Is.EqualTo(2), "Exactly one duplicate");
                else if (path == "GameObject/Create Empty") Assert.That(testScene.rootCount, Is.EqualTo(2));
                else if (path == "GameObject/Create Empty Parent")
                {
                    Assert.That(target.transform.parent, Is.Not.Null);
                    Assert.That(target.transform.parent.childCount, Is.EqualTo(1));
                    Assert.That(testScene.rootCount, Is.EqualTo(1));
                }
                else if (path == "GameObject/Create Empty Child")
                {
                    Assert.That(target.transform.childCount, Is.EqualTo(1));
                    child = target.transform.GetChild(0).gameObject;
                    Assert.That(child.scene, Is.EqualTo(testScene));
                    Assert.That(Selection.activeGameObject, Is.EqualTo(child));
                    Assert.That(testScene.rootCount, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(target.GetComponents<BoxCollider>().Length, Is.EqualTo(1));
                    Assert.That(target.GetComponents<Component>().Length, Is.EqualTo(withOtherComponent ? 3 : 2));
                    Assert.That(Selection.activeGameObject, Is.EqualTo(target));
                    Assert.That(testScene.rootCount, Is.EqualTo(1));
                }
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(testScene.rootCount, Is.EqualTo(1));
                Assert.That(target.transform.parent, Is.Null, "GameObject Undo restores original hierarchy");
                Assert.That(target.transform.childCount, Is.Zero);
                Assert.That(target.GetComponents<BoxCollider>(), Is.Empty);
                Undo.PerformRedo();
                if (path == "GameObject/Create Empty Child")
                {
                    Assert.That(target.transform.childCount, Is.EqualTo(1));
                    Assert.That(target.transform.GetChild(0).gameObject, Is.EqualTo(child));
                }
                else if (path == "Component/Physics/Box Collider")
                    Assert.That(target.GetComponents<BoxCollider>().Length, Is.EqualTo(1));
                else if (path == "Edit/Duplicate" || path == "GameObject/Create Empty") Assert.That(testScene.rootCount, Is.EqualTo(2));
                else Assert.That(target.transform.parent, Is.Not.Null);
            }
            else if (copyPath)
            {
                Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(AssetDatabase.GetAssetPath(context.Target)));
                Assert.That(Selection.activeObject, Is.EqualTo(context.Target));
                Assert.That(testScene.rootCount, Is.EqualTo(1));
                var filesAfter = Directory.GetFiles(temporaryRoot, "*", SearchOption.AllDirectories).OrderBy(f => f)
                    .Select(f => f + ":" + Convert.ToBase64String(File.ReadAllBytes(f))).ToArray();
                Assert.That(filesAfter, Is.EqualTo(filesBefore), "Clipboard command must not create or change files, including meta files");
            }
            else if (kind == "Asset")
            {
                var files = Directory.GetFiles(folder, "*.txt");
                Assert.That(files.Length, Is.EqualTo(2), "Exactly one asset duplicate");
                Assert.That(files.All(f => File.ReadAllText(f) == "Native execution fixture"), Is.True);
            }
            else
            {
                var copy = folder + " 1";
                try
                {
                    Assert.That(AssetDatabase.IsValidFolder(copy), Is.True, "Folder duplicate exists");
                    Assert.That(File.ReadAllText(copy + "/Source.txt"), Is.EqualTo("Native execution fixture"));
                    Assert.That(AssetDatabase.IsValidFolder(folder + " 2"), Is.False, "No second duplicate");
                }
                finally { AssetDatabase.DeleteAsset(copy); }
            }
            EditorApplication.ExecuteMenuItem("Window/UniPalette");
            window = EditorWindow.GetWindow<UniPaletteWindow>();
            yield return null;
            var reopened = (PaletteState)typeof(UniPaletteWindow).GetField("state", Private).GetValue(window);
            Assert.That(reopened.Mode, Is.EqualTo(PaletteState.Browser.Context));
            Assert.That(reopened.Context, Is.Null, "Reopened window has a new navigation lifecycle");
        }

        [UnityTest] public IEnumerator DuplicateGameObjectEnter() => Run("GameObject", "Edit/Duplicate", false);

        [UnityTest] public IEnumerator DuplicateGameObjectClick() => Run("GameObject", "Edit/Duplicate", true);

        [UnityTest] public IEnumerator ParentGameObjectEnter() => Run("GameObject", "GameObject/Create Empty Parent", false);

        [UnityTest] public IEnumerator DuplicateAssetEnter() => Run("Asset", "Edit/Duplicate", false);

        [UnityTest] public IEnumerator DuplicateFolderEnter() => Run("Folder", "Edit/Duplicate", false);

        [UnityTest] public IEnumerator ChildGameObjectEnter() => Run("GameObject", "GameObject/Create Empty Child", false);

        [UnityTest] public IEnumerator BoxColliderEnter() => Run("GameObject", "Component/Physics/Box Collider", false);

        [UnityTest] public IEnumerator CopyAssetPathEnter() => Run("Asset", "Assets/Copy Path", false);

        [UnityTest] public IEnumerator CopyAssetPathClick() => Run("Asset", "Assets/Copy Path", true);

        [UnityTest] public IEnumerator CopyFolderPathEnter() => Run("Folder", "Assets/Copy Path", false);

        [UnityTest] public IEnumerator CreateEmptyEnter() => Run("GameObject", "GameObject/Create Empty", false);

        [UnityTest] public IEnumerator CopyNonTxtAssetPathEnter() => Run("OtherAsset", "Assets/Copy Path", false);

        [UnityTest] public IEnumerator BoxColliderWithRigidbodyEnter() => Run("GameObject", "Component/Physics/Box Collider", false, true);

        [UnityTest]
        public IEnumerator PreCloseFailureKeepsPaletteAndSelection()
        {
            Selection.activeObject = target;
            window = EditorWindow.GetWindow<UniPaletteWindow>();
            typeof(UniPaletteWindow).GetMethod("CreateGUI", Private).Invoke(window, null);
            window.Show(); window.Focus();
            var state = (PaletteState)typeof(UniPaletteWindow).GetField("state", Private).GetValue(window);
            state.Select(state.Results.FindIndex(i => i.SelectionEntry));
            Key(KeyCode.Return);
            var deadline = EditorApplication.timeSinceStartup + 5;
            while (state.CommandsLoading && EditorApplication.timeSinceStartup < deadline) yield return null;
            state.SetQuery("Edit/Duplicate");
            state.Select(state.Results.FindIndex(i => i.Path == "Edit/Duplicate"));
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(folder + "/Source.txt");
            // Direct state activation preserves the stale row to exercise the final preflight check.
            state.ActivateSelected();
            Assert.That(window, Is.Not.Null);
            Assert.That(state.Message, Does.Contain("Selection"));
            Assert.That(testScene.rootCount, Is.EqualTo(1));
            Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo(folder + "/Source.txt"));
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
                for (int i = 0; i < 4; i++) yield return null;
            }
            Selection.activeObject = sceneRoot ? target : Context("Asset").Target;
            window = EditorWindow.GetWindow<UniPaletteWindow>();
            typeof(UniPaletteWindow).GetMethod("CreateGUI", Private).Invoke(window, null);
            window.Show(); window.Focus();
            var state = (PaletteState)typeof(UniPaletteWindow).GetField("state", Private).GetValue(window);
            var rootIndex = state.Results.FindIndex(i => i.GetType() == CreateRoot(kind).GetType());
            Assert.That(rootIndex, Is.GreaterThanOrEqualTo(0));
            state.Select(rootIndex);
            Key(KeyCode.Return, EventModifiers.Control);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Context.GetType(), Is.EqualTo(CreateRoot(kind).GetType()));
            Assert.That(state.Context.Target, Is.Null);
            Assert.That(MenuCommands.MatchesSelection(state.Context), Is.True);
            if (sceneRoot) Assert.That(Selection.count, Is.Zero);
            else Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo("Assets"));
            var deadline = EditorApplication.timeSinceStartup + 5;
            while (state.CommandsLoading && EditorApplication.timeSinceStartup < deadline) yield return null;
            window.rootVisualElement.Q<VisualElement>("search").Q<TextField>().value = path;
            var index = state.Results.FindIndex(i => i.Path == path);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Root command must be discovered");
            state.Select(index);
            var list = window.rootVisualElement.Q<ListView>("results");
            list.SetSelectionWithoutNotify(new[] { index });
            yield return null;
            var row = list.GetRootElementForIndex(index);
            Assert.That(row, Is.Not.Null);
            Assert.That(row.tooltip, Does.Contain("Native execution"));
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Key(KeyCode.Return, EventModifiers.Control);
            Assert.That(window, Is.Not.Null, "Alt+Right and Ctrl+Enter do not dispatch a leaf");
            Assert.That(MenuCommands.MatchesSelection(state.Context), Is.True);
            if (createFolder)
            {
                rootFolderPath = AssetDatabase.GenerateUniqueAssetPath("Assets/New Folder");
                Assert.That(Directory.Exists(rootFolderPath), Is.False);
            }
            else if (!sceneRoot) EditorGUIUtility.systemCopyBuffer = "root clipboard sentinel";
            var activate = typeof(UniPaletteWindow).GetMethod("ActivateSelected", Private);
            if (click)
            {
                Click(row, 2);
                Assert.That(window, Is.Not.Null);
                Click(row, 1);
            }
            else Key(KeyCode.Return);
            Assert.That(!window, Is.True, "Dispatch closes the palette");
            activate.Invoke(window, null);
            yield return null;
            var expectedWindow = sceneRoot ? "Unity.Hierarchy.Editor.HierarchyWindow" : "UnityEditor.ProjectBrowser";
            Assert.That(EditorWindow.focusedWindow.GetType().FullName, Is.EqualTo(expectedWindow));
            if (sceneRoot)
            {
                Assert.That(testScene.rootCount, Is.EqualTo(2), "Exactly one new root object");
                var created = testScene.GetRootGameObjects().Single(go => go != target);
                Assert.That(created.transform.parent, Is.Null);
                Assert.That(created.scene, Is.EqualTo(testScene));
                Assert.That(Selection.activeGameObject, Is.EqualTo(created));
                Assert.That(target.transform.childCount, Is.Zero, "Previous Selection was not used as parent");
                if (path.EndsWith("/Cube", StringComparison.Ordinal)) Assert.That(created.GetComponent<MeshFilter>(), Is.Not.Null);
                Undo.PerformUndo();
                Assert.That(testScene.rootCount, Is.EqualTo(1));
                Undo.PerformRedo();
                Assert.That(testScene.rootCount, Is.EqualTo(2));
            }
            else if (createFolder)
            {
                Assert.That(AssetDatabase.IsValidFolder(rootFolderPath), Is.False, "Unity is still editing the name after dispatch");
                var project = EditorWindow.focusedWindow;
                project.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
                project.SendEvent(new Event { type = EventType.KeyUp, keyCode = KeyCode.Return });
                for (int i = 0; i < 8; i++) yield return null;
                if (AssetDatabase.IsValidFolder(rootFolderPath)) rootFolderGuid = AssetDatabase.AssetPathToGUID(rootFolderPath);
                Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo(rootFolderPath));
                Assert.That(rootFolderGuid, Is.Not.Null.And.Not.Empty);
                Assert.That(AssetDatabase.GetSubFolders("Assets").Except(foldersBefore), Is.EqualTo(new[] { rootFolderPath }), "Exactly one folder in Assets, not the previous Project folder");
                Assert.That(AssetDatabase.GetSubFolders(folder), Is.Empty);
                Assert.That(File.ReadAllText(folder + "/Source.txt"), Is.EqualTo("Native execution fixture"));
            }
            else
            {
                Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo("Assets"));
                Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo("Assets"));
                Assert.That(AssetDatabase.GetSubFolders("Assets"), Is.EqualTo(foldersBefore));
            }
            Assert.That(target.transform.childCount, Is.Zero);
        }

        [UnityTest] public IEnumerator SceneRootEmptyEnter() => RunRoot("SceneRoot", "GameObject/Create Empty", false);

        [UnityTest] public IEnumerator SceneRootEmptyClick() => RunRoot("SceneRoot", "GameObject/Create Empty", true);

        [UnityTest] public IEnumerator SceneRootCubeEnter() => RunRoot("SceneRoot", "GameObject/3D Object/Cube", false);

        [UnityTest] public IEnumerator AssetsRootCopyPathEnter() => RunRoot("AssetsRoot", "Assets/Copy Path", false);

        [UnityTest] public IEnumerator AssetsRootCopyPathClick() => RunRoot("AssetsRoot", "Assets/Copy Path", true);

        [UnityTest] public IEnumerator AssetsRootCreateFolderEnter() => RunRoot("AssetsRoot", "Assets/Create/Folder", false);
    }
}
