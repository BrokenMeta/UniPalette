namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class MenuCategoryItem : MenuItem
    {
        internal MenuCategoryItem(
            string title,
            string path,
            PaletteItem[] children,
            string availabilityNote = "Menu category")
            : base(title, path, availabilityNote) =>
            Children = children;

        internal override PaletteItem[] Children { get; }
    }
}