using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Application
{
    // Text captured during one results rebuild. The item still supplies live identity and targets.
    internal readonly struct PaletteResult
    {
        internal PaletteItem Item { get; }

        internal string Title { get; }

        internal string Path { get; }

        internal PaletteResult(PaletteItem item)
        {
            Item = item;
            Title = item.Title;
            Path = item.Path;
        }
    }
}
