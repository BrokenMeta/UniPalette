namespace BrokenMeta.UniPalette.Editor.Model
{
    internal abstract class ContextItem : PaletteItem
    {
        private readonly bool _selectionEntry;

        protected ContextItem(bool selectionEntry = false) =>
            _selectionEntry = selectionEntry;

        internal override bool SelectionEntry => _selectionEntry;

        internal abstract T Accept<T>(IContextVisitor<T> visitor);
    }
}