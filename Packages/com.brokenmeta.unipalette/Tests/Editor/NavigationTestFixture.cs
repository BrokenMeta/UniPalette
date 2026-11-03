using NUnit.Framework;
using BrokenMeta.UniPalette.Editor;

namespace BrokenMeta.UniPalette.Tests
{
    public abstract class NavigationTestFixture
    {

        private protected static PaletteState CreateState(bool hasMockSelection = true)
        {
            var player = new FixtureItem("Player", "Scene Root/Player", false,
                new FixtureItem("Weapon", "Scene Root/Player/Weapon", false,
                    new FixtureItem("Muzzle", "Scene Root/Player/Weapon/Muzzle")),
                new FixtureItem("Camera", "Scene Root/Player/Camera"));
            var hierarchy = new FixtureItem("Hierarchy", "Scene Root", false, player,
                new FixtureItem("Light", "Scene Root/Light"));
            var project = new FixtureItem("Project", "Assets", false,
                new FixtureItem("Characters", "Assets/Characters", false,
                    new FixtureItem("Materials", "Assets/Characters/Materials", false,
                        new FixtureItem("Player Material", "Assets/Characters/Materials/Player Material")),
                    new FixtureItem("Player Prefab", "Assets/Characters/Player Prefab")),
                new FixtureItem("Readme", "Assets/Readme"));
            var roots = hasMockSelection
                ? new[] { new FixtureItem("Current Selection (mock: Player)", player.Path, true), hierarchy, project }
                : new[] { hierarchy, project };
            return new PaletteState(roots, PaletteTestData.Commands());
        }

        private protected static void Choose(PaletteState state, string title)
        {
            var index = state.Results.FindIndex(item => item.Title == title);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), title);
            state.Select(index);
        }

        private protected static void AssertForward(PaletteState state)
        {
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(state.Results.Count > 0 ? 0 : -1));
        }
    }
}
