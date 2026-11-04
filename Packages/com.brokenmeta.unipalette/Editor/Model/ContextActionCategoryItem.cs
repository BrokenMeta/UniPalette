namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class ContextActionCategoryItem : PaletteItem
    {
        internal override string Title { get; }

        internal override string Path => Title;

        internal override PaletteItem[] Children { get; }

        internal override bool CanBrowse => true;

        internal override string AvailabilityNote =>
            "Type-specific contextual actions, separate from main-menu commands.";

        internal ContextActionCategoryItem(string title, PaletteItem[] children)
        {
            Title = title;
            Children = children;
        }

        internal override bool SameIdentity(PaletteItem other) =>
            other is ContextActionCategoryItem item && Title == item.Title;

        internal override bool ContainsDescendant(PaletteItem other) =>
            other is ContextActionItem item && item.GroupTitle == Title;
    }
}
