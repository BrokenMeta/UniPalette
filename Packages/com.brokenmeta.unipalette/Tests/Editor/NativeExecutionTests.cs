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
    public class NativeExecutionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene originalScene, temporaryScene;
        private Scene[] userScenes;
        private bool[] dirty;
        private GameObject target;
        private string folder;
        private string temporaryRoot;
        private UniPaletteWindow window;
        private int undoGroup;
        private string clipboard;
        private string rootFolderPath, rootFolderGuid;
        private UnityEngine.Object[] originalSelection;

        [SetUp]
        public void SetUp()
        {
            rootFolderPath = rootFolderGuid = null;
            clipboard = EditorGUIUtility.systemCopyBuffer;
            originalSelection = Selection.objects;
            userScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            dirty = userScenes.Select(s => s.isDirty).ToArray();
            originalScene = SceneManager.GetActiveScene();
            temporaryRoot = "Assets/UniPaletteNativeTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(temporaryRoot));
            var scenePath = temporaryRoot + "/Fixture.unity";
            File.WriteAllText(scenePath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots: []\n");
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
            temporaryScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(temporaryScene);
            target = new GameObject("UniPalette execution test");
            folder = temporaryRoot + "/Data";
            AssetDatabase.CreateFolder(temporaryRoot, "Data");
            File.WriteAllText(folder + "/Source.txt", "Native execution fixture");
            AssetDatabase.ImportAsset(folder + "/Source.txt");
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            if (window) window.Close();
            if (rootFolderPath != null)
            {
                EditorUtility.FocusProjectWindow();
                EditorWindow.focusedWindow.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                if (!string.IsNullOrEmpty(rootFolderGuid) && AssetDatabase.AssetPathToGUID(rootFolderPath) == rootFolderGuid)
                    AssetDatabase.DeleteAsset(rootFolderPath); // Only the root folder created by this test.
            }
            Undo.RevertAllDownToGroup(undoGroup);
            if (temporaryScene.IsValid()) EditorSceneManager.CloseScene(temporaryScene, true);
            if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
            if (!string.IsNullOrEmpty(temporaryRoot)) AssetDatabase.DeleteAsset(temporaryRoot);
            Selection.objects = originalSelection;
            EditorGUIUtility.systemCopyBuffer = clipboard;
            for (var i = 0; i < userScenes.Length; ++i)
                Assert.That(userScenes[i].isDirty, Is.EqualTo(dirty[i]), "User scene dirty state changed");
        }

        private PaletteState.Item Context(string kind) => kind == "GameObject"
            ? new PaletteState.Item(UnityContexts.Kind.GameObject, target)
            : new PaletteState.Item(UnityContexts.Kind.Asset, guid: AssetDatabase.AssetPathToGUID(
                kind == "Folder" ? folder : folder + (kind == "OtherAsset" ? "/Source.bytes" : "/Source.txt")));
        private static PaletteState.Item Command(string path) => new PaletteState.Item(path, path,
            Array.Empty<PaletteState.Item>(), "Test menu");

        [TestCase("GameObject", "Edit/Duplicate", true)]
        [TestCase("GameObject", "GameObject/Create Empty Parent", true)]
        [TestCase("Asset", "Edit/Duplicate", true)]
        [TestCase("Folder", "Edit/Duplicate", true)]
        [TestCase("Asset", "GameObject/Create Empty Parent", false)]
        [TestCase("Folder", "GameObject/Create Empty Parent", false)]
        [TestCase("GameObject", "GameObject/Create Empty", true)]
        [TestCase("Asset", "Assets/Delete", true)]
        [TestCase("GameObject", "GameObject/Create Empty Child", true)]
        [TestCase("GameObject", "Component/Physics/Box Collider", true)]
        [TestCase("Asset", "Assets/Copy Path", true)]
        [TestCase("Folder", "Assets/Copy Path", true)]
        [TestCase("GameObject", "Assets/Copy Path", false)]
        [TestCase("Asset", "GameObject/Create Empty Child", false)]
        [TestCase("Folder", "GameObject/Create Empty Child", false)]
        [TestCase("Asset", "Component/Physics/Box Collider", false)]
        [TestCase("Folder", "Component/Physics/Box Collider", false)]
        [TestCase("GameObject", "Component/Physics/Sphere Collider", true)]
        public void ExistingMenuFamiliesAreEligibleWithoutPerCommandWhitelist(string kind, string path, bool supported)
        {
            Assert.That(NativeExecution.Supports(path, Context(kind)), Is.EqualTo(supported));
            Assert.That(NativeExecution.Supports(path, new PaletteState.Item(UnityContexts.Kind.SceneRoot)), Is.EqualTo(path.StartsWith("GameObject/", StringComparison.Ordinal)));
            Assert.That(NativeExecution.Supports(path, new PaletteState.Item(UnityContexts.Kind.AssetsRoot)), Is.EqualTo(path.StartsWith("Assets/", StringComparison.Ordinal)));
        }

        [TestCase("GameObject")]
        [TestCase("Asset")]
        [TestCase("Folder")]
        public void PreparationRequiresExactSingleSelection(string kind)
        {
            var context = Context(kind);
            Selection.activeObject = context.Target;
            Assert.That(MenuCommands.MatchesSelection(context), Is.True);
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out _, out var error), Is.True, error);
            Selection.activeObject = kind == "GameObject" ? AssetDatabase.LoadMainAssetAtPath(folder + "/Source.txt") : target;
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection"));
            Selection.objects = new UnityEngine.Object[] { target, AssetDatabase.LoadMainAssetAtPath(folder + "/Source.txt") };
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection"));
        }

        [Test]
        public void DeletedTargetAndMenuCategoriesCannotPrepare()
        {
            var context = Context("GameObject");
            Selection.activeObject = target;
            UnityEngine.Object.DestroyImmediate(target);
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("no longer exists"));
            var category = new PaletteState.Item("Duplicate", "Edit/Duplicate", new[] { Command("Edit/Duplicate") }, "Category");
            Assert.That(NativeExecution.TryPrepare(category, Context("Asset"), out _, out _), Is.False);
            Assert.That(NativeExecution.Supports("Edit/Duplicate", new PaletteState.Item(UnityContexts.Kind.AssetsRoot)), Is.False);
        }

        [Test]
        public void SelectionChangedAfterCloseAbortsOnceAndReportsHonestly()
        {
            var context = Context("GameObject");
            Selection.activeObject = target;
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out var request, out _), Is.True);
            Selection.activeObject = null;
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch Edit/Duplicate.*Selection"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
        }

        [Test]
        public void CopyPathAcceptsNonTextAsset()
        {
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            AssetDatabase.CreateAsset(material, temporaryRoot + "/Material.mat");
            var context = new PaletteState.Item(UnityContexts.Kind.Asset,
                guid: AssetDatabase.AssetPathToGUID(temporaryRoot + "/Material.mat"));
            Selection.activeObject = material;
            Assert.That(NativeExecution.TryPrepare(Command("Assets/Copy Path"), context, out _, out var error), Is.True, error);
        }

        [Test]
        public void VirtualRootsNeverPrepareAgainstAmbientSelection()
        {
            Selection.activeObject = target;
            foreach (var kind in new[] { UnityContexts.Kind.SceneRoot, UnityContexts.Kind.AssetsRoot })
                Assert.That(NativeExecution.TryPrepare(Command(kind == UnityContexts.Kind.SceneRoot
                    ? "GameObject/Create Empty" : "Assets/Copy Path"), new PaletteState.Item(kind), out _, out _), Is.False);
            Assert.That(Selection.activeGameObject, Is.EqualTo(target));
            Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
        }

        [TestCase("GameObject", "GameObject/Create Empty Child")]
        [TestCase("GameObject", "Component/Physics/Box Collider")]
        [TestCase("Asset", "Assets/Copy Path")]
        [TestCase("Folder", "Assets/Copy Path")]
        public void NewPairsRevalidateSelectionAndConsumeRequest(string kind, string path)
        {
            var context = Context(kind);
            Selection.activeObject = context.Target;
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out var request, out var error), Is.True, error);
            Selection.objects = new UnityEngine.Object[] { target, Context("Asset").Target };
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection"));
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*Selection"));
            request.ExecuteAfterClose();
            Selection.activeObject = context.Target;
            request.ExecuteAfterClose();
            Assert.That(target.transform.childCount, Is.Zero);
            Assert.That(target.GetComponents<BoxCollider>(), Is.Empty);
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(clipboard));
        }

        [Test]
        public void DeletedTargetAfterPreparationAbortsOnce()
        {
            Selection.activeObject = target;
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), Context("GameObject"), out var request, out _), Is.True);
            UnityEngine.Object.DestroyImmediate(target);
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*no longer exists"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(temporaryScene.rootCount, Is.Zero);
        }

        [Test]
        public void DeletedAssetAfterPreparationAbortsOnce()
        {
            var context = Context("Asset");
            Selection.activeObject = context.Target;
            Assert.That(NativeExecution.TryPrepare(Command("Assets/Copy Path"), context, out var request, out _), Is.True);
            AssetDatabase.DeleteAsset(context.AssetPath); // Only this test's disposable asset.
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*no longer exists"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(clipboard));
        }

        [Test]
        public void DisabledMenuFlagIsReadInsteadOfCachedEnabledNote()
        {
            Selection.activeObject = target;
            var context = Context("GameObject");
            // A removed menu leaf can survive in an old discovery snapshot. Never dispatch it.
            const string path = "GameObject/UniPalette Missing Command 004.4";
            var staleTree = MenuCommands.Build(new[] { path }, context, true, _ => true);
            var staleCommand = staleTree.Children.Single().Children.Single();
            Assert.That(staleCommand.AvailabilityNote, Does.Contain("Menu enabled"));
            Assert.That(Menu.GetEnabled(path), Is.False);
            Assert.That(NativeExecution.TryPrepare(staleCommand, context, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("disabled"));
            Assert.That(Selection.activeGameObject, Is.EqualTo(target));
            Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
        }

        private void Key(KeyCode key, EventModifiers modifiers = EventModifiers.None)
        {
            var search = window.rootVisualElement.Q<VisualElement>("search");
            using var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key, modifiers = modifiers });
            evt.target = search; search.SendEvent(evt);
        }

        private void Click(VisualElement row, int count)
        {
            var evt = new Event { type = EventType.MouseDown, button = 0, clickCount = count, mousePosition = row.worldBound.center };
            using (var down = PointerDownEvent.GetPooled(evt)) { down.target = row; row.SendEvent(down); }
            evt.type = EventType.MouseUp;
            using var up = PointerUpEvent.GetPooled(evt); up.target = row; row.SendEvent(up);
        }

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
            var activate = typeof(UniPaletteWindow).GetMethod("ActivateResult", Private);
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
            activate.Invoke(window, new object[] { false, false });
            yield return null;
            var expected = kind == "GameObject" ? "Unity.Hierarchy.Editor.HierarchyWindow" : "UnityEditor.ProjectBrowser";
            Assert.That(EditorWindow.focusedWindow.GetType().FullName, Is.EqualTo(expected));
            if (kind == "GameObject")
            {
                if (path == "Edit/Duplicate") Assert.That(temporaryScene.rootCount, Is.EqualTo(2), "Exactly one duplicate");
                else if (path == "GameObject/Create Empty") Assert.That(temporaryScene.rootCount, Is.EqualTo(2));
                else if (path == "GameObject/Create Empty Parent")
                {
                    Assert.That(target.transform.parent, Is.Not.Null);
                    Assert.That(target.transform.parent.childCount, Is.EqualTo(1));
                    Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
                }
                else if (path == "GameObject/Create Empty Child")
                {
                    Assert.That(target.transform.childCount, Is.EqualTo(1));
                    child = target.transform.GetChild(0).gameObject;
                    Assert.That(child.scene, Is.EqualTo(temporaryScene));
                    Assert.That(Selection.activeGameObject, Is.EqualTo(child));
                    Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(target.GetComponents<BoxCollider>().Length, Is.EqualTo(1));
                    Assert.That(target.GetComponents<Component>().Length, Is.EqualTo(withOtherComponent ? 3 : 2));
                    Assert.That(Selection.activeGameObject, Is.EqualTo(target));
                    Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
                }
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
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
                else if (path == "Edit/Duplicate" || path == "GameObject/Create Empty") Assert.That(temporaryScene.rootCount, Is.EqualTo(2));
                else Assert.That(target.transform.parent, Is.Not.Null);
            }
            else if (copyPath)
            {
                Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(context.AssetPath));
                Assert.That(Selection.activeObject, Is.EqualTo(context.Target));
                Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
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
        [UnityTest] public IEnumerator ParentGameObjectClick() => Run("GameObject", "GameObject/Create Empty Parent", true);
        [UnityTest] public IEnumerator DuplicateAssetEnter() => Run("Asset", "Edit/Duplicate", false);
        [UnityTest] public IEnumerator DuplicateAssetClick() => Run("Asset", "Edit/Duplicate", true);
        [UnityTest] public IEnumerator DuplicateFolderEnter() => Run("Folder", "Edit/Duplicate", false);
        [UnityTest] public IEnumerator DuplicateFolderClick() => Run("Folder", "Edit/Duplicate", true);
        [UnityTest] public IEnumerator ChildGameObjectEnter() => Run("GameObject", "GameObject/Create Empty Child", false);
        [UnityTest] public IEnumerator ChildGameObjectClick() => Run("GameObject", "GameObject/Create Empty Child", true);
        [UnityTest] public IEnumerator BoxColliderEnter() => Run("GameObject", "Component/Physics/Box Collider", false);
        [UnityTest] public IEnumerator BoxColliderClick() => Run("GameObject", "Component/Physics/Box Collider", true);
        [UnityTest] public IEnumerator CopyAssetPathEnter() => Run("Asset", "Assets/Copy Path", false);
        [UnityTest] public IEnumerator CopyAssetPathClick() => Run("Asset", "Assets/Copy Path", true);
        [UnityTest] public IEnumerator CopyFolderPathEnter() => Run("Folder", "Assets/Copy Path", false);
        [UnityTest] public IEnumerator CopyFolderPathClick() => Run("Folder", "Assets/Copy Path", true);

        [UnityTest] public IEnumerator NewlyEligibleCreateEmptyEnter() => Run("GameObject", "GameObject/Create Empty", false);
        [UnityTest] public IEnumerator NewlyEligibleCreateEmptyClick() => Run("GameObject", "GameObject/Create Empty", true);
        [UnityTest] public IEnumerator CopyNonTxtAssetPathEnter() => Run("OtherAsset", "Assets/Copy Path", false);
        [UnityTest] public IEnumerator CopyNonTxtAssetPathClick() => Run("OtherAsset", "Assets/Copy Path", true);
        [UnityTest] public IEnumerator BoxColliderWithRigidbodyEnter() => Run("GameObject", "Component/Physics/Box Collider", false, true);
        [UnityTest] public IEnumerator BoxColliderWithRigidbodyClick() => Run("GameObject", "Component/Physics/Box Collider", true, true);

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
            state.Enter(false);
            Assert.That(window, Is.Not.Null);
            Assert.That(state.Message, Does.Contain("Selection"));
            Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
            Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo(folder + "/Source.txt"));
        }

        private IEnumerator RunRoot(UnityContexts.Kind kind, string path, bool click)
        {
            var sceneRoot = kind == UnityContexts.Kind.SceneRoot;
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
            var rootIndex = state.Results.FindIndex(i => i.Kind == kind);
            Assert.That(rootIndex, Is.GreaterThanOrEqualTo(0));
            state.Select(rootIndex);
            Key(KeyCode.Return, EventModifiers.Control);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Context.Kind, Is.EqualTo(kind));
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
            var activate = typeof(UniPaletteWindow).GetMethod("ActivateResult", Private);
            if (click)
            {
                Click(row, 2);
                Assert.That(window, Is.Not.Null);
                Click(row, 1);
            }
            else Key(KeyCode.Return);
            Assert.That(!window, Is.True, "Dispatch closes the palette");
            activate.Invoke(window, new object[] { false, false });
            yield return null;
            var expectedWindow = sceneRoot ? "Unity.Hierarchy.Editor.HierarchyWindow" : "UnityEditor.ProjectBrowser";
            Assert.That(EditorWindow.focusedWindow.GetType().FullName, Is.EqualTo(expectedWindow));
            if (sceneRoot)
            {
                Assert.That(temporaryScene.rootCount, Is.EqualTo(2), "Exactly one new root object");
                var created = temporaryScene.GetRootGameObjects().Single(go => go != target);
                Assert.That(created.transform.parent, Is.Null);
                Assert.That(created.scene, Is.EqualTo(temporaryScene));
                Assert.That(Selection.activeGameObject, Is.EqualTo(created));
                Assert.That(target.transform.childCount, Is.Zero, "Previous Selection was not used as parent");
                if (path.EndsWith("/Cube", StringComparison.Ordinal)) Assert.That(created.GetComponent<MeshFilter>(), Is.Not.Null);
                Undo.PerformUndo();
                Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
                Undo.PerformRedo();
                Assert.That(temporaryScene.rootCount, Is.EqualTo(2));
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

        [UnityTest] public IEnumerator SceneRootEmptyEnter() => RunRoot(UnityContexts.Kind.SceneRoot, "GameObject/Create Empty", false);
        [UnityTest] public IEnumerator SceneRootEmptyClick() => RunRoot(UnityContexts.Kind.SceneRoot, "GameObject/Create Empty", true);
        [UnityTest] public IEnumerator SceneRootCubeEnter() => RunRoot(UnityContexts.Kind.SceneRoot, "GameObject/3D Object/Cube", false);
        [UnityTest] public IEnumerator SceneRootCubeClick() => RunRoot(UnityContexts.Kind.SceneRoot, "GameObject/3D Object/Cube", true);
        [UnityTest] public IEnumerator AssetsRootCopyPathEnter() => RunRoot(UnityContexts.Kind.AssetsRoot, "Assets/Copy Path", false);
        [UnityTest] public IEnumerator AssetsRootCopyPathClick() => RunRoot(UnityContexts.Kind.AssetsRoot, "Assets/Copy Path", true);
        [UnityTest] public IEnumerator AssetsRootCreateFolderEnter() => RunRoot(UnityContexts.Kind.AssetsRoot, "Assets/Create/Folder", false);
        [UnityTest] public IEnumerator AssetsRootCreateFolderClick() => RunRoot(UnityContexts.Kind.AssetsRoot, "Assets/Create/Folder", true);

        [TestCase("SceneRoot", "GameObject/Create Empty")]
        [TestCase("AssetsRoot", "Assets/Copy Path")]
        public void RootRequestRejectsChangedSelectionAndNeverRepairsIt(string kindName, string path)
        {
            var kind = (UnityContexts.Kind)Enum.Parse(typeof(UnityContexts.Kind), kindName);
            var context = new PaletteState.Item(kind);
            Selection.activeObject = target;
            Assert.That(NativeExecution.TryConfirmRoot(context, out var error), Is.True, error);
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out var request, out error), Is.True, error);
            Selection.activeObject = target;
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection"));
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*Selection"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(Selection.activeGameObject, Is.EqualTo(target));
            Assert.That(temporaryScene.rootCount, Is.EqualTo(1));
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(clipboard));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneRootRequestRejectsChangedOrClosedScene(bool closeScene)
        {
            var context = new PaletteState.Item(UnityContexts.Kind.SceneRoot);
            Assert.That(NativeExecution.TryConfirmRoot(context, out _), Is.True);
            Assert.That(NativeExecution.TryPrepare(Command("GameObject/Create Empty"), context, out var request, out _), Is.True);
            if (closeScene) EditorSceneManager.CloseScene(temporaryScene, true);
            else SceneManager.SetActiveScene(originalScene);
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*active scene changed"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(Selection.count, Is.Zero);
            Assert.That(originalScene.isDirty, Is.EqualTo(dirty[Array.IndexOf(userScenes, originalScene)]));
        }

        [TestCase("SceneRoot", "GameObject/UniPalette Missing Command 004.5")]
        [TestCase("AssetsRoot", "Assets/UniPalette Missing Command 004.5")]
        public void RootPreflightRejectsDisabledMenuAndCategories(string kindName, string path)
        {
            var kind = (UnityContexts.Kind)Enum.Parse(typeof(UnityContexts.Kind), kindName);
            var context = new PaletteState.Item(kind);
            Assert.That(NativeExecution.TryConfirmRoot(context, out _), Is.True);
            Assert.That(Menu.GetEnabled(path), Is.False);
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("disabled"));
            var category = new PaletteState.Item("Category", path, new[] { Command(path + "/Child") }, "Menu category");
            Assert.That(NativeExecution.TryPrepare(category, context, out _, out _), Is.False);
        }
    }
}
