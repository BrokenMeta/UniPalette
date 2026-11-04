using BrokenMeta.UniPalette.Editor.Application;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    public abstract class NavigationTestFixture
    {
        private protected static PaletteSession CreateSession(bool hasMockSelection = true)
        {
            var player = new FixtureItem(
                "Player",
                "Scene Root/Player",
                false,
                new FixtureItem(
                    "Weapon",
                    "Scene Root/Player/Weapon",
                    false,
                    new FixtureItem(
                        "Muzzle",
                        "Scene Root/Player/Weapon/Muzzle"
                    )
                ),
                new FixtureItem(
                    "Camera",
                    "Scene Root/Player/Camera"
                )
            );

            var hierarchy = new FixtureItem(
                "Hierarchy",
                "Scene Root",
                false,
                player,
                new FixtureItem(
                    "Light",
                    "Scene Root/Light"
                )
            );

            var project = new FixtureItem(
                "Project",
                "Assets",
                false,
                new FixtureItem(
                    "Characters",
                    "Assets/Characters",
                    false,
                    new FixtureItem(
                        "Materials",
                        "Assets/Characters/Materials",
                        false,
                        new FixtureItem(
                            "Player Material",
                            "Assets/Characters/Materials/Player Material"
                        )
                    ),
                    new FixtureItem(
                        "Player Prefab",
                        "Assets/Characters/Player Prefab"
                    )
                ),
                new FixtureItem(
                    "Readme",
                    "Assets/Readme"
                )
            );

            var roots = hasMockSelection
                ? new[] { new FixtureItem("Current Selection (mock: Player)", player.Path, true), hierarchy, project, }
                : new[] { hierarchy, project, };

            return PaletteTestData.CreateSession(roots, PaletteTestData.Commands());
        }

        private protected static void Choose(PaletteSession session, string title)
        {
            var index = session.FindResultIndex(item => item.Title == title);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), title);
            session.Select(index);
        }

        private protected static void AssertForward(PaletteSession session)
        {
            Assert.That(session.Query.IsEmpty, Is.True);
            Assert.That(session.SelectedIndex, Is.EqualTo(session.Results.Count > 0 ? 0 : -1));
        }
    }
}