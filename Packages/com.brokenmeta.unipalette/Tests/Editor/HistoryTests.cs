using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("History")]
    [Category("Fast")]
    public class HistoryTests : NavigationTestFixture
    {
        [Test]
        public void ProjectFolderQueryResetsSelectionAndBackRestoresEntry()
        {
            var session = CreateSession();
            Choose(session, "Project");
            session.ActivateSelected();
            session.SetQuery("Characters");
            session.MoveSelection(1);
            session.ActivateSelected();
            Assert.That(session.Context.Path, Is.EqualTo("Assets/Characters"));
            AssertForward(session);
            session.MoveSelection(-1);
            session.SetQuery("Player");
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.GoBack();
            Assert.That(session.Query, Is.Empty);
            Assert.That(session._results[session.SelectedIndex].Title, Is.EqualTo("Characters"));
            Assert.That(session.Context.Path, Is.EqualTo("Assets"));
        }

        [Test]
        public void BackWithoutSelectedEntryDoesNotPickFirstResult()
        {
            var session = CreateSession();
            Choose(session, "Hierarchy");
            session.ActivateSelected();
            session.SetQuery("not found");
            Assert.That(session.OpenContextCommands(), Is.True);
            session.SetQuery("Inspect");
            session.GoBack();
            Assert.That(session.Query, Is.Empty);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.GoBack();
            Assert.That(session._results[session.SelectedIndex].Title, Is.EqualTo("Hierarchy"));
            session.SetQuery("Project");
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void RecursiveCategoryReturnSelectsVisibleAncestor()
        {
            var session = CreateSession();
            Choose(session, "Project");
            session.OpenContextCommands();
            session.SetQuery("Shapes");
            Choose(session, "Shapes (experimental category)");
            session.BrowseSelected();
            AssertForward(session);
            session.GoBack();
            Assert.That(session.Query, Is.Empty);
            Assert.That(session._results[session.SelectedIndex].Title, Is.EqualTo("Create (experimental category)"));
            // A forward transition replaces the restored row with the first child of the next level.
            session.ActivateSelected();
            AssertForward(session);
        }

        [Test]
        public void ForwardSelectionDoesNotOverrideQueryOrBackRules()
        {
            var session = CreateSession();
            session.SetQuery("Project");
            session.Select(0);
            session.ActivateSelected();
            AssertForward(session);
            Choose(session, "Readme");
            session.OpenContextCommands();
            AssertForward(session);
            session.GoBack();
            Assert.That(session._results[session.SelectedIndex].Title, Is.EqualTo("Readme"));
            Assert.That(session.SelectedIndex, Is.EqualTo(1), "Back must not pick first instead of its entry");
            session.SetQuery("Characters");
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.SetQuery("");
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            session.OpenContextCommands();
            AssertForward(session);
            session.GoBack();
            Assert.That(session.SelectedIndex, Is.EqualTo(-1), "No entry row was selected");
        }
    }
}