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
    [Category("ContextConfirmation")]
    [Category("EditorIntegration")]
    public class ContextConfirmationTests : ContextTestFixture
    {

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
            state.ActivateSelected();
            Selection.activeObject = second;
            state.Refresh();
            Assert.That(state.Context.Target, Is.EqualTo(first), "Open command context is stable");
            state.GoBack();
            Assert.That(state.Results.Single(item => item.SelectionEntry).Target, Is.EqualTo(second));
            Object.DestroyImmediate(second);
            state.Refresh();
            Assert.That(state.Results.Any(item => item.SelectionEntry), Is.False);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
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
            Assert.That((altRight ? state.BrowseSelected() : state.ActivateSelected()), Is.True);
            var container = kind == "Parent" || kind == "Folder";
            Assert.That(state.Mode, Is.EqualTo(container ? PaletteState.Browser.Context : PaletteState.Browser.Commands));
            Assert.That(state.Context.Target, Is.EqualTo(target));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.Zero);
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Target, Is.EqualTo(target));
            Assert.That(state.OpenContextCommands(), Is.True, "Ctrl Enter always opens commands, including containers");
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
            state.Select(0); state.OpenContextCommands();
            Assert.That(Selection.objects, Is.EqualTo(new[] { target }));
            state.Select(1); state.ActivateSelected(); // Command category.
            Assert.That(Selection.activeObject, Is.EqualTo(target));
            state.GoBack(); state.GoBack();
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
                Choose(state, title); state.ActivateSelected();
                Assert.That(Selection.activeObject, Is.EqualTo(original), "Browsing a root is passive");
                state.GoBack();
                Choose(state, title); state.OpenContextCommands();
                Assert.That(state.Context.Target, Is.Null, "A virtual root does not adopt the previous object");
                Assert.That(MenuCommands.MatchesSelection(state.Context), Is.True);
                if (title == "Hierarchy") Assert.That(Selection.count, Is.Zero);
                else Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo("Assets"));
                var prepared = Selection.objects;
                state.GoBack();
                Assert.That(Selection.objects, Is.EqualTo(prepared), "Back does not restore ambient Selection");
            }
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
            state.Select(0); state.OpenContextCommands();
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
            Choose(state, "Project"); state.ActivateSelected();
            state.SetQuery(folder + "/Empty"); state.MoveSelection(1);
            Assert.That(state.ActivateSelected(), Is.True);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Context.Path, Is.EqualTo(folder + "/Empty"));
            state.GoBack();
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo(temporaryRoot), "Deep entry restores its visible ancestor");
        }
    }
}
