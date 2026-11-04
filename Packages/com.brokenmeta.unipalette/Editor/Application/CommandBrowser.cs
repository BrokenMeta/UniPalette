using System;
using System.Collections.Generic;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Application
{
    internal sealed class CommandBrowser : PaletteBrowser
    {
        internal CommandBrowser(PaletteItem context, PaletteItem category)
            : base(context ?? throw new ArgumentNullException(nameof(context))) =>
            Category = category ?? throw new ArgumentNullException(nameof(category));

        internal PaletteItem Category { get; }

        internal override string Location => Category.Path.Length == 0 ? "Commands" : Category.Path;

        private static PaletteItem FindCategory(PaletteItem node, string path)
        {
            if (node.Path == path)
                return node;

            return node
                .Children
                .Where(IsCategoryOnPath)
                .Select(child => FindCategory(child, path))
                .FirstOrDefault();

            bool IsCategoryOnPath(PaletteItem child) =>
                child.CanBrowse
                && (path == child.Path || path.StartsWith(child.Path + "/", StringComparison.Ordinal));
        }

        internal override TResult Accept<TResult>(IPaletteBrowserVisitor<TResult> visitor) =>
            visitor.Visit(this);

        internal override IEnumerable<PaletteItem> GetItems(string query) =>
            Filter(Category.Children, query, true);

        internal override bool Activate(PaletteSession session, PaletteItem selected)
        {
            if (selected == null)
                return false;

            if (selected.Children.Length > 0)
                return session.Push(new CommandBrowser(Context, selected));

            if (selected is MenuCommandItem)
                session.DispatchCommand(selected, Context);
            else
                session.ShowExecutionError(
                    selected.Path
                    + " \u00B7 Execution requires a confirmed context and relevant menu leaf; "
                    + "nothing executed. "
                    + selected.AvailabilityNote
                );

            return false;
        }

        // Alt+Right may open a category, but must never activate a command leaf.
        internal override bool Browse(PaletteSession session, PaletteItem selected) =>
            selected is { CanBrowse: true, }
            && session.ActivateSelected();

        // Ctrl+Enter remains unassigned through the base implementation.
        internal override PaletteBrowser RefreshCommands(Func<PaletteItem, PaletteItem> load)
        {
            var root = load(Context);

            return new CommandBrowser(Context, FindCategory(root, Category.Path) ?? root);
        }

        internal override PaletteBrowser OnCommandsChanged(Func<PaletteItem, PaletteItem> load) =>
            RefreshCommands(load);
    }
}