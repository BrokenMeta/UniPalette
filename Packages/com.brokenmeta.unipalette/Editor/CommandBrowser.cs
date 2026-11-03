using System;
using System.Collections.Generic;

namespace BrokenMeta.UniPalette.Editor
{
    internal sealed class CommandBrowser : PaletteBrowser
    {
        internal PaletteItem Category { get; }
        internal CommandBrowser(PaletteItem context, PaletteItem category)
            : base(context ?? throw new ArgumentNullException(nameof(context)))
        { Category = category ?? throw new ArgumentNullException(nameof(category)); }
        internal override string Label => "Commands";
        internal override string SearchPlaceholder => "Search commands...";
        internal override string DataLabel => "Unity menu";
        internal override string DataTooltip => "Native execution for discovered, enabled commands in a confirmed context. Scene Root uses no Selection and the active main-stage scene; Assets Root selects Assets. Unity determines applicability and results; special window/UI state may still be required.";
        internal override string ResultsTitle => "Commands";
        internal override string Hints => "↑↓ Select · Enter Open / Run · Esc Back";
        internal override string Location => Category.Path.Length == 0 ? "Commands" : Category.Path;
        internal override string Breadcrumb => Context.Path + " / " + Location;

        internal override IEnumerable<PaletteItem> GetItems(string query) => Filter(Category.Children, query, true);

        internal override bool Activate(PaletteState state, PaletteItem selected)
        {
            if (selected == null) return false;
            if (selected.Children.Length > 0) return state.Push(new CommandBrowser(Context, selected));
            if (selected is MenuCommandItem && NativeExecution.Supports(selected.Path, Context))
                state.DispatchCommand(selected, Context);
            else state.ShowExecutionError(selected.Path
                + " · Execution requires a confirmed context and relevant menu leaf; nothing executed. " + selected.AvailabilityNote);
            return false;
        }

        // Alt+Right may open a category, but must never activate a command leaf.
        internal override bool Browse(PaletteState state, PaletteItem selected) =>
            selected != null && selected.CanBrowse && state.ActivateSelected();

        // Ctrl+Enter remains unassigned through the base implementation.
        internal override PaletteBrowser RefreshCommands(Func<PaletteItem, PaletteItem> load)
        {
            var root = load(Context);
            return new CommandBrowser(Context, FindCategory(root, Category.Path) ?? root);
        }

        internal override PaletteBrowser OnCommandsChanged(Func<PaletteItem, PaletteItem> load) => RefreshCommands(load);

        private static PaletteItem FindCategory(PaletteItem node, string path)
        {
            if (node.Path == path) return node;
            foreach (var child in node.Children)
                if (child.CanBrowse && (path == child.Path || path.StartsWith(child.Path + "/", StringComparison.Ordinal)))
                    return FindCategory(child, path);
            return null;
        }

        internal override string GetNotice(PaletteState state) => state.CommandsLoading ? state.CommandNotice
            : state.Results.Count == 0 ? "No menu commands found. " + state.CommandNotice : state.CommandNotice;

        internal override PaletteRow Describe(PaletteItem item)
        {
            var container = item.CanBrowse;
            var supported = !container && NativeExecution.Supports(item.Path, Context);
            var subtitle = item.SelectionEntry ? item.Title + " · " + item.Path : item.Path
                + (!container ? supported ? " · Native execution" : " · Execution unavailable" : "");
            return new PaletteRow(item, subtitle, null, container ? "Folder Icon" : "TextAsset Icon",
                container ? "›" : supported ? "↵" : "—",
                container ? "Open menu category" : supported ? "Run native Unity command"
                    : "Relevant confirmed context required · No execution");
        }
    }
}
