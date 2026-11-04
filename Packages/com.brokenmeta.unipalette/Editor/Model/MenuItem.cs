using System;

namespace BrokenMeta.UniPalette.Editor.Model
{
    internal abstract class MenuItem : PaletteItem
    {
        internal override string Title { get; }
        internal override string Path { get; }
        internal override string AvailabilityNote { get; }
        protected MenuItem(string title, string path, string availabilityNote)
        { Title = title; Path = path; AvailabilityNote = availabilityNote; }
        internal override bool SameIdentity(PaletteItem other) => other is MenuItem item && Path == item.Path;
        internal override bool ContainsDescendant(PaletteItem other) => CanBrowse && other is MenuItem item
            && item.Path.StartsWith(Path + "/", StringComparison.Ordinal);
    }
}
