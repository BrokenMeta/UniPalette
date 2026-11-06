using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Each presenter receives its own selector and formatting instances from the host.
    internal sealed class BrowserPresentationSelector : IPaletteBrowserVisitor<BrowserPresentation>
    {
        private readonly BrowserPresentation _commands = new CommandPresentation();

        public BrowserPresentation Visit(ContextBrowser browser) =>
            new ContextPresentation(browser);

        public BrowserPresentation Visit(CommandBrowser browser) =>
            _commands;

        private sealed class ContextPresentation : BrowserPresentation
        {
            private readonly ContextBrowser _browser;

            internal override string Label => "Context";
            internal override string SearchPlaceholder => "Search contexts...";
            internal override string DataLabel => "Unity Editor";

            internal override string DataTooltip =>
                "Live scene objects and project assets. Single-object Selection only.";

            internal override string ResultsTitle => "Contexts";

            internal override string Hints =>
                "\u2191\u2193 Select \u00B7 Enter Browse / Commands \u00B7 Ctrl+Enter Commands \u00B7 Esc Back";

            internal ContextPresentation(ContextBrowser browser) =>
                _browser = browser;

            private (string Text, string Tooltip) DescribeActivation(PaletteItem item)
            {
                if (item.SelectionEntry)
                    return ("\u21B5", "Open selection commands");

                return _browser.BrowsesOnActivate(item)
                    ? ("\u203A", "Enter to browse \u00B7 Ctrl+Enter for commands")
                    : ("\u2197", "Enter for commands");
            }

            internal override string GetBreadcrumb(PaletteBrowser browser) =>
                browser.Context?.Path
                ?? "Open scenes / Assets";

            internal override string GetNotice(PaletteSession session) =>
                session.Results.Count == 0
                    ? "No results"
                    : "";

            internal override PaletteRow Describe(PaletteResult result, PaletteItem context)
            {
                var item = result.Item;
                return new PaletteRow(
                    result,
                    item.SelectionEntry
                        ? result.Title
                        + " \u00B7 "
                        + result.Path
                        : result.Path,
                    () => item.Target,
                    item.IconName,
                    resolveIndicator: () => DescribeActivation(item)
                );
            }
        }

        private sealed class CommandPresentation : BrowserPresentation
        {
            internal override string Label => "Commands";
            internal override string SearchPlaceholder => "Search commands...";
            internal override string DataLabel => "Unity commands";

            internal override string DataTooltip =>
                "Main-menu commands and separate contextual action groups for scene objects and assets. "
                + "An existing Scene View is required for contextual actions. "
                + "Scene Root uses no Selection and the active main-stage scene; Assets Root selects Assets. "
                + "Unity determines applicability and results; special window/UI state may still be required.";

            internal override string ResultsTitle => "Commands";
            internal override string Hints => "\u2191\u2193 Select \u00B7 Enter Open / Run \u00B7 Esc Back";

            internal override string GetBreadcrumb(PaletteBrowser browser) =>
                browser.Context.Path
                + " / "
                + browser.Location;

            internal override string GetNotice(PaletteSession session) =>
                session.CommandsLoading
                    ? session.CommandNotice
                    : session.Results.Count == 0
                        ? "No menu commands found. "
                        + session.CommandNotice
                        : session.CommandNotice;

            internal override PaletteRow Describe(PaletteResult result, PaletteItem context)
            {
                var item = result.Item;

                if (item is ContextActionItem action)
                {
                    return new PaletteRow(
                        result,
                        result.Path + " \u00B7 Contextual action",
                        null,
                        "TextAsset Icon",
                        action.IsEnabled ? "\u21B5" : "\u2014",
                        action.IsEnabled
                            ? "Run contextual action; Unity may defer execution"
                            : "Disabled in the contextual snapshot; rechecked before execution"
                    );
                }

                var container = item.CanBrowse;
                var relevantMenuCommand = !container
                    && item is MenuCommandItem
                    && new MenuRelevance(result.Path).Matches(context);

                var subtitle = item.SelectionEntry
                    ? result.Title + " \u00B7 " + result.Path
                    : result.Path
                    + (!container
                        ? relevantMenuCommand
                            ? " \u00B7 Unity menu command"
                            : " \u00B7 Execution unavailable"
                        : "");

                return new PaletteRow(
                    result,
                    subtitle,
                    null,
                    container ? "Folder Icon" : "TextAsset Icon",
                    container
                        ? "\u203A"
                        : relevantMenuCommand
                            ? "\u21B5"
                            : "\u2014",
                    container
                        ? "Open menu category"
                        : relevantMenuCommand
                            ? "Enter to run \u00B7 Availability checked at launch"
                            : "A relevant Unity menu command is required"
                );
            }
        }
    }
}