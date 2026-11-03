using System;
using System.Collections.Generic;

namespace BrokenMeta.UniPalette.Editor
{
    // An immutable navigation level. The state owns history/query/selection; the view owns UI elements.
    internal abstract class PaletteBrowser
    {
        internal PaletteItem Context { get; }
        protected PaletteBrowser(PaletteItem context) { Context = context; }
        internal abstract string Label { get; }
        internal abstract string SearchPlaceholder { get; }
        internal abstract string DataLabel { get; }
        internal abstract string DataTooltip { get; }
        internal abstract string ResultsTitle { get; }
        internal abstract string Hints { get; }
        internal abstract string Location { get; }
        internal abstract string Breadcrumb { get; }
        internal abstract IEnumerable<PaletteItem> GetItems(string query);
        internal abstract bool Activate(PaletteState state, PaletteItem selected);
        internal virtual bool Browse(PaletteState state, PaletteItem selected) => state.ActivateSelected();
        internal virtual bool OpenContextCommands(PaletteState state, PaletteItem selected) => false;
        internal virtual PaletteBrowser RefreshCommands(Func<PaletteItem, PaletteItem> load) => this;
        internal virtual PaletteBrowser OnCommandsChanged(Func<PaletteItem, PaletteItem> load) => null;
        internal abstract string GetNotice(PaletteState state);
        internal abstract PaletteRow Describe(PaletteItem item);

        protected static bool Matches(PaletteItem item, string query) => query.Length == 0
            || ((item.SelectionEntry ? "Current Selection " : "") + item.Title + " " + item.Path)
                .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        protected static IEnumerable<PaletteItem> Filter(PaletteItem[] items, string query, bool recursive)
        {
            foreach (var item in items)
            {
                if (Matches(item, query)) yield return item;
                if (recursive && query.Length > 0)
                    foreach (var descendant in Filter(item.Children, query, true)) yield return descendant;
            }
        }
    }

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
