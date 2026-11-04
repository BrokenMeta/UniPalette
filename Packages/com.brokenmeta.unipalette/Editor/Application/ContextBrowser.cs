using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Application
{
    internal sealed class ContextBrowser : PaletteBrowser
    {
        private readonly IContextSource _source;

        internal ContextBrowser(IContextSource source, PaletteItem context = null)
            : base(context) => _source = source ?? throw new ArgumentNullException(nameof(source));

        internal override string Location => Context?.Path ?? "UniPalette / Choose a context";

        internal override TResult Accept<TResult>(IPaletteBrowserVisitor<TResult> visitor) => visitor.Visit(this);

        internal override IEnumerable<PaletteItem> GetItems(string query)
        {
            foreach (var item in _source.GetItems(Context, query.Length > 0))
            {
                if (Matches(item, query))
                    yield return item;
            }
        }

        internal override bool Activate(PaletteSession session, PaletteItem selected)
        {
            if (selected == null)
                return false;

            if (selected.SelectionEntry)
                return session.OpenCommands(selected);

            // Source entries open their area even when empty; empty folders open commands.
            return selected.HasBrowseChildren
                ? session.Push(new ContextBrowser(_source, selected))
                : session.OpenCommands(
                    selected
                );
        }

        internal override bool OpenContextCommands(PaletteSession session, PaletteItem selected)
        {
            var context = selected ?? Context;

            return context != null && session.OpenCommands(context);
        }
    }
}