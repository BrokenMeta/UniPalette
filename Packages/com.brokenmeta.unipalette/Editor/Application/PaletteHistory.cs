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

        internal Entry PeekBack(out int entriesToPop)
        {
            entriesToPop = 0;

            foreach (var previous in _entries)
            {
                entriesToPop++;

                if (previous._browser.Context == null || previous._browser.Context.IsValid)
                    return previous;
            }

            return new Entry(_root, null);
        }

        internal void CommitBack(int entriesToPop)
        {
            for (var index = 0; index < entriesToPop; index++)
                _entries.Pop();
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

            internal int FindSelection(IReadOnlyList<PaletteResult> results)
            {
                var entry = _item;

                if (entry == null || !entry.IsValid)
                    return -1;

                for (var index = 0; index < results.Count; index++)
                {
                    if (results[index].Item.SameIdentity(entry))
                        return index;
                }

                // With an empty query, restore the visible containing branch if the entry is deeper.
                for (var index = 0; index < results.Count; index++)
                {
                    if (results[index].Item.ContainsDescendant(entry))
                        return index;
                }

                return -1;
            }
        }
    }
}