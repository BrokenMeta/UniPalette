using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Tests
{
    public class UnityContextsTests
    {
        private Scene scene;
        private Scene previousScene;
        private Object[] previousSelection;
        private string folder;
        private string temporaryRoot;
        private UniPaletteWindow testWindow;

        private SharedTestScene sharedScene;

        [OneTimeSetUp]
        public void OpenSharedScene() => sharedScene = new SharedTestScene();

        [OneTimeTearDown]
        public void CloseSharedScene() => sharedScene?.Dispose();

        [SetUp]
        public void SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            previousSelection = Selection.objects;
            temporaryRoot = "Assets/UniPaletteTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(temporaryRoot));
            scene = sharedScene.Use();
            AssetDatabase.CreateFolder(temporaryRoot, "Data");
            folder = temporaryRoot + "/Data";
            Selection.objects = Array.Empty<Object>();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = previousSelection;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            sharedScene.ClearObjects();
            if (!string.IsNullOrEmpty(temporaryRoot)) AssetDatabase.DeleteAsset(temporaryRoot);
            temporaryRoot = null;
            folder = null;
            if (testWindow)
                typeof(UniPaletteWindow).GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(testWindow, null);
            testWindow = null;
        }

        private GameObject Go(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            if (parent) go.transform.SetParent(parent.transform);
            return go;
        }

        private static void Choose(PaletteState state, string title)
        {
            var index = state.Results.FindIndex(item => item.Title == title);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), title);
            state.Select(index);
        }

        private void CreateAssets()
        {
            AssetDatabase.CreateFolder(folder, "A");
            AssetDatabase.CreateFolder(folder, "AB");
            AssetDatabase.CreateFolder(folder + "/A", "Nested");
            WriteText(folder + "/A/Nested/Needle.txt");
            WriteText(folder + "/AB/Needle.txt");
        }

        private static void WriteText(string path)
        {
            File.WriteAllText(path, "UniPalette temporary test asset");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        [UnityTest]
        public IEnumerator WindowTracksGameObjectAssetFolderAndEmptySelection()
        {
            CreateAssets();
            var go = Go("SelectionSequence");
            var window = EditorWindow.GetWindow<UniPaletteWindow>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(UniPaletteWindow).GetMethod("CreateGUI", flags).Invoke(window, null);
            PaletteTestData.Install(window, new PaletteState(commandRoot: PaletteTestData.Commands()));
            testWindow = window;
            window.Show();
            window.Focus();
            var state = (PaletteState)typeof(UniPaletteWindow).GetField("state", flags).GetValue(window);
            var targets = new Object[] {
                go,
                AssetDatabase.LoadMainAssetAtPath(folder + "/A/Nested/Needle.txt"),
                AssetDatabase.LoadMainAssetAtPath(folder + "/A"),
                AssetDatabase.LoadMainAssetAtPath("Packages/com.brokenmeta.unipalette/Editor/UniPaletteWindow.cs"),
                AssetDatabase.LoadMainAssetAtPath("Packages/com.brokenmeta.unipalette/Editor")
            };
            foreach (var target in targets)
            {
                Assert.That(target, Is.Not.Null);
                Selection.activeObject = target;
                var deadline = EditorApplication.timeSinceStartup + 1;
                while (state.Results.SingleOrDefault(item => item.SelectionEntry)?.Target != target
                       && EditorApplication.timeSinceStartup < deadline) yield return null;
                var selection = state.Results.Single(item => item.SelectionEntry);
                Assert.That(selection.Target, Is.EqualTo(target));
                Assert.That(selection.IsValid, Is.True, selection.Path);
                Assert.That(state.SelectedIndex, Is.EqualTo(-1));
                var search = window.rootVisualElement.Q<VisualElement>("search");
                foreach (var key in new[] { KeyCode.DownArrow, KeyCode.RightArrow })
                {
                    using var evt = KeyDownEvent.GetPooled('\0', key,
                        key == KeyCode.RightArrow ? EventModifiers.Alt : EventModifiers.None);
                    evt.target = search;
                    search.SendEvent(evt);
                }
                Assert.That(state.Context.Target, Is.EqualTo(target), "Alt Right uses the latest selection context");
                Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
                Assert.That(state.Query, Is.Empty);
                Assert.That(state.SelectedIndex, Is.Zero);
                using var back = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None);
                back.target = search;
                search.SendEvent(back);
                Assert.That(state.Context, Is.Null);
            }
            Selection.objects = Array.Empty<Object>();
            var end = EditorApplication.timeSinceStartup + 1;
            while (state.Results.Any(item => item.SelectionEntry) && EditorApplication.timeSinceStartup < end) yield return null;
            Assert.That(state.Results.Any(item => item.SelectionEntry), Is.False);
            Assert.That(state.Results.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator WindowTracksSelectionChangesWithoutManualRefresh()
        {
            var target = Go("NotificationTarget");
            var window = EditorWindow.GetWindow<UniPaletteWindow>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(UniPaletteWindow).GetMethod("CreateGUI", flags).Invoke(window, null);
            PaletteTestData.Install(window, new PaletteState(commandRoot: PaletteTestData.Commands()));
            testWindow = window;
            window.Show();
            var notifications = 0;
            Action observed = () => ++notifications;
            Selection.selectionChanged += observed;
            try
            {
                Selection.activeObject = target;
                var state = (PaletteState)typeof(UniPaletteWindow).GetField("state", flags).GetValue(window);
                var deadline = EditorApplication.timeSinceStartup + 1;
                while (!state.Results.Any(item => item.SelectionEntry && item.Target == target)
                       && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(state.Results.SingleOrDefault(item => item.SelectionEntry)?.Target, Is.EqualTo(target),
                    "Selection notifications: " + notifications + "; actual Selection: " + Selection.activeObject);
                Selection.objects = Array.Empty<Object>();
                deadline = EditorApplication.timeSinceStartup + 1;
                while (state.Results.Any(item => item.SelectionEntry)
                       && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(state.Results.Any(item => item.SelectionEntry), Is.False, "Selection notifications: " + notifications);
                Assert.That(state.SelectedIndex, Is.EqualTo(-1));
                Selection.activeObject = target;
                deadline = EditorApplication.timeSinceStartup + 1;
                while (!state.Results.Any(item => item.SelectionEntry)
                       && EditorApplication.timeSinceStartup < deadline) yield return null;
                var search = window.rootVisualElement.Q<VisualElement>("search");
                foreach (var key in new[] { KeyCode.DownArrow, KeyCode.Return })
                {
                    using var evt = KeyDownEvent.GetPooled('\0', key, EventModifiers.None);
                    evt.target = search;
                    search.SendEvent(evt);
                }
                Assert.That(state.Context.Target, Is.EqualTo(target));
                Object.DestroyImmediate(target);
                deadline = EditorApplication.timeSinceStartup + 1;
                while (state.Context != null && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(state.Context, Is.Null, "Deleted command context returns to root automatically");
                Assert.That(state.Query, Is.Empty);
                Assert.That(state.SelectedIndex, Is.EqualTo(-1));
                }
            finally { Selection.selectionChanged -= observed; }
        }

        [Test]
        public void BackRestoresGameObjectIdentityAndNearestVisibleAncestor()
        {
            var parent = Go("Parent");
            var otherParent = Go("Parent");
            var leaf = Go("Needle", Go("Branch", parent));
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Hierarchy"); state.Enter(false);
            state.SetQuery("Needle"); state.MoveSelection(1); state.Enter(true);
            leaf.transform.SetParent(otherParent.transform);
            leaf.name = "Renamed";
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Target, Is.EqualTo(otherParent), "Current ancestry, not an old path or duplicate name");
            state.Enter(false);
            Assert.That(state.SelectedIndex, Is.Zero);
            state.Select(0); state.Enter(true);
            leaf.name = "RenamedAgain";
            state.Back();
            Assert.That(state.Results[state.SelectedIndex].Target, Is.EqualTo(leaf), "Visible object restored by identity after rename");
        }

        [Test]
        public void DeletedEntryLeavesNoSelectionEvenWhenItsAncestorIsVisible()
        {
            var parent = Go("Parent");
            var leaf = Go("Needle", parent);
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Hierarchy"); state.Enter(false);
            state.SetQuery("Needle"); state.MoveSelection(1); state.Enter(true);
            Object.DestroyImmediate(leaf);
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results.Any(item => item.Target == parent), Is.True);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void BackRestoresAssetByGuidAndVisibleAncestorAfterMove()
        {
            CreateAssets();
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Project"); state.Enter(false);
            state.SetQuery(folder);
            Choose(state, "Data"); state.Enter(false);
            state.SetQuery(folder + "/A/Nested/Needle.txt"); state.MoveSelection(1); state.Enter(true);
            Assert.That(AssetDatabase.MoveAsset(folder + "/A/Nested/Needle.txt", folder + "/AB/Moved.txt"), Is.Empty);
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo(folder + "/AB"));
            state.Enter(false);
            Choose(state, "Moved.txt"); state.Enter(true);
            Assert.That(AssetDatabase.MoveAsset(folder + "/AB/Moved.txt", folder + "/AB/Renamed.txt"), Is.Empty);
            state.Back();
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo(folder + "/AB/Renamed.txt"));
        }

        [Test]
        public void BackRestoresCurrentSelectionOnlyForTheSameObject()
        {
            var first = Go("First");
            var second = Go("Second");
            Selection.activeObject = first;
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            state.Select(0); state.Enter(false); state.Back();
            Assert.That(state.SelectedIndex, Is.Zero, "Previously explicit first row is restored");
            state.Enter(false);
            Selection.activeObject = second;
            state.Back();
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            Assert.That(state.Results.Single(item => item.SelectionEntry).Target, Is.EqualTo(second));
        }

        [Test]
        public void HierarchySearchIsRecursiveScopedAndIncludesInactiveObjects()
        {
            var parent = Go("TestParent");
            var branch = Go("Branch", parent);
            var child = Go("Needle", branch);
            child.SetActive(false);
            Go("Needle", Go("OtherBranch"));
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Hierarchy");
            state.Enter(false);
            Assert.That(state.Results.Any(item => item.Target == parent), Is.True);
            Assert.That(state.Results.Any(item => item.Target == child), Is.False, "Empty query shows immediate children");
            Choose(state, "TestParent");
            state.Enter(false);
            state.SetQuery("Needle");
            Assert.That(state.Results.Select(item => item.Target), Is.EqualTo(new[] { child }));
            state.MoveSelection(1);
            Assert.That(state.Enter(false), Is.True, "Smart Enter opens leaf commands");
            Assert.That(state.Context.Target, Is.EqualTo(child));
            Assert.That(Selection.objects, Is.EqualTo(new Object[] { child }), "Confirming the leaf adopts Unity Selection");
        }

        [Test]
        public void DuplicateNamesHaveDifferentPathsEvenWithSameParent()
        {
            var parent = Go("Parent");
            var a = Go("Needle", parent);
            var b = Go("Needle", parent);
            var items = UnityContexts.Children(new SceneObjectContext(parent), true).ToArray();
            Assert.That(items.Select(item => item.Target), Is.EqualTo(new[] { a, b }));
            Assert.That(items.Select(item => item.Path).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void SelectionEntryTracksChangeAndDeletionWithoutRetargetingOpenCommands()
        {
            var first = Go("First");
            var second = Go("Second");
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Assert.That(state.Results.Any(item => item.SelectionEntry), Is.False);
            Selection.activeObject = first;
            state.Refresh();
            Assert.That(state.Results.Single(item => item.SelectionEntry).Target, Is.EqualTo(first));
            state.SetQuery("Current Selection");
            Assert.That(state.Results.Single().Target, Is.EqualTo(first));
            state.SetQuery("");
            state.Select(0);
            state.Enter(false);
            Selection.activeObject = second;
            state.Refresh();
            Assert.That(state.Context.Target, Is.EqualTo(first), "Open command context is stable");
            state.Back();
            Assert.That(state.Results.Single(item => item.SelectionEntry).Target, Is.EqualTo(second));
            Object.DestroyImmediate(second);
            state.Refresh();
            Assert.That(state.Results.Any(item => item.SelectionEntry), Is.False);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void DeletedContextsAndHistoryAreSkippedOnRefreshAndBack()
        {
            var parent = Go("Parent");
            var branch = Go("Branch", parent);
            Go("Leaf", branch);
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Hierarchy"); state.Enter(false);
            Choose(state, "Parent"); state.Enter(false);
            Choose(state, "Branch"); state.Enter(false);
            state.Enter(true);
            state.Select(1); state.Enter(false); // Experimental category.
            state.SetQuery("Cube");
            Object.DestroyImmediate(branch);
            state.Refresh();
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Context));
            Assert.That(state.Context.Target, Is.EqualTo(parent));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            parent.name = "Renamed";
            state.Refresh();
            Assert.That(state.Context.Title, Is.EqualTo("Renamed"));
            Assert.That(state.Context.Path, Does.Contain("Renamed"));
            Object.DestroyImmediate(parent);
            state.Back();
            Assert.That(state.Context, Is.TypeOf<SceneRootContext>());
        }

        [Test]
        public void ProjectSearchRespectsFolderBoundaryAndImmediateChildren()
        {
            CreateAssets();
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Project"); state.Enter(false);
            Choose(state, Path.GetFileName(temporaryRoot)); state.Enter(false);
            Choose(state, "Data"); state.Enter(false);
            Assert.That(state.Results.Select(item => item.Title), Is.EquivalentTo(new[] { "A", "AB" }));
            Choose(state, "A"); state.Enter(false);
            Assert.That(state.Results.Select(item => item.Title), Is.EqualTo(new[] { "Nested" }));
            state.SetQuery("Needle");
            Assert.That(state.Results.Select(item => item.Path), Is.EqualTo(new[] { folder + "/A/Nested/Needle.txt" }));
            state.MoveSelection(1);
            Assert.That(state.Enter(false), Is.True);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Context.Target, Is.InstanceOf<TextAsset>());
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Nested"));
            state.SetQuery("not found"); // Clear the restored row before testing commands for the current folder.
            state.Enter(true);
            Assert.That(state.Context.Path, Is.EqualTo(folder + "/A"), "Ctrl Enter without result uses folder");
        }

        [Test]
        public void AssetSelectionFolderMoveAndDeletionRefreshByGuid()
        {
            CreateAssets();
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(folder + "/A");
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Assert.That(state.Results.Single(item => item.SelectionEntry).Path, Is.EqualTo(folder + "/A"));
            state.Select(0); state.Enter(false);
            Assert.That(AssetDatabase.MoveAsset(folder + "/A", folder + "/Moved"), Is.Empty);
            state.Refresh();
            Assert.That(state.Context.Path, Is.EqualTo(folder + "/Moved"));
            state.Back();
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(folder + "/Moved/Nested/Needle.txt");
            state.Refresh();
            Assert.That(state.Results.Single(item => item.SelectionEntry).Target, Is.InstanceOf<TextAsset>());
            state.Select(0); state.Enter(true);
            AssetDatabase.DeleteAsset(folder + "/Moved/Nested/Needle.txt");
            state.Refresh();
            Assert.That(state.Context, Is.Null);
            Assert.That(state.Results.Any(item => item.SelectionEntry), Is.False);
        }

        [TestCase("GameObject", false)]
        [TestCase("GameObject", true)]
        [TestCase("Asset", false)]
        [TestCase("Asset", true)]
        [TestCase("EmptyFolder", false)]
        [TestCase("EmptyFolder", true)]
        [TestCase("Parent", false)]
        [TestCase("Parent", true)]
        [TestCase("Folder", false)]
        [TestCase("Folder", true)]
        public void SmartEnterUsesActualChildrenAndRestoresEntry(string kind, bool altRight)
        {
            CreateAssets();
            AssetDatabase.CreateFolder(folder, "Empty");
            var parent = Go("Parent");
            var leaf = Go("Leaf", parent);
            var target = kind == "Parent" ? (Object)parent : kind == "GameObject" ? leaf
                : AssetDatabase.LoadMainAssetAtPath(kind == "Asset" ? folder + "/A/Nested/Needle.txt"
                    : kind == "EmptyFolder" ? folder + "/Empty" : folder + "/A");
            var item = target is GameObject go ? (PaletteItem)new SceneObjectContext(go)
                : new AssetContext(guid: AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target)));
            using var state = new PaletteState(new[] { item }, PaletteTestData.Commands());
            state.SetQuery(item.Title);
            state.Select(0);
            Assert.That(state.Enter(false, browseOnly: altRight), Is.True);
            var container = kind == "Parent" || kind == "Folder";
            Assert.That(state.Mode, Is.EqualTo(container ? PaletteState.Browser.Context : PaletteState.Browser.Commands));
            Assert.That(state.Context.Target, Is.EqualTo(target));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.Zero);
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Target, Is.EqualTo(target));
            Assert.That(state.Enter(true), Is.True, "Ctrl Enter always opens commands, including containers");
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Context.Target, Is.EqualTo(target));
            Assert.That(Selection.objects, Is.EqualTo(new[] { target }));
        }

        [TestCase("GameObject")]
        [TestCase("Asset")]
        [TestCase("Folder")]
        public void ConfirmationAdoptsTargetAndBackAndCategoriesDoNotRestoreSelection(string kind)
        {
            CreateAssets();
            var original = Go("Original");
            var target = kind == "GameObject" ? (Object)Go("Target")
                : AssetDatabase.LoadMainAssetAtPath(kind == "Asset" ? folder + "/A/Nested/Needle.txt" : folder + "/A");
            var item = target is GameObject go ? (PaletteItem)new SceneObjectContext(go)
                : new AssetContext(guid: AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target)));
            Selection.activeObject = original;
            using var state = new PaletteState(new[] { item }, PaletteTestData.Commands());
            state.MoveSelection(1);
            state.SetQuery(item.Title);
            Assert.That(Selection.activeObject, Is.EqualTo(original), "Browsing/query/selection are passive");
            state.Select(0); state.Enter(true);
            Assert.That(Selection.objects, Is.EqualTo(new[] { target }));
            state.Select(1); state.Enter(false); // Command category.
            Assert.That(Selection.activeObject, Is.EqualTo(target));
            state.Back(); state.Back();
            Assert.That(Selection.activeObject, Is.EqualTo(target), "Back restores palette row, not global Selection");
            Assert.That(state.Results[state.SelectedIndex].Target, Is.EqualTo(target));
        }

        [Test]
        public void VirtualRootsNeverAdoptAmbientSelectionAsTheirTarget()
        {
            var original = Go("Original");
            Selection.activeObject = original;
            using var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            foreach (var title in new[] { "Hierarchy", "Project" })
            {
                Selection.activeObject = original;
                Choose(state, title); state.Enter(false);
                Assert.That(Selection.activeObject, Is.EqualTo(original), "Browsing a root is passive");
                state.Back();
                Choose(state, title); state.Enter(true);
                Assert.That(state.Context.Target, Is.Null, "A virtual root does not adopt the previous object");
                Assert.That(MenuCommands.MatchesSelection(state.Context), Is.True);
                if (title == "Hierarchy") Assert.That(Selection.count, Is.Zero);
                else Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo("Assets"));
                var prepared = Selection.objects;
                state.Back();
                Assert.That(Selection.objects, Is.EqualTo(prepared), "Back does not restore ambient Selection");
            }
        }

        [UnityTest]
        public IEnumerator WindowConfirmationIgnoresOwnDelayedSelectionNotification()
        {
            // Give fixture notifications time to settle before testing Selection notifications.
            // Unity 6.6 may delay or omit hierarchyChanged under Pipeline; its delivery is not this test's contract.
            var hierarchyReady = false;
            void OnHierarchyReady() => hierarchyReady = true;
            EditorApplication.hierarchyChanged += OnHierarchyReady;
            var original = Go("Original");
            var target = Go("Target");
            try
            {
                var deadline = EditorApplication.timeSinceStartup + 2;
                while (!hierarchyReady && EditorApplication.timeSinceStartup < deadline) yield return null;
            }
            finally { EditorApplication.hierarchyChanged -= OnHierarchyReady; }
            Selection.activeObject = original;
            var window = EditorWindow.GetWindow<UniPaletteWindow>();
            testWindow = window;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(UniPaletteWindow).GetMethod("CreateGUI", flags).Invoke(window, null);
            var item = new SceneObjectContext(target);
            using var state = new PaletteState(new[] { item }, PaletteTestData.Commands());
            PaletteTestData.Install(window, state);
            window.Show(); window.Focus();
            // A delayed hierarchy notification from fixture creation is a separate signal:
            // it legitimately refreshes the list. Settle it before testing our Selection notification.
            var quietUntil = EditorApplication.timeSinceStartup + 0.25;
            var settleDeadline = EditorApplication.timeSinceStartup + 5;
            void PostponeQuietPeriod() => quietUntil = EditorApplication.timeSinceStartup + 0.25;
            EditorApplication.hierarchyChanged += PostponeQuietPeriod;
            EditorApplication.projectChanged += PostponeQuietPeriod;
            try
            {
                while ((EditorApplication.timeSinceStartup < quietUntil || EditorApplication.isUpdating)
                    && EditorApplication.timeSinceStartup < settleDeadline) yield return null;
                Assert.That(EditorApplication.timeSinceStartup, Is.GreaterThanOrEqualTo(quietUntil), "Fixture notifications did not settle");
                Assert.That(EditorApplication.isUpdating, Is.False);
            }
            finally
            {
                EditorApplication.hierarchyChanged -= PostponeQuietPeriod;
                EditorApplication.projectChanged -= PostponeQuietPeriod;
            }
            state.Select(0);
            typeof(UniPaletteWindow).GetMethod("ActivateResult", flags).Invoke(window, new object[] { true, false });
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(Selection.activeObject, Is.EqualTo(target));
            typeof(UniPaletteWindow).GetMethod("OnSelectionChanged", flags).Invoke(window, null);
            var hierarchyEvents = 0;
            var projectEvents = 0;
            void CountHierarchy() => hierarchyEvents++;
            void CountProject() => projectEvents++;
            EditorApplication.hierarchyChanged += CountHierarchy;
            EditorApplication.projectChanged += CountProject;
            try { for (var i = 0; i < 15; ++i) yield return null; }
            finally
            {
                EditorApplication.hierarchyChanged -= CountHierarchy;
                EditorApplication.projectChanged -= CountProject;
            }
            Assert.That(state.SelectedIndex, Is.Zero, "Own notification/poll must not clear forward selection; hierarchy="
                + hierarchyEvents + ", project=" + projectEvents + ", target=" + (Selection.activeObject == target));
            Assert.That(window.rootVisualElement.Q<ListView>("results").selectedIndex, Is.Zero);
            Assert.That(state.Back(), Is.True);
            Assert.That(state.CanGoBack, Is.False, "Exactly one transition was pushed");
            Assert.That(Selection.activeObject, Is.EqualTo(target));
        }

        [Test]
        public void ConfirmedContextReadsFreshMenuStateWithoutCachedSearchLabels()
        {
            var target = Go("Target");
            using var context = UnityEditor.Search.SearchService.CreateContext("menu", "", UnityEditor.Search.SearchFlags.Synchronous);
            using var results = UnityEditor.Search.SearchService.Request(context, UnityEditor.Search.SearchFlags.Synchronous);
            var stale = results.Single(item => item.id == "GameObject/Create Empty Parent");
            Assert.That(stale.GetLabel(context), Does.Contain("disabled"));
            using var state = new PaletteState(new PaletteItem[] { new SceneObjectContext(target) }, PaletteTestData.Commands());
            state.Select(0); state.Enter(true);
            Assert.That(MenuCommands.MatchesSelection(state.Context), Is.True);
            Assert.That(Menu.GetEnabled(stale.id), Is.True);
            var tree = MenuCommands.Build(new[] { stale.id }, state.Context, MenuCommands.MatchesSelection(state.Context), path => Menu.GetEnabled(path));
            Assert.That(tree.Children[0].Children.Single().Path, Is.EqualTo(stale.id));
            Assert.That(stale.GetLabel(context), Does.Contain("disabled"), "Stale label is deliberately not the availability source");
        }

        [Test]
        public void EmptyFolderOpensCommandsAndMultiSelectionIsNotReinterpreted()
        {
            CreateAssets();
            AssetDatabase.CreateFolder(folder, "Empty");
            Selection.objects = new Object[] { Go("A"), Go("B") };
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Assert.That(state.Results.Any(item => item.SelectionEntry), Is.False);
            Choose(state, "Project"); state.Enter(false);
            state.SetQuery(folder + "/Empty"); state.MoveSelection(1);
            Assert.That(state.Enter(false), Is.True);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Context.Path, Is.EqualTo(folder + "/Empty"));
            state.Back();
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo(temporaryRoot), "Deep entry restores its visible ancestor");
        }
    }
}

