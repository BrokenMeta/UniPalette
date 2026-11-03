using NUnit.Framework;
using BrokenMeta.UniPalette.Editor;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("History")]
    [Category("Fast")]
    public class HistoryTests : NavigationTestFixture
    {

        [Test]
        public void ProjectFolderQueryResetsSelectionAndBackRestoresEntry()
        {
            var state = CreateState();
            Choose(state, "Project");
            state.ActivateSelected();
            state.SetQuery("Characters");
            state.MoveSelection(1);
            state.ActivateSelected();
            Assert.That(state.Context.Path, Is.EqualTo("Assets/Characters"));
            AssertForward(state);
            state.MoveSelection(-1);
            state.SetQuery("Player");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Characters"));
            Assert.That(state.Context.Path, Is.EqualTo("Assets"));
        }

        [Test]
        public void BackWithoutSelectedEntryDoesNotPickFirstResult()
        {
            var state = CreateState();
            Choose(state, "Hierarchy"); state.ActivateSelected();
            state.SetQuery("not found");
            Assert.That(state.OpenContextCommands(), Is.True);
            state.SetQuery("Inspect");
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.GoBack();
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Hierarchy"));
            state.SetQuery("Project");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void RecursiveCategoryReturnSelectsVisibleAncestor()
        {
            var state = CreateState();
            Choose(state, "Project"); state.OpenContextCommands();
            state.SetQuery("Shapes");
            Choose(state, "Shapes (experimental category)"); state.BrowseSelected();
            AssertForward(state);
            state.GoBack();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Create (experimental category)"));
            // A forward transition replaces the restored row with the first child of the next level.
            state.ActivateSelected();
            AssertForward(state);
        }

        [Test]
        public void ForwardSelectionDoesNotOverrideQueryOrBackRules()
        {
            var state = CreateState();
            state.SetQuery("Project"); state.Select(0); state.ActivateSelected();
            AssertForward(state);
            Choose(state, "Readme"); state.OpenContextCommands();
            AssertForward(state);
            state.GoBack();
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Readme"));
            Assert.That(state.SelectedIndex, Is.EqualTo(1), "Back must not pick first instead of its entry");
            state.SetQuery("Characters");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.SetQuery("");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.OpenContextCommands();
            AssertForward(state);
            state.GoBack();
            Assert.That(state.SelectedIndex, Is.EqualTo(-1), "No entry row was selected");
        }
    }
}
