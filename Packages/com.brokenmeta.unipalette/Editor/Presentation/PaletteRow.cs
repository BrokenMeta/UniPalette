using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Row text and identity are prepared up front. Live details are resolved when binding visible rows.
    internal readonly struct PaletteRow
    {
        private readonly (string Text, string Tooltip) _indicator;

        private readonly Func<(string Text, string Tooltip)> _resolveIndicator;

        internal readonly string _title;
        internal readonly string _subtitle;
        internal readonly string _tooltip;
        internal readonly string _iconName;
        internal readonly PaletteItem _item;

        internal readonly Func<Object> _resolveIconTarget;

        internal PaletteRow(
            PaletteResult result,
            string subtitle,
            Func<Object> resolveIconTarget,
            string iconName,
            string indicator = null,
            string indicatorTooltip = null,
            Func<(string Text, string Tooltip)> resolveIndicator = null)
        {
            var item = result.Item;
            _title = item.SelectionEntry ? "Current Selection" : result.Title;
            _subtitle = subtitle;
            _tooltip = subtitle + (item.AvailabilityNote == null ? "" : "\n" + item.AvailabilityNote);
            _item = item;
            _resolveIconTarget = resolveIconTarget;
            _iconName = iconName;
            _indicator = (indicator, indicatorTooltip);
            _resolveIndicator = resolveIndicator;
        }

        internal (string Text, string Tooltip) GetIndicator() =>
            _resolveIndicator?.Invoke() ?? _indicator;
    }
}