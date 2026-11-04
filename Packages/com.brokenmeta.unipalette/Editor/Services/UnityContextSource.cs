using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Resolve roots on each request rather than caching the Editor Selection.
    internal sealed class UnityContextSource : IContextSource
    {
        public IEnumerable<PaletteItem> GetItems(PaletteItem context, bool recursive) =>
            context != null ? context.GetChildren(recursive) : UnityContexts.Roots();
    }
}
