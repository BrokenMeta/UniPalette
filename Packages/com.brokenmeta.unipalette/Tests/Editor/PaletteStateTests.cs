using NUnit.Framework;
using BrokenMeta.UniPalette.Editor;

namespace BrokenMeta.UniPalette.Tests
{
    public class PaletteStateTests
    {
        private static PaletteState CreateState(bool hasMockSelection = true)
        {
            var player = new PaletteState.Item("Player", "Scene Root/Player", false,
                new PaletteState.Item("Weapon", "Scene Root/Player/Weapon", false,
                    new PaletteState.Item("Muzzle", "Scene Root/Player/Weapon/Muzzle")),
                new PaletteState.Item("Camera", "Scene Root/Player/Camera"));
            var hierarchy = new PaletteState.Item("Hierarchy", "Scene Root", false, player,
                new PaletteState.Item("Light", "Scene Root/Light"));
            var project = new PaletteState.Item("Project", "Assets", false,
                new PaletteState.Item("Characters", "Assets/Characters", false,
                    new PaletteState.Item("Materials", "Assets/Characters/Materials", false,
                        new PaletteState.Item("Player Material", "Assets/Characters/Materials/Player Material")),
                    new PaletteState.Item("Player Prefab", "Assets/Characters/Player Prefab")),
                new PaletteState.Item("Readme", "Assets/Readme"));
            var roots = hasMockSelection
                ? new[] { new PaletteState.Item("Current Selection (mock: Player)", player.Path, true), hierarchy, project }
                : new[] { hierarchy, project };
            return new PaletteState(roots, PaletteTestData.Commands());
        }

        private static void Choose(PaletteState state, string title)
        {
            var index = state.Results.FindIndex(item => item.Title == title);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), title);
            state.Select(index);
        }

        private static void AssertForward(PaletteState state)
        {
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(state.Results.Count > 0 ? 0 : -1));
        }

        [TestCase(0, false, "Commands", "Scene Root/Player")]
        [TestCase(0, true, "Commands", "Scene Root/Player")]
        [TestCase(1, false, "Context", "Scene Root")]
        [TestCase(1, true, "Commands", "Scene Root")]
        [TestCase(2, false, "Context", "Assets")]
        [TestCase(2, true, "Commands", "Assets")]
        public void RootEntries(int index, bool control, string mode, string path)
        {
            var state = CreateState();
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.Select(index);
            Assert.That(state.Enter(control), Is.True);
            Assert.That(state.Mode.ToString(), Is.EqualTo(mode));
            Assert.That(state.Context.Path, Is.EqualTo(path));
            AssertForward(state);
            Assert.That(state.Back(), Is.True);
            Assert.That(state.Context, Is.Null);
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(index));
        }

        [Test]
        public void MockSelectionCanBeAbsentAndRootHasNoImplicitContext()
        {
            var state = CreateState(false);
            Assert.That(state.Results.Count, Is.EqualTo(2));
            Assert.That(state.Enter(false), Is.False);
            Assert.That(state.Enter(true), Is.False);
            Assert.That(state.Back(), Is.False);
        }

        [Test]
        public void NestedContextSearchLeafAndCurrentContextCommands()
        {
            var state = CreateState();
            Choose(state, "Hierarchy");
            state.Enter(false);
            state.SetQuery("Muzzle");
            Assert.That(state.Results.Count, Is.EqualTo(1));
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.MoveSelection(1);
            Assert.That(state.Enter(false), Is.True, "Smart Enter opens leaf commands");
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Player/Weapon/Muzzle"));
            AssertForward(state);
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Player"));
            Choose(state, "Player");
            state.Enter(false);
            Choose(state, "Weapon");
            state.Enter(false);
            state.SetQuery("Light");
            Assert.That(state.Results, Is.Empty, "Sibling subtree excluded");
            Assert.That(state.Enter(true), Is.True, "No selection uses Weapon context");
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Player/Weapon"));
            state.Back();
            state.Back();
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Player"));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Weapon"));
        }

        [Test]
        public void ProjectFolderQueryResetsSelectionAndBackRestoresEntry()
        {
            var state = CreateState();
            Choose(state, "Project");
            state.Enter(false);
            state.SetQuery("Characters");
            state.MoveSelection(1);
            state.Enter(false);
            Assert.That(state.Context.Path, Is.EqualTo("Assets/Characters"));
            AssertForward(state);
            state.MoveSelection(-1);
            state.SetQuery("Player");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Characters"));
            Assert.That(state.Context.Path, Is.EqualTo("Assets"));
        }

        [Test]
        public void CategoriesAreNavigableAndMockCommandsNeverExecuteOrNavigate()
        {
            var state = CreateState();
            Choose(state, "Project");
            state.Enter(true);
            Choose(state, "Create (experimental category)");
            state.Enter(false);
            Choose(state, "Shapes (experimental category)");
            state.Enter(false);
            AssertForward(state);
            state.SetQuery("Cube");
            state.MoveSelection(1);
            Assert.That(state.Enter(true), Is.False);
            Assert.That(state.Message, Is.Empty);
            Assert.That(state.Enter(false), Is.False);
            Assert.That(state.Message, Does.Contain("nothing executed"));
            state.Back();
            Assert.That(state.Location, Is.EqualTo("Mock commands/Create"));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Shapes (experimental category)"));
            state.Back();
            state.SetQuery("Shapes/Cube");
            Assert.That(state.Results.Count, Is.EqualTo(1));
        }

        [Test]
        public void BackWithoutSelectedEntryDoesNotPickFirstResult()
        {
            var state = CreateState();
            Choose(state, "Hierarchy"); state.Enter(false);
            state.SetQuery("not found");
            Assert.That(state.Enter(true), Is.True);
            state.SetQuery("Inspect");
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.Back();
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Hierarchy"));
            state.SetQuery("Project");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void RecursiveCategoryReturnSelectsVisibleAncestor()
        {
            var state = CreateState();
            Choose(state, "Project"); state.Enter(true);
            state.SetQuery("Shapes");
            Choose(state, "Shapes (experimental category)"); state.Enter(false, browseOnly: true);
            AssertForward(state);
            state.Back();
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Create (experimental category)"));
            // A forward transition replaces the restored row with the first child of the next level.
            state.Enter(false);
            AssertForward(state);
        }

        [Test]
        public void ForwardSelectionDoesNotOverrideQueryOrBackRules()
        {
            var state = CreateState();
            state.SetQuery("Project"); state.Select(0); state.Enter(false);
            AssertForward(state);
            Choose(state, "Readme"); state.Enter(true);
            AssertForward(state);
            state.Back();
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Readme"));
            Assert.That(state.SelectedIndex, Is.EqualTo(1), "Back must not pick first instead of its entry");
            state.SetQuery("Characters");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.SetQuery("");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.Enter(true);
            AssertForward(state);
            state.Back();
            Assert.That(state.SelectedIndex, Is.EqualTo(-1), "No entry row was selected");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SmartEnterWithEmptyCommandsKeepsSelectionEmptyAndRestoresLeaf(bool altRight)
        {
            var leaf = new PaletteState.Item("Leaf", "Leaf");
            using var state = new PaletteState(new[] { leaf }, new PaletteState.Item("Commands", ""));
            state.Select(0);
            Assert.That(state.Enter(false, browseOnly: altRight), Is.True);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Results, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.Back();
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
            Assert.That(state.Enter(false), Is.False);
            state.SetQuery("");
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.MoveSelection(1);
            Assert.That(state.SelectedIndex, Is.Zero);
        }
    }
}


