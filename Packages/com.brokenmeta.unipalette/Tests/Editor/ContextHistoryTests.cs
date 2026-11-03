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
    [Category("History")]
    [Category("EditorIntegration")]
    public class ContextHistoryTests : ContextTestFixture
    {

        [Test]
        public void BackRestoresGameObjectIdentityAndNearestVisibleAncestor()
        {
            var parent = Go("Parent");
            var otherParent = Go("Parent");
            var leaf = Go("Needle", Go("Branch", parent));
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Hierarchy"); state.ActivateSelected();
            state.SetQuery("Needle"); state.MoveSelection(1); state.OpenContextCommands();
            leaf.transform.SetParent(otherParent.transform);
            leaf.name = "Renamed";
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Target, Is.EqualTo(otherParent), "Current ancestry, not an old path or duplicate name");
            state.ActivateSelected();
            Assert.That(state.SelectedIndex, Is.Zero);
            state.Select(0); state.OpenContextCommands();
            leaf.name = "RenamedAgain";
            state.GoBack();
            Assert.That(state.Results[state.SelectedIndex].Target, Is.EqualTo(leaf), "Visible object restored by identity after rename");
        }

        [Test]
        public void DeletedEntryLeavesNoSelectionEvenWhenItsAncestorIsVisible()
        {
            var parent = Go("Parent");
            var leaf = Go("Needle", parent);
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Hierarchy"); state.ActivateSelected();
            state.SetQuery("Needle"); state.MoveSelection(1); state.OpenContextCommands();
            Object.DestroyImmediate(leaf);
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results.Any(item => item.Target == parent), Is.True);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void BackRestoresAssetByGuidAndVisibleAncestorAfterMove()
        {
            CreateAssets();
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Project"); state.ActivateSelected();
            state.SetQuery(folder);
            Choose(state, "Data"); state.ActivateSelected();
            state.SetQuery(folder + "/A/Nested/Needle.txt"); state.MoveSelection(1); state.OpenContextCommands();
            Assert.That(AssetDatabase.MoveAsset(folder + "/A/Nested/Needle.txt", folder + "/AB/Moved.txt"), Is.Empty);
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo(folder + "/AB"));
            state.ActivateSelected();
            Choose(state, "Moved.txt"); state.OpenContextCommands();
            Assert.That(AssetDatabase.MoveAsset(folder + "/AB/Moved.txt", folder + "/AB/Renamed.txt"), Is.Empty);
            state.GoBack();
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo(folder + "/AB/Renamed.txt"));
        }

        [Test]
        public void BackRestoresCurrentSelectionOnlyForTheSameObject()
        {
            var first = Go("First");
            var second = Go("Second");
            Selection.activeObject = first;
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            state.Select(0); state.ActivateSelected(); state.GoBack();
            Assert.That(state.SelectedIndex, Is.Zero, "Previously explicit first row is restored");
            state.ActivateSelected();
            Selection.activeObject = second;
            state.GoBack();
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            Assert.That(state.Results.Single(item => item.SelectionEntry).Target, Is.EqualTo(second));
        }

        [Test]
        public void DeletedContextsAndHistoryAreSkippedOnRefreshAndBack()
        {
            var parent = Go("Parent");
            var branch = Go("Branch", parent);
            Go("Leaf", branch);
            var state = new PaletteState(commandRoot: PaletteTestData.Commands());
            Choose(state, "Hierarchy"); state.ActivateSelected();
            Choose(state, "Parent"); state.ActivateSelected();
            Choose(state, "Branch"); state.ActivateSelected();
            state.OpenContextCommands();
            state.Select(1); state.ActivateSelected(); // Experimental category.
            state.SetQuery("Cube");
            Object.DestroyImmediate(branch);
            state.Refresh();
            Assert.That(state.Browser, Is.TypeOf<ContextBrowser>());
            Assert.That(state.Context.Target, Is.EqualTo(parent));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            parent.name = "Renamed";
            state.Refresh();
            Assert.That(state.Context.Title, Is.EqualTo("Renamed"));
            Assert.That(state.Context.Path, Does.Contain("Renamed"));
            Object.DestroyImmediate(parent);
            state.GoBack();
            Assert.That(state.Context, Is.TypeOf<SceneRootContext>());
        }
    }
}
