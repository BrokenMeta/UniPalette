using System.Collections.Generic;

namespace BrokenMeta.UniPalette.Editor
{
    // Browser snapshots and entry identity only; no query, loading, Selection or UI mutations.
    internal sealed class PaletteHistory
    {
        internal readonly struct Entry
        {
            internal readonly PaletteBrowser Browser;
            private readonly PaletteItem item;
            internal Entry(PaletteBrowser browser, PaletteItem item) { Browser = browser; this.item = item; }

            internal int FindSelection(List<PaletteItem> results)
            {
                var entry = item;
                if (entry == null || !entry.IsValid) return -1;
                var index = results.FindIndex(candidate => candidate.SameIdentity(entry));
                // With an empty query, restore the visible containing branch if the entry is deeper.
                return index >= 0 ? index : results.FindIndex(candidate => candidate.ContainsDescendant(entry));
            }
        }

        private readonly Stack<Entry> entries = new Stack<Entry>();
        private readonly PaletteBrowser root;
        internal bool CanGoBack => entries.Count > 0;

        internal PaletteHistory(PaletteBrowser root) { this.root = root; }

        internal void Push(PaletteBrowser browser, PaletteItem item) => entries.Push(new Entry(browser, item));

        internal Entry GoBack()
        {
            while (entries.Count > 0)
            {
                var previous = entries.Pop();
                if (previous.Browser.Context == null || previous.Browser.Context.IsValid) return previous;
            }
            return new Entry(root, null);
        }
    }
}
