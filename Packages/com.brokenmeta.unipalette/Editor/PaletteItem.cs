using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor
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
        internal virtual bool HasBrowseChildren => CanBrowse;
        internal virtual string ContextTitle => Title;
        internal virtual string IconName => "Folder Icon";
        internal virtual bool SameIdentity(PaletteItem other) => ReferenceEquals(this, other);
        internal virtual bool ContainsDescendant(PaletteItem other) => false;
        internal virtual bool IsMenuRelevant(string path) => false;

        internal virtual IEnumerable<PaletteItem> GetChildren(bool recursive)
        {
            foreach (var child in Children)
            {
                yield return child;
                if (recursive)
                    foreach (var descendant in child.GetChildren(true)) yield return descendant;
            }
        }
    }
}
