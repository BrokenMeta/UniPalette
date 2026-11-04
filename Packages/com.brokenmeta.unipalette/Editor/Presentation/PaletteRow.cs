using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Data only: no App UI elements, callbacks or window lifetime.
    internal readonly struct PaletteRow
    {
        internal readonly string Title, Subtitle, Tooltip, IconName, Indicator, IndicatorTooltip;
        internal readonly UnityEngine.Object Target;
        internal PaletteRow(PaletteItem item, string subtitle, UnityEngine.Object target, string iconName,
            string indicator, string indicatorTooltip)
        {
            Title = item.SelectionEntry ? "Current Selection" : item.Title;
            Subtitle = subtitle;
            Tooltip = subtitle + (item.AvailabilityNote == null ? "" : "\n" + item.AvailabilityNote);
            Target = target;
            IconName = iconName;
            Indicator = indicator;
            IndicatorTooltip = indicatorTooltip;
        }
    }
}
