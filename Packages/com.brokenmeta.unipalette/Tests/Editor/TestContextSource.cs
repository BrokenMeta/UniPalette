using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Tests
{
    internal sealed class TestContextSource : IContextSource
    {
        private readonly UnityContextSource _children = new();

        private readonly PaletteItem[] _roots;

        internal TestContextSource(PaletteItem[] roots) =>
            _roots = roots;

        public IEnumerable<PaletteItem> GetItems(PaletteItem context, bool recursive) =>
            context != null
                ? _children.GetItems(context, recursive)
                : _roots;

        public bool HasBrowseChildren(PaletteItem context) =>
            _children.HasBrowseChildren(context);
    }
}