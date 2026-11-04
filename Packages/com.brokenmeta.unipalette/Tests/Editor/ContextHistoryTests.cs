using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

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
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Choose(session, "Hierarchy"); session.ActivateSelected();
            session.SetQuery("Needle"); session.MoveSelection(1); session.OpenContextCommands();
            leaf.transform.SetParent(otherParent.transform);
            leaf.name = "Renamed";
            session.GoBack();
            Assert.That(session.Query, Is.Empty);
            Assert.That(session.Results[session.SelectedIndex].Target, Is.EqualTo(otherParent), "Current ancestry, not an old path or duplicate name");
            session.ActivateSelected();
            Assert.That(session.SelectedIndex, Is.Zero);
            session.Select(0); session.OpenContextCommands();
            leaf.name = "RenamedAgain";
            session.GoBack();
            Assert.That(session.Results[session.SelectedIndex].Target, Is.EqualTo(leaf), "Visible object restored by identity after rename");
        }

        [Test]
        public void DeletedEntryLeavesNoSelectionEvenWhenItsAncestorIsVisible()
        {
            var parent = Go("Parent");
            var leaf = Go("Needle", parent);
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Choose(session, "Hierarchy"); session.ActivateSelected();
            session.SetQuery("Needle"); session.MoveSelection(1); session.OpenContextCommands();
            Object.DestroyImmediate(leaf);
            session.GoBack();
            Assert.That(session.Query, Is.Empty);
            Assert.That(session.Results.Any(item => item.Target == parent), Is.True);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void BackRestoresAssetByGuidAndVisibleAncestorAfterMove()
        {
            CreateAssets();
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Choose(session, "Project"); session.ActivateSelected();
            session.SetQuery(folder);
            Choose(session, "Data"); session.ActivateSelected();
            session.SetQuery(folder + "/A/Nested/Needle.txt"); session.MoveSelection(1); session.OpenContextCommands();
            Assert.That(AssetDatabase.MoveAsset(folder + "/A/Nested/Needle.txt", folder + "/AB/Moved.txt"), Is.Empty);
            session.GoBack();
            Assert.That(session.Query, Is.Empty);
            Assert.That(session.Results[session.SelectedIndex].Path, Is.EqualTo(folder + "/AB"));
            session.ActivateSelected();
            Choose(session, "Moved.txt"); session.OpenContextCommands();
            Assert.That(AssetDatabase.MoveAsset(folder + "/AB/Moved.txt", folder + "/AB/Renamed.txt"), Is.Empty);
            session.GoBack();
            Assert.That(session.Results[session.SelectedIndex].Path, Is.EqualTo(folder + "/AB/Renamed.txt"));
        }

        [Test]
        public void BackRestoresCurrentSelectionOnlyForTheSameObject()
        {
            var first = Go("First");
            var second = Go("Second");
            Selection.activeObject = first;
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            session.Select(0); session.ActivateSelected(); session.GoBack();
            Assert.That(session.SelectedIndex, Is.Zero, "Previously explicit first row is restored");
            session.ActivateSelected();
            Selection.activeObject = second;
            session.GoBack();
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            Assert.That(session.Results.Single(item => item.SelectionEntry).Target, Is.EqualTo(second));
        }

        [Test]
        public void DeletedContextsAndHistoryAreSkippedOnRefreshAndBack()
        {
            var parent = Go("Parent");
            var branch = Go("Branch", parent);
            Go("Leaf", branch);
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Choose(session, "Hierarchy"); session.ActivateSelected();
            Choose(session, "Parent"); session.ActivateSelected();
            Choose(session, "Branch"); session.ActivateSelected();
            session.OpenContextCommands();
            session.Select(1); session.ActivateSelected(); // Experimental category.
            session.SetQuery("Cube");
            Object.DestroyImmediate(branch);
            session.Refresh();
            Assert.That(session.Browser, Is.TypeOf<ContextBrowser>());
            Assert.That(session.Context.Target, Is.EqualTo(parent));
            Assert.That(session.Query, Is.Empty);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            parent.name = "Renamed";
            session.Refresh();
            Assert.That(session.Context.Title, Is.EqualTo("Renamed"));
            Assert.That(session.Context.Path, Does.Contain("Renamed"));
            Object.DestroyImmediate(parent);
            session.GoBack();
            Assert.That(session.Context, Is.TypeOf<SceneRootContext>());
        }
    }
}
