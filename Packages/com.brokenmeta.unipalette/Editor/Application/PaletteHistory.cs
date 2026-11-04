using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Application
{
    // Browser snapshots and entry identity only; no query, loading, Selection or UI mutations.
    internal sealed class PaletteHistory
    {
        private readonly Stack<Entry> _entries = new();

        private readonly PaletteBrowser _root;

        internal PaletteHistory(PaletteBrowser root) =>
            _root = root;

        internal bool CanGoBack => _entries.Count > 0;

        internal void Push(PaletteBrowser browser, PaletteItem item)
        {
            _entries.Push(new Entry(browser, item));
        }

        internal Entry GoBack()
        {
            while (_entries.Count > 0)
            {
                var previous = _entries.Pop();

                if (previous._browser.Context == null || previous._browser.Context.IsValid)
                    return previous;
            }

            return new Entry(_root, null);
        }

        internal readonly struct Entry
        {
            private readonly PaletteItem _item;

            internal readonly PaletteBrowser _browser;

            internal Entry(PaletteBrowser browser, PaletteItem item)
            {
                _browser = browser;
                _item = item;
            }

            internal int FindSelection(List<PaletteItem> results)
            {
                var entry = _item;

                if (entry == null || !entry.IsValid)
                    return -1;

                var index = results.FindIndex(candidate => candidate.SameIdentity(entry));

                // With an empty query, restore the visible containing branch if the entry is deeper.
                return index >= 0 ? index : results.FindIndex(candidate => candidate.ContainsDescendant(entry));
            }
        }
    }
}