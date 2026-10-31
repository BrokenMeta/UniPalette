using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Tests
{
    internal static class PaletteTestData
    {
        internal static PaletteState.Item Commands() => new PaletteState.Item("Commands", "Mock commands", false,
            new PaletteState.Item("Inspect (mock)", "Mock commands/Inspect"),
            new PaletteState.Item("Create (experimental category)", "Mock commands/Create", false,
                new PaletteState.Item("Empty Object (mock)", "Mock commands/Create/Empty Object"),
                new PaletteState.Item("Shapes (experimental category)", "Mock commands/Create/Shapes", false,
                    new PaletteState.Item("Cube (mock)", "Mock commands/Create/Shapes/Cube"))));

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
