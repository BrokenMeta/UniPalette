using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Presentation;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Tests
{
    internal static class PaletteTestData
    {
        internal static PalettePresenter GetPresenter(UniPaletteWindow window) =>
            (PalettePresenter)typeof(UniPaletteWindow)
                .GetField(
                    "_presenter",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                )
                .GetValue(window);

        internal static PaletteSession CreateSession(
            PaletteItem[] fixtureRoots = null,
            PaletteItem commandRoot = null) =>
            new(
                new TestCommandSource(
                    commandRoot
                    ?? Commands()
                ),
                new UnityContextSelection(),
                fixtureRoots == null
                    ? new UnityContextSource()
                    : new TestContextSource(fixtureRoots)
            );

        internal static PaletteItem Commands() =>
            new FixtureItem(
                "Commands",
                "Mock commands",
                false,
                new FixtureItem(
                    "Inspect (mock)",
                    "Mock commands/Inspect"
                ),
                new FixtureItem(
                    "Create (experimental category)",
                    "Mock commands/Create",
                    false,
                    new FixtureItem(
                        "Empty Object (mock)",
                        "Mock commands/Create/Empty Object"
                    ),
                    new FixtureItem(
                        "Shapes (experimental category)",
                        "Mock commands/Create/Shapes",
                        false,
                        new FixtureItem(
                            "Cube (mock)",
                            "Mock commands/Create/Shapes/Cube"
                        )
                    )
                )
            );
    }
}