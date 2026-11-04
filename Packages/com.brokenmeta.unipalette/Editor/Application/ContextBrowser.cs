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
        internal override string Label => "Context";
        internal override string SearchPlaceholder => "Search contexts...";
        internal override string DataLabel => "Unity Editor";
        internal override string DataTooltip => "Live scene objects and project assets. Single-object Selection only.";
        internal override string ResultsTitle => "Contexts";
        internal override string Hints => "↑↓ Select · Enter Browse / Commands · Ctrl+Enter Commands · Esc Back";
        internal override string Location => Context?.Path ?? "UniPalette / Choose a context";
        internal override string Breadcrumb => Context?.Path ?? "Open scenes / Assets";

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

        internal override string GetNotice(PaletteSession session) => session.Results.Count == 0 ? "No results" : "";

        internal override PaletteRow Describe(PaletteItem item) => new PaletteRow(item,
            item.SelectionEntry ? item.Title + " · " + item.Path : item.Path, item.Target, item.IconName,
            item.SelectionEntry ? "↵" : item.CanBrowse ? "›" : "↗",
            item.SelectionEntry ? "Open selection commands"
                : item.CanBrowse ? "Enter Browse / Commands · Ctrl+Enter for commands" : "Enter for commands");
    }
}
