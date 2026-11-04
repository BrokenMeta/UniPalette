using System.Linq;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
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
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Assert.That(session._results.Any(item => item.SelectionEntry), Is.False);
            Selection.activeObject = first;
            session.Refresh();
            Assert.That(session._results.Single(item => item.SelectionEntry).Target, Is.EqualTo(first));
            session.SetQuery("Current Selection");
            Assert.That(session._results.Single().Target, Is.EqualTo(first));
            session.SetQuery("");
            session.Select(0);
            session.ActivateSelected();
            Selection.activeObject = second;
            session.Refresh();
            Assert.That(session.Context.Target, Is.EqualTo(first), "Open command context is stable");
            session.GoBack();
            Assert.That(session._results.Single(item => item.SelectionEntry).Target, Is.EqualTo(second));
            Object.DestroyImmediate(second);
            session.Refresh();
            Assert.That(session._results.Any(item => item.SelectionEntry), Is.False);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
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
            AssetDatabase.CreateFolder(_folder, "Empty");
            var parent = Go("Parent");
            var leaf = Go("Leaf", parent);

            var target = kind == "Parent"
                ? parent
                : kind == "GameObject"
                    ? leaf
                    : AssetDatabase.LoadMainAssetAtPath(
                        kind == "Asset"
                            ? _folder
                            + "/A/Nested/Needle.txt"
                            : kind == "EmptyFolder"
                                ? _folder
                                + "/Empty"
                                : _folder
                                + "/A"
                    );

            var item = target is GameObject go
                ? (PaletteItem)new SceneObjectContext(go)
                : new AssetContext(
                    AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(
                            target
                        )
                    )
                );

            using var session = PaletteTestData.CreateSession(new[] { item, }, PaletteTestData.Commands());
            session.SetQuery(item.Title);
            session.Select(0);
            Assert.That(altRight ? session.BrowseSelected() : session.ActivateSelected(), Is.True);
            bool container = kind == "Parent" || kind == "Folder";

            Assert.That(
                session.Browser.GetType(),
                Is.EqualTo(
                    container
                        ? typeof(ContextBrowser)
                        : typeof(CommandBrowser)
                )
            );

            Assert.That(session.Context.Target, Is.EqualTo(target));
            Assert.That(session.Query, Is.Empty);
            Assert.That(session.SelectedIndex, Is.Zero);
            session.GoBack();
            Assert.That(session.Query, Is.Empty);
            Assert.That(session._results[session.SelectedIndex].Target, Is.EqualTo(target));

            Assert.That(
                session.OpenContextCommands(),
                Is.True,
                "Ctrl Enter always opens commands, including containers"
            );

            Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());
            Assert.That(session.Context.Target, Is.EqualTo(target));
            Assert.That(Selection.objects, Is.EqualTo(new[] { target, }));
        }

        [TestCase("GameObject")]
        [TestCase("Asset")]
        [TestCase("Folder")]
        public void ConfirmationAdoptsTargetAndBackAndCategoriesDoNotRestoreSelection(string kind)
        {
            CreateAssets();
            var original = Go("Original");

            var target = kind == "GameObject"
                ? Go("Target")
                : AssetDatabase.LoadMainAssetAtPath(
                    kind == "Asset"
                        ? _folder
                        + "/A/Nested/Needle.txt"
                        : _folder
                        + "/A"
                );

            var item = target is GameObject go
                ? (PaletteItem)new SceneObjectContext(go)
                : new AssetContext(
                    AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(
                            target
                        )
                    )
                );

            Selection.activeObject = original;
            using var session = PaletteTestData.CreateSession(new[] { item, }, PaletteTestData.Commands());
            session.MoveSelection(1);
            session.SetQuery(item.Title);
            Assert.That(Selection.activeObject, Is.EqualTo(original), "Browsing/query/selection are passive");
            session.Select(0);
            session.OpenContextCommands();
            Assert.That(Selection.objects, Is.EqualTo(new[] { target, }));
            session.Select(1);
            session.ActivateSelected(); // Command category.
            Assert.That(Selection.activeObject, Is.EqualTo(target));
            session.GoBack();
            session.GoBack();

            Assert.That(
                Selection.activeObject,
                Is.EqualTo(target),
                "Back restores palette row, not global Selection"
            );

            Assert.That(session._results[session.SelectedIndex].Target, Is.EqualTo(target));
        }

        [Test]
        public void VirtualRootsNeverAdoptAmbientSelectionAsTheirTarget()
        {
            var original = Go("Original");
            Selection.activeObject = original;
            using var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());

            foreach (string title in new[] { "Hierarchy", "Project", }
            )
            {
                Selection.activeObject = original;
                Choose(session, title);
                session.ActivateSelected();
                Assert.That(Selection.activeObject, Is.EqualTo(original), "Browsing a root is passive");
                session.GoBack();
                Choose(session, title);
                session.OpenContextCommands();
                Assert.That(session.Context.Target, Is.Null, "A virtual root does not adopt the previous object");
                Assert.That(new UnityContextSelection().MatchesSelection(session.Context), Is.True);

                if (title == "Hierarchy")
                    Assert.That(Selection.count, Is.Zero);
                else
                    Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo("Assets"));

                var prepared = Selection.objects;
                session.GoBack();
                Assert.That(Selection.objects, Is.EqualTo(prepared), "Back does not restore ambient Selection");
            }
        }

        [Test]
        public void ConfirmedContextReadsFreshMenuStateWithoutCachedSearchLabels()
        {
            var target = Go("Target");

            using var context = SearchService.CreateContext(
                "menu",
                "",
                SearchFlags.Synchronous
            );

            using var results = SearchService.Request(
                context,
                SearchFlags.Synchronous
            );

            var stale = results.Single(
                item => item.id == "GameObject/Create Empty Parent"
            );

            Assert.That(stale.GetLabel(context), Does.Contain("disabled"));

            using var session = PaletteTestData.CreateSession(
                new PaletteItem[]
                {
                    new SceneObjectContext(
                        target
                    ),
                },
                PaletteTestData.Commands()
            );

            session.Select(0);
            session.OpenContextCommands();
            Assert.That(new UnityContextSelection().MatchesSelection(session.Context), Is.True);
            Assert.That(Menu.GetEnabled(stale.id), Is.True);

            var tree = MenuCommands.Build(
                new[] { stale.id, },
                session.Context,
                new UnityContextSelection().MatchesSelection(
                    session.Context
                ),
                path => Menu.GetEnabled(
                    path
                )
            );

            Assert.That(tree.Children[0].Children.Single().Path, Is.EqualTo(stale.id));

            Assert.That(
                stale.GetLabel(context),
                Does.Contain("disabled"),
                "Stale label is deliberately not the availability source"
            );
        }

        [Test]
        public void EmptyFolderOpensCommandsAndMultiSelectionIsNotReinterpreted()
        {
            CreateAssets();
            AssetDatabase.CreateFolder(_folder, "Empty");
            Selection.objects = new Object[] { Go("A"), Go("B"), };
            var session = PaletteTestData.CreateSession(commandRoot: PaletteTestData.Commands());
            Assert.That(session._results.Any(item => item.SelectionEntry), Is.False);
            Choose(session, "Project");
            session.ActivateSelected();
            session.SetQuery(_folder + "/Empty");
            session.MoveSelection(1);
            Assert.That(session.ActivateSelected(), Is.True);
            Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());
            Assert.That(session.SelectedIndex, Is.Zero);
            Assert.That(session.Context.Path, Is.EqualTo(_folder + "/Empty"));
            session.GoBack();

            Assert.That(
                session._results[session.SelectedIndex].Path,
                Is.EqualTo(_temporaryRoot),
                "Deep entry restores its visible ancestor"
            );
        }
    }
}