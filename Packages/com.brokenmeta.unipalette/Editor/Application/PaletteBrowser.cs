using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Application
{
    // An immutable navigation level. The session owns history/query/selection; the view owns UI elements.
    internal abstract class PaletteBrowser
    {
        protected PaletteBrowser(PaletteItem context) =>
            Context = context;

        internal PaletteItem Context { get; }

        internal abstract string Location { get; }

        protected static bool Matches(PaletteItem item, string query)
        {
            if (query.Length == 0)
                return true;

            var selectionPrefix = item.SelectionEntry ? "Current Selection " : string.Empty;
            var searchText = $"{selectionPrefix}{item.Title} {item.Path}";

            return searchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected static IEnumerable<PaletteItem> Filter(PaletteItem[] items, string query, bool recursive)
        {
            foreach (var item in items)
            {
                if (Matches(item, query))
                    yield return item;

                if (!recursive || query.Length <= 0)
                    continue;

                foreach (var descendant in Filter(item.Children, query, true))
                    yield return descendant;
            }
        }

        internal abstract TResult Accept<TResult>(IPaletteBrowserVisitor<TResult> visitor);

        internal abstract IEnumerable<PaletteItem> GetItems(string query);

        internal abstract bool Activate(PaletteSession session, PaletteItem selected);

        internal virtual bool Browse(PaletteSession session, PaletteItem selected) =>
            session.ActivateSelected();

        internal virtual bool OpenContextCommands(PaletteSession session, PaletteItem selected) =>
            false;

        internal virtual PaletteBrowser RefreshCommands(Func<PaletteItem, PaletteItem> load) =>
            this;

        internal virtual PaletteBrowser OnCommandsChanged(Func<PaletteItem, PaletteItem> load) =>
            null;
    }
}