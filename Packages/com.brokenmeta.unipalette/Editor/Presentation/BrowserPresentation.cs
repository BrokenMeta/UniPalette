using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Formatting contract only. Implementations are supplied through the presenter dependency.
    internal abstract class BrowserPresentation
    {
        internal abstract string Label { get; }
        internal abstract string SearchPlaceholder { get; }
        internal abstract string DataLabel { get; }
        internal abstract string DataTooltip { get; }
        internal abstract string ResultsTitle { get; }
        internal abstract string Hints { get; }
        
        internal abstract string GetBreadcrumb(PaletteBrowser browser);

        internal abstract string GetNotice(PaletteSession session);

        internal abstract PaletteRow Describe(PaletteItem item, PaletteItem context);
    }
}