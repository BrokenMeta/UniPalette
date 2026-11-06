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

        protected static bool Matches(PaletteResult result, SearchQuery query)
        {
            if (query.IsEmpty)
                return true;

            var selectionPrefix = result.Item.SelectionEntry ? "Current Selection " : string.Empty;
            var searchText = $"{selectionPrefix}{result.Title} {result.Path}";

            return query.Matches(searchText);
        }

        protected static IEnumerable<PaletteResult> Filter(PaletteItem[] items, SearchQuery query, bool recursive)
        {
            foreach (var item in items)
            {
                var result = new PaletteResult(item);

                if (Matches(result, query))
                    yield return result;

                if (!recursive || query.IsEmpty)
                    continue;

                foreach (var descendant in Filter(item.Children, query, true))
                    yield return descendant;
            }
        }

        internal abstract TResult Accept<TResult>(IPaletteBrowserVisitor<TResult> visitor);

        internal abstract IEnumerable<PaletteResult> GetItems(SearchQuery query);

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