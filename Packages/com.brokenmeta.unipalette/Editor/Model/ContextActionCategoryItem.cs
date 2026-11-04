namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class ContextActionCategoryItem : PaletteItem
    {
        internal override string Title => "Scene View Actions";

        internal override string Path => Title;

        internal override PaletteItem[] Children { get; }

        internal override bool CanBrowse => true;

        internal override string AvailabilityNote =>
            "Contextual actions from Scene View, separate from main-menu commands.";

        internal ContextActionCategoryItem(PaletteItem[] children) =>
            Children = children;

        internal override bool SameIdentity(PaletteItem other) =>
            other is ContextActionCategoryItem;

        internal override bool ContainsDescendant(PaletteItem other) =>
            other is ContextActionItem;
    }
}
