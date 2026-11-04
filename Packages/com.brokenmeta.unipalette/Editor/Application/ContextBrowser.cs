using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor.Application
{
    internal sealed class ContextBrowser : PaletteBrowser
    {
        private readonly PaletteItem[] roots;
        internal ContextBrowser(PaletteItem context = null, PaletteItem[] roots = null) : base(context)
        { this.roots = roots; }
        internal override string Location => Context?.Path ?? "UniPalette / Choose a context";

        internal override TResult Accept<TResult>(IPaletteBrowserVisitor<TResult> visitor) => visitor.Visit(this);

        internal override IEnumerable<PaletteItem> GetItems(string query)
        {
            if (Context is ContextItem)
            {
                foreach (var item in UnityContexts.Children(Context, query.Length > 0))
                    if (Matches(item, query)) yield return item;
            }
            else
                foreach (var item in Filter(Context?.Children ?? roots ?? UnityContexts.Roots(), query, Context != null))
                    yield return item;
        }

        internal override bool Activate(PaletteSession session, PaletteItem selected)
        {
            if (selected == null) return false;
            if (selected.SelectionEntry) return session.OpenCommands(selected);
            // Source entries open their area even when empty; empty folders open commands.
            return selected.HasBrowseChildren
                ? session.Push(new ContextBrowser(selected)) : session.OpenCommands(selected);
        }

        internal override bool OpenContextCommands(PaletteSession session, PaletteItem selected)
        {
            var context = selected ?? Context;
            return context != null && session.OpenCommands(context);
        }

    }
}
