using BrokenMeta.UniPalette.Editor.Model;
using UnityEngine;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Data only: no App UI elements, callbacks or window lifetime.
    internal readonly struct PaletteRow
    {
        internal readonly string _title;
        internal readonly string _subtitle;
        internal readonly string _tooltip;
        internal readonly string _iconName;
        internal readonly string _indicator;
        internal readonly string _indicatorTooltip;
        internal readonly Object _target;

        internal PaletteRow(
            PaletteItem item,
            string subtitle,
            Object target,
            string iconName,
            string indicator,
            string indicatorTooltip)
        {
            _title = item.SelectionEntry ? "Current Selection" : item.Title;
            _subtitle = subtitle;
            _tooltip = subtitle + (item.AvailabilityNote == null ? "" : "\n" + item.AvailabilityNote);
            _target = target;
            _iconName = iconName;
            _indicator = indicator;
            _indicatorTooltip = indicatorTooltip;
        }
    }
}