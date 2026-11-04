namespace BrokenMeta.UniPalette.Editor.Model
{
    // A descriptor from a contextual menu snapshot, not a main-menu path or a retained callback.
    internal sealed class ContextActionItem
    {
        internal SceneObjectContext Context { get; }

        internal string Name { get; }

        internal bool IsEnabled { get; }

        internal ContextActionItem(SceneObjectContext context, string name, bool isEnabled)
        {
            Context = context;
            Name = name;
            IsEnabled = isEnabled;
        }
    }
}
