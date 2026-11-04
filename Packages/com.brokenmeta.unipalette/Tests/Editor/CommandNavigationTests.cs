using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using BrokenMeta.UniPalette.Editor.Application;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("Navigation")]
    [Category("Fast")]
    public class CommandNavigationTests : CommandTestFixture
    {

        [Test]
        public void RefreshingCategoryKeepsPreviousBrowserSnapshot()
        {
            var original = Build(new[] { "Assets/Create/Folder" });
            var browser = new CommandBrowser(Assets(), original.Children[0].Children[0]);
            var updated = Build(new[] { "Assets/Create/Material" });
            var refreshed = browser.RefreshCommands(_ => updated);

            Assert.That(refreshed.Location, Is.EqualTo("Assets/Create"));
            Assert.That(refreshed.Context, Is.SameAs(browser.Context));
            Assert.That(refreshed.GetItems("").Single().Path, Is.EqualTo("Assets/Create/Material"));
            Assert.That(browser.GetItems("").Single().Path, Is.EqualTo("Assets/Create/Folder"),
                "Refreshing the current browser must not mutate a snapshot retained in history");
        }

        [Test]
        public void RefreshingRemovedCategoryFallsBackToCommandRoot()
        {
            var original = Build(new[] { "Assets/Create/Folder" });
            var browser = new CommandBrowser(Assets(), original.Children[0].Children[0]);
            var updated = Build(new[] { "Assets/Open" });
            var refreshed = browser.RefreshCommands(_ => updated);

            Assert.That(refreshed.Location, Is.EqualTo("Commands"));
            Assert.That(refreshed.GetItems("").Single().Path, Is.EqualTo("Assets"));
            Assert.That(refreshed.Context, Is.SameAs(browser.Context));
            Assert.That(browser.Location, Is.EqualTo("Assets/Create"));
        }

        [Test]
        public void EmptyQueryShowsChildrenAndRecursiveQueryStaysInsideCategory()
        {
            using var session = PaletteTestData.CreateSession(new[] { Assets() }, Build());
            session.Select(0); session.OpenContextCommands();
            Assert.That(session.Results.Select(item => item.Path), Is.EqualTo(new[] { "Assets" }));
            Choose(session, "Assets"); session.ActivateSelected();
            Choose(session, "Assets/Create"); session.BrowseSelected();
            Assert.That(session.Results.Select(item => item.Path), Is.EqualTo(new[] { "Assets/Create/Folder", "Assets/Create/Material" }));
            session.SetQuery("Other");
            Assert.That(session.Results, Is.Empty, "CreateMore is not a descendant of Create");
            session.SetQuery("CREATE/material");
            Assert.That(session.Results.Single().Path, Is.EqualTo("Assets/Create/Material"));
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.GoBack();
            Assert.That(session.Results[session.SelectedIndex].Path, Is.EqualTo("Assets/Create"));
        }

        [Test]
        public void RepeatedQueryAndDeepCategoryReturnHaveNoDuplicates()
        {
            var root = Build(new[] { "Assets/Create/Shapes/Cube", "Assets/Create/Shapes/Cube", "Assets/Open" });
            using var session = PaletteTestData.CreateSession(new[] { Assets() }, root);
            session.Select(0); session.OpenContextCommands();
            session.SetQuery("Shapes");
            Choose(session, "Assets/Create/Shapes"); session.ActivateSelected();
            Assert.That(session.SelectedIndex, Is.Zero);
            Assert.That(session.Results.Single().Path, Is.EqualTo("Assets/Create/Shapes/Cube"));
            session.GoBack();
            Assert.That(session.Results[session.SelectedIndex].Path, Is.EqualTo("Assets"), "Restore visible ancestor at menu root");
            for (var i = 0; i < 3; ++i)
            {
                session.SetQuery("Cube");
                Assert.That(session.Results.Single().Path, Is.EqualTo("Assets/Create/Shapes/Cube"));
                session.SetQuery("");
                Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            }
        }
    }
}
