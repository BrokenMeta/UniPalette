namespace BrokenMeta.UniPalette.Editor.Model
{
    // A descriptor from a contextual menu snapshot, not a main-menu path or a retained callback.
    internal sealed class ContextActionItem : PaletteItem
    {
        internal ContextItem Context { get; }

        internal string Name { get; }

        internal string GroupTitle { get; }

        internal bool IsEnabled { get; }

        internal override string Title => Name;

        internal override string Path => $"{GroupTitle}/{Name}";

        internal override bool IsValid => Context != null && Context.IsValid;

        internal override string AvailabilityNote => IsEnabled
            ? "Contextual action; availability is rechecked before dispatch. Unity may defer execution."
            : "Disabled in the contextual snapshot; availability is rechecked before dispatch.";

        internal ContextActionItem(ContextItem context, string name, bool isEnabled, string groupTitle)
        {
            Context = context;
            Name = name;
            IsEnabled = isEnabled;
            GroupTitle = groupTitle;
        }

        internal override bool SameIdentity(PaletteItem other) =>
            other is ContextActionItem item
            && GroupTitle == item.GroupTitle
            && Name == item.Name
            && Context.SameIdentity(item.Context);
    }
}
