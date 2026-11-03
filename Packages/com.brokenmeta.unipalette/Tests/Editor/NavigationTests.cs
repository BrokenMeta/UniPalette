using NUnit.Framework;
using BrokenMeta.UniPalette.Editor;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("Navigation")]
    [Category("Fast")]
    public class NavigationTests : NavigationTestFixture
    {

        [TestCase(0, false, typeof(CommandBrowser), "Scene Root/Player")]
        [TestCase(0, true, typeof(CommandBrowser), "Scene Root/Player")]
        [TestCase(1, false, typeof(ContextBrowser), "Scene Root")]
        [TestCase(1, true, typeof(CommandBrowser), "Scene Root")]
        [TestCase(2, false, typeof(ContextBrowser), "Assets")]
        [TestCase(2, true, typeof(CommandBrowser), "Assets")]
        public void RootEntries(int index, bool control, System.Type browserType, string path)
        {
            var state = CreateState();
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.Select(index);
            Assert.That((control ? state.OpenContextCommands() : state.ActivateSelected()), Is.True);
            Assert.That(state.Browser.GetType(), Is.EqualTo(browserType));
            Assert.That(state.Context.Path, Is.EqualTo(path));
            AssertForward(state);
            Assert.That(state.GoBack(), Is.True);
            Assert.That(state.Context, Is.Null);
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(index));
        }

        [Test]
        public void MockSelectionCanBeAbsentAndRootHasNoImplicitContext()
        {
            var state = CreateState(false);
            Assert.That(state.Results.Count, Is.EqualTo(2));
            Assert.That(state.ActivateSelected(), Is.False);
            Assert.That(state.OpenContextCommands(), Is.False);
            Assert.That(state.GoBack(), Is.False);
        }

        [Test]
        public void NestedContextSearchLeafAndCurrentContextCommands()
        {
            var state = CreateState();
            Choose(state, "Hierarchy");
            state.ActivateSelected();
            state.SetQuery("Muzzle");
            Assert.That(state.Results.Count, Is.EqualTo(1));
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.MoveSelection(1);
            Assert.That(state.ActivateSelected(), Is.True, "Smart Enter opens leaf commands");
            Assert.That(state.Browser, Is.TypeOf<CommandBrowser>());
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Player/Weapon/Muzzle"));
            AssertForward(state);
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Player"));
            Choose(state, "Player");
            state.ActivateSelected();
            Choose(state, "Weapon");
            state.ActivateSelected();
            state.SetQuery("Light");
            Assert.That(state.Results, Is.Empty, "Sibling subtree excluded");
            Assert.That(state.OpenContextCommands(), Is.True, "No selection uses Weapon context");
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Player/Weapon"));
            state.GoBack();
            state.GoBack();
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Player"));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Weapon"));
        }

        [Test]
        public void CategoriesAreNavigableAndMockCommandsNeverExecuteOrNavigate()
        {
            var state = CreateState();
            Choose(state, "Project");
            state.OpenContextCommands();
            Choose(state, "Create (experimental category)");
            state.ActivateSelected();
            Choose(state, "Shapes (experimental category)");
            state.ActivateSelected();
            AssertForward(state);
            state.SetQuery("Cube");
            state.MoveSelection(1);
            Assert.That(state.OpenContextCommands(), Is.False);
            Assert.That(state.Message, Is.Empty);
            Assert.That(state.ActivateSelected(), Is.False);
            Assert.That(state.Message, Does.Contain("nothing executed"));
            state.GoBack();
            Assert.That(state.Location, Is.EqualTo("Mock commands/Create"));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Shapes (experimental category)"));
            state.GoBack();
            state.SetQuery("Shapes/Cube");
            Assert.That(state.Results.Count, Is.EqualTo(1));
        }

        [Test]
        public void CommandCategoryOpensThroughActivateOrBrowseButNotContextConfirmation()
        {
            using var state = CreateState();
            Choose(state, "Project");
            state.OpenContextCommands();
            Choose(state, "Create (experimental category)");
            var selected = state.SelectedIndex;
            var location = state.Location;
            Assert.That(state.OpenContextCommands(), Is.False);
            Assert.That(state.Location, Is.EqualTo(location));
            Assert.That(state.SelectedIndex, Is.EqualTo(selected));
            Assert.That(state.Message, Is.Empty);
            Assert.That(state.BrowseSelected(), Is.True);
            var categoryLocation = state.Location;
            Assert.That(state.GoBack(), Is.True);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Create (experimental category)"));
            Assert.That(state.ActivateSelected(), Is.True);
            Assert.That(state.Location, Is.EqualTo(categoryLocation));
        }

        [Test]
        public void BrowsingCommandLeafPreservesExistingMessageAndNavigation()
        {
            using var state = CreateState();
            Choose(state, "Project");
            state.OpenContextCommands();
            Choose(state, "Inspect (mock)");
            Assert.That(state.ActivateSelected(), Is.False);
            var message = state.Message;
            var location = state.Location;
            var selected = state.SelectedIndex;
            Assert.That(message, Does.Contain("nothing executed"));
            Assert.That(state.BrowseSelected(), Is.False);
            Assert.That(state.Message, Is.EqualTo(message));
            Assert.That(state.Location, Is.EqualTo(location));
            Assert.That(state.SelectedIndex, Is.EqualTo(selected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SmartEnterWithEmptyCommandsKeepsSelectionEmptyAndRestoresLeaf(bool altRight)
        {
            var leaf = new FixtureItem("Leaf", "Leaf");
            using var state = new PaletteState(new[] { leaf }, new FixtureItem("Commands", ""));
            state.Select(0);
            Assert.That((altRight ? state.BrowseSelected() : state.ActivateSelected()), Is.True);
            Assert.That(state.Browser, Is.TypeOf<CommandBrowser>());
            Assert.That(state.Results, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.GoBack();
            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Results[0], Is.SameAs(leaf));
            state.SetQuery("Leaf");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void EmptyResultsAndExplicitArrowSelection()
        {
            var state = CreateState();
            state.MoveSelection(-1);
            Assert.That(state.SelectedIndex, Is.EqualTo(2));
            state.SetQuery("not found");
            state.MoveSelection(1);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            Assert.That(state.ActivateSelected(), Is.False);
            state.SetQuery("");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.MoveSelection(1);
            Assert.That(state.SelectedIndex, Is.Zero);
        }
    }
}
