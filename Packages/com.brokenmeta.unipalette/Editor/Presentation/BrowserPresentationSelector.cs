using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Each presenter receives its own selector and formatting instances from the host.
    internal sealed class BrowserPresentationSelector : IPaletteBrowserVisitor<BrowserPresentation>
    {
        private readonly BrowserPresentation contexts = new ContextPresentation();
        private readonly BrowserPresentation commands = new CommandPresentation();

        public BrowserPresentation Visit(ContextBrowser browser) => contexts;
        public BrowserPresentation Visit(CommandBrowser browser) => commands;

        private sealed class ContextPresentation : BrowserPresentation
        {
            internal override string Label => "Context";
            internal override string SearchPlaceholder => "Search contexts...";
            internal override string DataLabel => "Unity Editor";
            internal override string DataTooltip => "Live scene objects and project assets. Single-object Selection only.";
            internal override string ResultsTitle => "Contexts";
            internal override string Hints => "↑↓ Select · Enter Browse / Commands · Ctrl+Enter Commands · Esc Back";
            internal override string GetBreadcrumb(PaletteBrowser browser) => browser.Context?.Path ?? "Open scenes / Assets";
            internal override string GetNotice(PaletteSession session) => session.Results.Count == 0 ? "No results" : "";

            internal override PaletteRow Describe(PaletteItem item, PaletteItem context) => new PaletteRow(item,
                item.SelectionEntry ? item.Title + " · " + item.Path : item.Path, item.Target, item.IconName,
                item.SelectionEntry ? "↵" : item.CanBrowse ? "›" : "↗",
                item.SelectionEntry ? "Open selection commands"
                    : item.CanBrowse ? "Enter Browse / Commands · Ctrl+Enter for commands" : "Enter for commands");
        }

        private sealed class CommandPresentation : BrowserPresentation
        {
            internal override string Label => "Commands";
            internal override string SearchPlaceholder => "Search commands...";
            internal override string DataLabel => "Unity menu";
            internal override string DataTooltip => "Native execution for discovered, enabled commands in a confirmed context. Scene Root uses no Selection and the active main-stage scene; Assets Root selects Assets. Unity determines applicability and results; special window/UI state may still be required.";
            internal override string ResultsTitle => "Commands";
            internal override string Hints => "↑↓ Select · Enter Open / Run · Esc Back";
            internal override string GetBreadcrumb(PaletteBrowser browser) => browser.Context.Path + " / " + browser.Location;
            internal override string GetNotice(PaletteSession session) => session.CommandsLoading ? session.CommandNotice
                : session.Results.Count == 0 ? "No menu commands found. " + session.CommandNotice : session.CommandNotice;

            internal override PaletteRow Describe(PaletteItem item, PaletteItem context)
            {
                var container = item.CanBrowse;
                var supported = !container && NativeExecution.Supports(item.Path, context);
                var subtitle = item.SelectionEntry ? item.Title + " · " + item.Path : item.Path
                    + (!container ? supported ? " · Native execution" : " · Execution unavailable" : "");
                return new PaletteRow(item, subtitle, null, container ? "Folder Icon" : "TextAsset Icon",
                    container ? "›" : supported ? "↵" : "—",
                    container ? "Open menu category" : supported ? "Run native Unity command"
                        : "Relevant confirmed context required · No execution");
            }
        }
    }
}
