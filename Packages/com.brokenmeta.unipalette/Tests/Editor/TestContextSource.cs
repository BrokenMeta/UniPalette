using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Tests
{
    internal sealed class TestContextSource : IContextSource
    {
        private readonly PaletteItem[] _roots;

        internal TestContextSource(PaletteItem[] roots) => _roots = roots;

        public IEnumerable<PaletteItem> GetItems(PaletteItem context, bool recursive) => context != null
            ? context.GetChildren(
                recursive
            )
            : _roots;
    }
}