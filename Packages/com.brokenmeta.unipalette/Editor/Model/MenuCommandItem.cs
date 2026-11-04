namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class MenuCommandItem : MenuItem
    {
        internal MenuCommandItem(string title, string path, string availabilityNote = null)
            : base(title, path, availabilityNote) { }
    }
}