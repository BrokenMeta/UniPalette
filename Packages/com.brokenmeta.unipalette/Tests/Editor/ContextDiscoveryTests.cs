using System.IO;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Choose(session, "Hierarchy");
            session.ActivateSelected();
            Assert.That(session._results.Any(item => item.Target == parent), Is.True);

            Assert.That(
                session._results.Any(item => item.Target == child),
                Is.False,
                "Empty query shows immediate children"
            );

            Choose(session, "TestParent");
            session.ActivateSelected();
            session.SetQuery("Needle");
            Assert.That(session._results.Select(item => item.Target), Is.EqualTo(new[] { child, }));
            session.MoveSelection(1);
            Assert.That(session.ActivateSelected(), Is.True, "Smart Enter opens leaf commands");
            Assert.That(session.Context.Target, Is.EqualTo(child));

            Assert.That(
                Selection.objects,
                Is.EqualTo(new Object[] { child, }),
                "Confirming the leaf adopts Unity Selection"
            );
        }

        [Test]
        public void DuplicateNamesHaveDifferentPathsEvenWithSameParent()
        {
            var parent = Go("Parent");
            var a = Go("Needle", parent);
            var b = Go("Needle", parent);

            var items = new UnityContextSource()
                .GetItems(new SceneObjectContext(parent), true)
                .ToArray();

            Assert.That(items.Select(item => item.Target), Is.EqualTo(new[] { a, b, }));

            Assert.That(
                items
                    .Select(item => item.Path)
                    .Distinct()
                    .Count(),
                Is.EqualTo(2)
            );
        }

        [Test]
        public void ProjectSearchRespectsFolderBoundaryAndImmediateChildren()
        {
            CreateAssets();
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Choose(session, "Project");
            session.ActivateSelected();
            Choose(session, Path.GetFileName(_temporaryRoot));
            session.ActivateSelected();
            Choose(session, "Data");
            session.ActivateSelected();
            Assert.That(session._results.Select(item => item.Title), Is.EquivalentTo(new[] { "A", "AB", }));
            Choose(session, "A");
            session.ActivateSelected();
            Assert.That(session._results.Select(item => item.Title), Is.EqualTo(new[] { "Nested", }));
            session.SetQuery("Needle");

            Assert.That(
                session._results.Select(item => item.Path),
                Is.EqualTo(
                    new[]
                    {
                        _folder
                        + "/A/Nested/Needle.txt",
                    }
                )
            );

            session.MoveSelection(1);
            Assert.That(session.ActivateSelected(), Is.True);
            Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());
            Assert.That(session.Context.Target, Is.InstanceOf<TextAsset>());
            session.GoBack();
            Assert.That(session.Query, Is.Empty);
            Assert.That(session._results[session.SelectedIndex].Title, Is.EqualTo("Nested"));
            session.SetQuery("not found"); // Clear the restored row before testing commands for the current folder.
            session.OpenContextCommands();
            Assert.That(session.Context.Path, Is.EqualTo(_folder + "/A"), "Ctrl Enter without result uses folder");
        }

        [Test]
        public void AssetSelectionFolderMoveAndDeletionRefreshByGuid()
        {
            CreateAssets();
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(_folder + "/A");
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Assert.That(session._results.Single(item => item.SelectionEntry).Path, Is.EqualTo(_folder + "/A"));
            session.Select(0);
            session.ActivateSelected();
            Assert.That(AssetDatabase.MoveAsset(_folder + "/A", _folder + "/Moved"), Is.Empty);
            session.Refresh();
            Assert.That(session.Context.Path, Is.EqualTo(_folder + "/Moved"));
            session.GoBack();
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(_folder + "/Moved/Nested/Needle.txt");
            session.Refresh();
            Assert.That(session._results.Single(item => item.SelectionEntry).Target, Is.InstanceOf<TextAsset>());
            session.Select(0);
            session.OpenContextCommands();
            AssetDatabase.DeleteAsset(_folder + "/Moved/Nested/Needle.txt");
            session.Refresh();
            Assert.That(session.Context, Is.Null);
            Assert.That(session._results.Any(item => item.SelectionEntry), Is.False);
        }
    }
}