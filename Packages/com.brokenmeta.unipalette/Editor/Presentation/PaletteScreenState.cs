using UnityEngine;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    internal sealed class PaletteScreenState
    {
        internal string Query { get; }

        internal string SearchPlaceholder { get; }

        internal string ModeLabel { get; }

        internal string ContextTitle { get; }

        internal string Breadcrumb { get; }

        internal Object ContextTarget { get; }

        internal string ContextIconName { get; }

        internal string DataLabel { get; }

        internal string DataTooltip { get; }

        internal string ResultsTitle { get; }

        internal string Hints { get; }

        internal bool CanGoBack { get; }

        internal PaletteRow[] Rows { get; }

        internal int SelectedIndex { get; }

        internal string Message { get; }

        internal PaletteScreenState(
            string query,
            string searchPlaceholder,
            string modeLabel,
            string contextTitle,
            string breadcrumb,
            Object contextTarget,
            string contextIconName,
            string dataLabel,
            string dataTooltip,
            string resultsTitle,
            string hints,
            bool canGoBack,
            PaletteRow[] rows,
            int selectedIndex,
            string message)
        {
            Query = query;
            SearchPlaceholder = searchPlaceholder;
            ModeLabel = modeLabel;
            ContextTitle = contextTitle;
            Breadcrumb = breadcrumb;
            ContextTarget = contextTarget;
            ContextIconName = contextIconName;
            DataLabel = dataLabel;
            DataTooltip = dataTooltip;
            ResultsTitle = resultsTitle;
            Hints = hints;
            CanGoBack = canGoBack;
            Rows = rows;
            SelectedIndex = selectedIndex;
            Message = message;
        }
    }
}
