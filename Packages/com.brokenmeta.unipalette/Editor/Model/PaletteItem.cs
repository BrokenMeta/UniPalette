using System;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Model
{
    internal abstract class PaletteItem
    {
        internal abstract string Title { get; }
        internal abstract string Path { get; }
        internal virtual bool IsValid => true;
        internal virtual Object Target => null;
        internal virtual bool SelectionEntry => false;
        internal virtual string AvailabilityNote => null;
        internal virtual PaletteItem[] Children => Array.Empty<PaletteItem>();
        internal virtual bool CanBrowse => Children.Length > 0;
        internal virtual string ContextTitle => Title;
        internal virtual string IconName => "Folder Icon";

        internal virtual bool SameIdentity(PaletteItem other) =>
            ReferenceEquals(this, other);

        internal virtual bool ContainsDescendant(PaletteItem other) =>
            false;

        internal virtual bool IsMenuRelevant(string path) =>
            false;
    }
}
