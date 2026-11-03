using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Tests
{
    internal static class PaletteTestData
    {
        internal static PaletteItem Commands() => new FixtureItem("Commands", "Mock commands", false,
            new FixtureItem("Inspect (mock)", "Mock commands/Inspect"),
            new FixtureItem("Create (experimental category)", "Mock commands/Create", false,
                new FixtureItem("Empty Object (mock)", "Mock commands/Create/Empty Object"),
                new FixtureItem("Shapes (experimental category)", "Mock commands/Create/Shapes", false,
                    new FixtureItem("Cube (mock)", "Mock commands/Create/Shapes/Cube"))));

        internal static void Install(UniPaletteWindow window, PaletteState state)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = typeof(UniPaletteWindow).GetField("state", flags);
            ((PaletteState)field.GetValue(window))?.Dispose();
            field.SetValue(window, state);
            window.rootVisualElement.Q<ListView>("results").itemsSource = state.Results;
            typeof(UniPaletteWindow).GetMethod("Render", flags).Invoke(window, new object[] { true });
        }
    }
}
