namespace BrokenMeta.UniPalette.Editor.Model
{
    // A descriptor from a contextual menu snapshot, not a main-menu path or a retained callback.
    internal sealed class ContextActionItem : PaletteItem
    {
        internal SceneObjectContext Context { get; }

        internal string Name { get; }

        internal bool IsEnabled { get; }

        internal override string Title => Name;

        internal override string Path => $"Scene View Actions/{Name}";

        internal override bool IsValid => Context != null && Context.IsValid;

        internal override string AvailabilityNote => IsEnabled
            ? "Scene View action; availability is rechecked before dispatch. Unity may defer execution."
            : "Disabled in the contextual snapshot; availability is rechecked before dispatch.";

        internal ContextActionItem(SceneObjectContext context, string name, bool isEnabled)
        {
            Context = context;
            Name = name;
            IsEnabled = isEnabled;
        }

        internal override bool SameIdentity(PaletteItem other) =>
            other is ContextActionItem item && Name == item.Name && Context.SameIdentity(item.Context);
    }
}
