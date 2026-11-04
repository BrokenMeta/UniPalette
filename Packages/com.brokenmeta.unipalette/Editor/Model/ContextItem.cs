namespace BrokenMeta.UniPalette.Editor.Model
{
    internal abstract class ContextItem : PaletteItem
    {
        private readonly bool selectionEntry;
        protected ContextItem(bool selectionEntry = false) { this.selectionEntry = selectionEntry; }
        internal override bool SelectionEntry => selectionEntry;
        internal abstract T Accept<T>(IContextVisitor<T> visitor);
    }
}
