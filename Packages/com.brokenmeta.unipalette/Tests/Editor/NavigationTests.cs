using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Presentation;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("Navigation")]
    [Category("Fast")]
    public class NavigationTests : NavigationTestFixture
    {
        private sealed class ExternalChildrenSource : IContextSource
        {
            private readonly PaletteItem _root;

            private readonly PaletteItem _child;

            private readonly bool _canBrowse;

            internal PaletteItem CheckedContext { get; private set; }

            internal ExternalChildrenSource(PaletteItem root, PaletteItem child, bool canBrowse)
            {
                _root = root;
                _child = child;
                _canBrowse = canBrowse;
            }

            public IEnumerable<PaletteItem> GetItems(PaletteItem context, bool recursive) =>
                context == null ? new[] { _root, } : new[] { _child, };

            public bool HasBrowseChildren(PaletteItem context)
            {
                CheckedContext = context;
                return _canBrowse;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContextSourceControlsBrowsingIndependentlyOfItemChildren(bool canBrowse)
        {
            var child = new FixtureItem("Child", "Root/Child");
            var embeddedChildren = canBrowse ? Array.Empty<PaletteItem>() : new PaletteItem[] { child, };
            var root = new FixtureItem("Root", "Root", false, embeddedChildren);
            var contexts = new ExternalChildrenSource(root, child, canBrowse);

            using var commands = new TestCommandSource(PaletteTestData.Commands());
            using var session = new PaletteSession(commands, new UnityContextSelection(), contexts);
            var presentation = session.Browser.Accept(new BrowserPresentationSelector());
            var row = presentation.Describe(root, session.Context);
            Assert.That(contexts.CheckedContext, Is.Null, "Preparing rows must not enumerate children");
            Assert.That(row.GetIndicator().Text, Is.EqualTo(canBrowse ? "\u203A" : "\u2197"));
            Assert.That(contexts.CheckedContext, Is.SameAs(root));

            session.Select(0);

            Assert.That(session.ActivateSelected(), Is.True);
            Assert.That(contexts.CheckedContext, Is.SameAs(root));
            Assert.That(session.Context, Is.SameAs(root));

            if (canBrowse)
            {
                Assert.That(session.Browser, Is.TypeOf<ContextBrowser>());
                Assert.That(session.Results, Is.EqualTo(new[] { child, }));
            }
            else
            {
                Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());
            }

            Assert.That(session.GoBack(), Is.True);
            Assert.That(session.Context, Is.Null);
            Assert.That(session.SelectedIndex, Is.Zero);
        }

        [TestCase(0, false, typeof(CommandBrowser), "Scene Root/Player")]
        [TestCase(0, true, typeof(CommandBrowser), "Scene Root/Player")]
        [TestCase(1, false, typeof(ContextBrowser), "Scene Root")]
        [TestCase(1, true, typeof(CommandBrowser), "Scene Root")]
        [TestCase(2, false, typeof(ContextBrowser), "Assets")]
        [TestCase(2, true, typeof(CommandBrowser), "Assets")]
        public void RootEntries(int index, bool control, Type browserType, string path)
        {
            var session = CreateSession();
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.Select(index);
            Assert.That(control ? session.OpenContextCommands() : session.ActivateSelected(), Is.True);
            Assert.That(session.Browser.GetType(), Is.EqualTo(browserType));
            Assert.That(session.Context.Path, Is.EqualTo(path));
            AssertForward(session);
            Assert.That(session.GoBack(), Is.True);
            Assert.That(session.Context, Is.Null);
            Assert.That(session.Query.IsEmpty, Is.True);
            Assert.That(session.SelectedIndex, Is.EqualTo(index));
        }

        [Test]
        public void MockSelectionCanBeAbsentAndRootHasNoImplicitContext()
        {
            var session = CreateSession(false);
            Assert.That(session.Results.Count, Is.EqualTo(2));
            Assert.That(session.ActivateSelected(), Is.False);
            Assert.That(session.OpenContextCommands(), Is.False);
            Assert.That(session.GoBack(), Is.False);
        }

        [Test]
        public void NestedContextSearchLeafAndCurrentContextCommands()
        {
            var session = CreateSession();
            Choose(session, "Hierarchy");
            session.ActivateSelected();
            session.SetQuery("Muzzle");
            Assert.That(session.Results.Count, Is.EqualTo(1));
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.MoveSelection(1);
            Assert.That(session.ActivateSelected(), Is.True, "Smart Enter opens leaf commands");
            Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());
            Assert.That(session.Context.Path, Is.EqualTo("Scene Root/Player/Weapon/Muzzle"));
            AssertForward(session);
            session.GoBack();
            Assert.That(session.Query.IsEmpty, Is.True);
            Assert.That(session.Results[session.SelectedIndex].Title, Is.EqualTo("Player"));
            Choose(session, "Player");
            session.ActivateSelected();
            Choose(session, "Weapon");
            session.ActivateSelected();
            session.SetQuery("Light");
            Assert.That(session.Results, Is.Empty, "Sibling subtree excluded");
            Assert.That(session.OpenContextCommands(), Is.True, "No selection uses Weapon context");
            Assert.That(session.Context.Path, Is.EqualTo("Scene Root/Player/Weapon"));
            session.GoBack();
            session.GoBack();
            Assert.That(session.Context.Path, Is.EqualTo("Scene Root/Player"));
            Assert.That(session.Query.IsEmpty, Is.True);
            Assert.That(session.Results[session.SelectedIndex].Title, Is.EqualTo("Weapon"));
        }

        [Test]
        public void CategoriesAreNavigableAndMockCommandsNeverExecuteOrNavigate()
        {
            var session = CreateSession();
            Choose(session, "Project");
            session.OpenContextCommands();
            Choose(session, "Create (experimental category)");
            session.ActivateSelected();
            Choose(session, "Shapes (experimental category)");
            session.ActivateSelected();
            AssertForward(session);
            session.SetQuery("Cube");
            session.MoveSelection(1);
            Assert.That(session.OpenContextCommands(), Is.False);
            Assert.That(session.Message, Is.Empty);
            Assert.That(session.ActivateSelected(), Is.False);
            Assert.That(session.Message, Does.Contain("nothing executed"));
            session.GoBack();
            Assert.That(session.Location, Is.EqualTo("Mock commands/Create"));
            Assert.That(session.Query.IsEmpty, Is.True);
            Assert.That(session.Results[session.SelectedIndex].Title, Is.EqualTo("Shapes (experimental category)"));
            session.GoBack();
            session.SetQuery("Shapes/Cube");
            Assert.That(session.Results.Count, Is.EqualTo(1));
        }

        [Test]
        public void CommandCategoryOpensThroughActivateOrBrowseButNotContextConfirmation()
        {
            using var session = CreateSession();
            Choose(session, "Project");
            session.OpenContextCommands();
            Choose(session, "Create (experimental category)");
            var selected = session.SelectedIndex;
            var location = session.Location;
            Assert.That(session.OpenContextCommands(), Is.False);
            Assert.That(session.Location, Is.EqualTo(location));
            Assert.That(session.SelectedIndex, Is.EqualTo(selected));
            Assert.That(session.Message, Is.Empty);
            Assert.That(session.BrowseSelected(), Is.True);
            var categoryLocation = session.Location;
            Assert.That(session.GoBack(), Is.True);
            Assert.That(session.Results[session.SelectedIndex].Title, Is.EqualTo("Create (experimental category)"));
            Assert.That(session.ActivateSelected(), Is.True);
            Assert.That(session.Location, Is.EqualTo(categoryLocation));
        }

        [Test]
        public void BrowsingCommandLeafPreservesExistingMessageAndNavigation()
        {
            using var session = CreateSession();
            Choose(session, "Project");
            session.OpenContextCommands();
            Choose(session, "Inspect (mock)");
            Assert.That(session.ActivateSelected(), Is.False);
            var message = session.Message;
            var location = session.Location;
            var selected = session.SelectedIndex;
            Assert.That(message, Does.Contain("nothing executed"));
            Assert.That(session.BrowseSelected(), Is.False);
            Assert.That(session.Message, Is.EqualTo(message));
            Assert.That(session.Location, Is.EqualTo(location));
            Assert.That(session.SelectedIndex, Is.EqualTo(selected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SmartEnterWithEmptyCommandsKeepsSelectionEmptyAndRestoresLeaf(bool altRight)
        {
            var leaf = new FixtureItem("Leaf", "Leaf");

            using var session = PaletteTestData.CreateSession(
                new[] { leaf, },
                new FixtureItem(
                    "Commands",
                    ""
                )
            );

            session.Select(0);
            Assert.That(altRight ? session.BrowseSelected() : session.ActivateSelected(), Is.True);
            Assert.That(session.Browser, Is.TypeOf<CommandBrowser>());
            Assert.That(session.Results, Is.Empty);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.GoBack();
            Assert.That(session.SelectedIndex, Is.Zero);
            Assert.That(session.Results[0], Is.SameAs(leaf));
            session.SetQuery("Leaf");
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void EmptyResultsAndExplicitArrowSelection()
        {
            var session = CreateSession();
            session.MoveSelection(-1);
            Assert.That(session.SelectedIndex, Is.EqualTo(2));
            session.SetQuery("not found");
            session.MoveSelection(1);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            Assert.That(session.ActivateSelected(), Is.False);
            session.SetQuery("");
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.MoveSelection(1);
            Assert.That(session.SelectedIndex, Is.Zero);
        }
    }
}