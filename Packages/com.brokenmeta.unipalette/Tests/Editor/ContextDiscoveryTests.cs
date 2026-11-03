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
    [Category("ContextDiscovery")]
    [Category("EditorIntegration")]
    public class ContextDiscoveryTests : ContextTestFixture
    {

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
            state.ActivateSelected();
            Assert.That(state.Results.Any(item => item.Target == parent), Is.True);
            Assert.That(state.Results.Any(item => item.Target == child), Is.False, "Empty query shows immediate children");
            Choose(state, "TestParent");
            state.ActivateSelected();
            state.SetQuery("Needle");
            Assert.That(state.Results.Select(item => item.Target), Is.EqualTo(new[] { child }));
            state.MoveSelection(1);
            Assert.That(state.ActivateSelected(), Is.True, "Smart Enter opens leaf commands");
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
        public void ProjectSearchRespectsFolderBoundaryAndImmediateChildren()
        {
            CreateAssets();
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Project"); state.ActivateSelected();
            Choose(state, Path.GetFileName(temporaryRoot)); state.ActivateSelected();
            Choose(state, "Data"); state.ActivateSelected();
            Assert.That(state.Results.Select(item => item.Title), Is.EquivalentTo(new[] { "A", "AB" }));
            Choose(state, "A"); state.ActivateSelected();
            Assert.That(state.Results.Select(item => item.Title), Is.EqualTo(new[] { "Nested" }));
            state.SetQuery("Needle");
            Assert.That(state.Results.Select(item => item.Path), Is.EqualTo(new[] { folder + "/A/Nested/Needle.txt" }));
            state.MoveSelection(1);
            Assert.That(state.ActivateSelected(), Is.True);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Context.Target, Is.InstanceOf<TextAsset>());
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Nested"));
            state.SetQuery("not found"); // Clear the restored row before testing commands for the current folder.
            state.OpenContextCommands();
            Assert.That(state.Context.Path, Is.EqualTo(folder + "/A"), "Ctrl Enter without result uses folder");
        }

        [Test]
        public void AssetSelectionFolderMoveAndDeletionRefreshByGuid()
        {
            CreateAssets();
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(folder + "/A");
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Assert.That(state.Results.Single(item => item.SelectionEntry).Path, Is.EqualTo(folder + "/A"));
            state.Select(0); state.ActivateSelected();
            Assert.That(AssetDatabase.MoveAsset(folder + "/A", folder + "/Moved"), Is.Empty);
            state.Refresh();
            Assert.That(state.Context.Path, Is.EqualTo(folder + "/Moved"));
            state.GoBack();
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(folder + "/Moved/Nested/Needle.txt");
            state.Refresh();
            Assert.That(state.Results.Single(item => item.SelectionEntry).Target, Is.InstanceOf<TextAsset>());
            state.Select(0); state.OpenContextCommands();
            AssetDatabase.DeleteAsset(folder + "/Moved/Nested/Needle.txt");
            state.Refresh();
            Assert.That(state.Context, Is.Null);
            Assert.That(state.Results.Any(item => item.SelectionEntry), Is.False);
        }
    }
}
