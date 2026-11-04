using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BrokenMeta.UniPalette.Editor.Model;

[assembly: InternalsVisibleTo("BrokenMeta.UniPalette.Tests")]

namespace BrokenMeta.UniPalette.Editor.Application
{
    // Owns navigation history and transient state; each browser owns its level's behavior.
    internal sealed class PaletteSession : IDisposable
    {
        private readonly PaletteHistory history;
        private readonly ICommandSource commandSource;
        private bool selectFirstWhenLoaded;
        private bool openingCommands;
        private bool disposed;
        internal bool ConfirmingContext => openingCommands;
        internal event Action CommandResultsChanged;
        internal event Action<PaletteItem, PaletteItem> ExecuteCommand;
        internal void ShowExecutionError(string error) => Message = error;
        internal bool CommandsLoading => commandSource.IsLoading;
        internal string CommandNotice => commandSource.Error.Length > 0 ? commandSource.Error
            : CommandsLoading ? "Loading Unity menu commands…" : "Native execution · Confirmed context required · Unity determines applicability and result";
        internal PaletteBrowser Browser { get; private set; }
        internal PaletteItem Context => Browser.Context;
        internal bool CanGoBack => history.CanGoBack;
        internal string Location => Browser.Location;
        internal string Query { get; private set; } = "";
        internal int SelectedIndex { get; private set; } = -1;
        internal string Message { get; private set; } = "";
        internal readonly List<PaletteItem> Results = new List<PaletteItem>();

        internal PaletteSession(ICommandSource commandSource, PaletteItem[] fixtureRoots = null)
        {
            this.commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
            Browser = new ContextBrowser(roots: fixtureRoots);
            history = new PaletteHistory(Browser);
            Rebuild();
            commandSource.Changed += OnCommandsChanged;
        }

        private PaletteItem LoadCommands(PaletteItem context) => commandSource.ForContext(context);

        internal bool OpenCommands(PaletteItem context)
        {
            PaletteItem commands;
            openingCommands = true;
            try
            {
                if (context is ContextItem confirmed && !confirmed.TryConfirm(out var error))
                { Message = error; return false; }
                // Availability is read after Selection changes, never from cached search labels.
                commands = LoadCommands(context);
            }
            finally { openingCommands = false; }
            selectFirstWhenLoaded = CommandsLoading;
            return Push(new CommandBrowser(context, commands));
        }

        private void OnCommandsChanged()
        {
            if (disposed || openingCommands) return;
            var next = Browser.OnCommandsChanged(LoadCommands);
            if (next == null) return;
            Browser = next;
            Rebuild();
            Select(selectFirstWhenLoaded && Query.Length == 0 ? 0 : -1);
            selectFirstWhenLoaded = false;
            CommandResultsChanged?.Invoke();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            commandSource.Changed -= OnCommandsChanged;
            CommandResultsChanged = null;
            ExecuteCommand = null;
        }

        // Editor notifications rebuild the current area without restoring an old query/selection.
        internal void Refresh()
        {
            if (Context != null && !Context.IsValid)
            {
                ReturnToPreviousBrowser();
                return;
            }
            Browser = Browser.RefreshCommands(LoadCommands);
            SelectedIndex = -1;
            Rebuild();
        }

        private void ReturnToPreviousBrowser()
        {
            var previous = history.GoBack();
            Browser = previous.Browser.RefreshCommands(LoadCommands);
            ResetTransient();
            Select(previous.FindSelection(Results));
        }

        internal void SetQuery(string query)
        {
            query = query ?? "";
            if (query == Query) return;
            Query = query;
            selectFirstWhenLoaded = false;
            SelectedIndex = -1;
            Message = "";
            Rebuild();
        }

        internal void Select(int index) => SelectedIndex = index >= 0 && index < Results.Count ? index : -1;

        internal void MoveSelection(int direction)
        {
            if (Results.Count == 0) return;
            Select(SelectedIndex < 0 ? (direction > 0 ? 0 : Results.Count - 1)
                : Math.Max(0, Math.Min(Results.Count - 1, SelectedIndex + direction)));
        }

        private PaletteItem SelectedItem => SelectedIndex >= 0 ? Results[SelectedIndex] : null;

        internal bool ActivateSelected()
        {
            var selected = SelectedItem;
            return ValidateNavigationTarget(selected) && Browser.Activate(this, selected);
        }

        internal bool BrowseSelected() => Browser.Browse(this, SelectedItem);

        internal bool OpenContextCommands()
        {
            var selected = SelectedItem;
            return ValidateNavigationTarget(selected) && Browser.OpenContextCommands(this, selected);
        }

        private bool ValidateNavigationTarget(PaletteItem selected)
        {
            if ((Context == null || Context.IsValid) && (selected == null || selected.IsValid)) return true;
            Message = "Context no longer exists.";
            return false;
        }

        internal void DispatchCommand(PaletteItem command, PaletteItem context)
        {
            if (ExecuteCommand != null) ExecuteCommand(command, context);
            else Message = "Native execution is unavailable; nothing executed.";
        }

        internal bool GoBack()
        {
            if (!CanGoBack) return false;
            ReturnToPreviousBrowser();
            return true;
        }

        internal bool Push(PaletteBrowser next)
        {
            history.Push(Browser, SelectedItem);
            Browser = next;
            ResetTransient();
            Select(0);
            return true;
        }

        private void ResetTransient()
        {
            Query = "";
            SelectedIndex = -1;
            Message = "";
            Rebuild();
        }

        private void Rebuild()
        {
            Results.Clear();
            Results.AddRange(Browser.GetItems(Query));
        }
    }
}
