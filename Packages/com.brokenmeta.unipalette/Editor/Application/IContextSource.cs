using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Application
{
    internal interface IContextSource
    {
        // A null context requests the top level; recursive applies only within a context.
        IEnumerable<PaletteItem> GetItems(PaletteItem context, bool recursive);

        bool HasBrowseChildren(PaletteItem context);
    }
}