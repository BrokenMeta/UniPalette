using System;
using BrokenMeta.UniPalette.Editor.Model;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Row text and identity are prepared up front. Icon targets are resolved for visible rows.
    internal readonly struct PaletteRow
    {
        internal readonly string _title;
        internal readonly string _subtitle;
        internal readonly string _tooltip;
        internal readonly string _iconName;
        internal readonly string _indicator;
        internal readonly string _indicatorTooltip;
        internal readonly PaletteItem _item;

        internal readonly Func<Object> _resolveIconTarget;

        internal PaletteRow(
            PaletteItem item,
            string subtitle,
            Func<Object> resolveIconTarget,
            string iconName,
            string indicator,
            string indicatorTooltip)
        {
            _title = item.SelectionEntry ? "Current Selection" : item.Title;
            _subtitle = subtitle;
            _tooltip = subtitle + (item.AvailabilityNote == null ? "" : "\n" + item.AvailabilityNote);
            _item = item;
            _resolveIconTarget = resolveIconTarget;
            _iconName = iconName;
            _indicator = indicator;
            _indicatorTooltip = indicatorTooltip;
        }
    }
}