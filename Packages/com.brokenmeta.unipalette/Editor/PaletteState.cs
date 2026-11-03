using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BrokenMeta.UniPalette.Tests")]

namespace BrokenMeta.UniPalette.Editor
{
    // Navigation owns levels and transient state; Editor data access stays in UnityContexts.
    internal sealed class PaletteState : IDisposable
    {
        internal enum Browser { Context, Commands }
        private readonly struct Level
        {
            internal readonly Browser Mode;
            internal readonly PaletteItem Context;
            internal readonly PaletteItem Category;
            internal Level(Browser mode, PaletteItem context, PaletteItem category = null)
            { Mode = mode; Context = context; Category = category; }
        }

        private readonly struct HistoryEntry
        {
            internal readonly Level Level;
            internal readonly PaletteItem Entry;
            internal HistoryEntry(Level level, PaletteItem entry) { Level = level; Entry = entry; }
        }

        private readonly Stack<HistoryEntry> history = new Stack<HistoryEntry>();
        private readonly PaletteItem[] roots;
        private PaletteItem commands;
        private readonly MenuCommands menuCommands;
        private bool selectFirstWhenLoaded;
        private bool openingCommands;
        internal bool ConfirmingContext => openingCommands;
        internal event Action CommandResultsChanged;
        internal event Action<PaletteItem, PaletteItem> ExecuteCommand;
        internal void ShowExecutionError(string error) => Message = error;
        internal bool CommandsLoading => menuCommands?.IsLoading == true;
        internal string CommandNotice => menuCommands?.Error.Length > 0 ? menuCommands.Error
            : CommandsLoading ? "Loading Unity menu commands…" : "Native execution · Confirmed context required · Unity determines applicability and result";
        private Level level;
        internal Browser Mode => level.Mode;
        internal PaletteItem Context => level.Context;
        internal bool CanGoBack => history.Count > 0;
        internal string Location => level.Category != null ? (level.Category.Path.Length == 0 ? "Commands" : level.Category.Path)
            : level.Context?.Path ?? "UniPalette / Choose a context";
        internal string Query { get; private set; } = "";
        internal int SelectedIndex { get; private set; } = -1;
        internal string Message { get; private set; } = "";
        internal readonly List<PaletteItem> Results = new List<PaletteItem>();

        internal PaletteState(PaletteItem[] fixtureRoots = null, PaletteItem commandRoot = null)
        {
            roots = fixtureRoots;
            commands = commandRoot;
            if (commands == null)
            {
                commands = new MenuCategoryItem("Commands", "", Array.Empty<PaletteItem>());
                menuCommands = new MenuCommands();
                menuCommands.Changed += OnCommandsChanged;
            }
            Rebuild();
        }

        private bool OpenCommands(PaletteItem context)
        {
            openingCommands = true;
            try
            {
                if (context is ContextItem confirmed && !confirmed.TryConfirm(out var error))
                { Message = error; return false; }
                // Paths are cached, availability is read afresh after Selection changes, never from labels.
                if (menuCommands != null) commands = menuCommands.ForContext(context);
            }
            finally { openingCommands = false; }
            selectFirstWhenLoaded = CommandsLoading;
            return Push(new Level(Browser.Commands, context, commands));
        }

        private void OnCommandsChanged()
        {
            if (openingCommands || Mode != Browser.Commands) return;
            UpdateCommands();
            Rebuild();
            Select(selectFirstWhenLoaded && Query.Length == 0 ? 0 : -1);
            selectFirstWhenLoaded = false;
            CommandResultsChanged?.Invoke();
        }

        private void UpdateCommands()
        {
            var path = level.Category?.Path ?? "";
            commands = menuCommands.ForContext(Context);
            level = new Level(Browser.Commands, Context, FindCategory(commands, path) ?? commands);
        }

        private static PaletteItem FindCategory(PaletteItem node, string path)
        {
            if (node.Path == path) return node;
            foreach (var child in node.Children)
                if (child.CanBrowse && (path == child.Path || path.StartsWith(child.Path + "/", StringComparison.Ordinal)))
                    return FindCategory(child, path);
            return null;
        }

        public void Dispose()
        {
            CommandResultsChanged = null;
            ExecuteCommand = null;
            menuCommands?.Dispose();
        }

        // Editor notifications rebuild the current area without restoring an old query/selection.
        internal void Refresh()
        {
            if (Context != null && !Context.IsValid)
            {
                ReturnToPreviousLevel();
                return;
            }
            if (Mode == Browser.Commands && menuCommands != null) UpdateCommands();
            SelectedIndex = -1;
            Rebuild();
        }

        private void ReturnToPreviousLevel()
        {
            HistoryEntry previous;
            do
            {
                previous = history.Count > 0 ? history.Pop() : default;
                level = previous.Level;
            }
            while (Context != null && !Context.IsValid);
            if (Mode == Browser.Commands && menuCommands != null) UpdateCommands();
            ResetTransient();
            if (previous.Entry == null || !previous.Entry.IsValid) return;
            var index = Results.FindIndex(item => item.SameIdentity(previous.Entry));
            // With an empty query Results contains immediate children, so only one visible branch can contain the entry.
            if (index < 0) index = Results.FindIndex(item => item.ContainsDescendant(previous.Entry));
            Select(index);
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

        internal bool Enter(bool control, bool browseOnly = false)
        {
            var selected = SelectedIndex >= 0 ? Results[SelectedIndex] : null;
            // Alt+Right shares Context Enter, but never previews or executes a command leaf.
            if (browseOnly && Mode == Browser.Commands && (selected == null || !selected.CanBrowse)) return false;
            if ((Context != null && !Context.IsValid) || (selected != null && !selected.IsValid))
            {
                Message = "Context no longer exists.";
                return false;
            }
            if (Mode == Browser.Commands)
            {
                // Ctrl+Enter in Command Browser is deliberately unassigned.
                if (control || selected == null) return false;
                if (selected.Children.Length > 0)
                    return Push(new Level(Browser.Commands, Context, selected));
                if (selected is MenuCommandItem && NativeExecution.Supports(selected.Path, Context))
                {
                    if (ExecuteCommand != null) ExecuteCommand(selected, Context);
                    else Message = "Native execution is unavailable; nothing executed.";
                }
                else Message = selected.Path + " · Execution requires a confirmed context and relevant menu leaf; nothing executed. " + selected.AvailabilityNote;
                return false;
            }
            if (control)
            {
                var context = selected ?? Context;
                return context != null && OpenCommands(context);
            }
            if (selected == null) return false;
            if (selected.SelectionEntry)
                return OpenCommands(selected);
            // Source entries always open their area, even when it is empty. A folder's
            // browse capability alone does not imply that it currently has children.
            var hasChildren = selected.HasBrowseChildren;
            return hasChildren ? Push(new Level(Browser.Context, selected)) : OpenCommands(selected);
        }

        internal bool Back()
        {
            if (!CanGoBack) return false;
            ReturnToPreviousLevel();
            return true;
        }

        private bool Push(Level next)
        {
            history.Push(new HistoryEntry(level, SelectedIndex >= 0 ? Results[SelectedIndex] : null));
            level = next;
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
            if (Mode == Browser.Context && Context != null && Context is ContextItem)
            {
                foreach (var item in UnityContexts.Children(Context, Query.Length > 0))
                    if (Matches(item)) Results.Add(item);
                return;
            }
            var items = level.Category?.Children ?? Context?.Children ?? roots ?? UnityContexts.Roots();
            AddMatches(items, Context != null || Mode == Browser.Commands);
        }

        private bool Matches(PaletteItem item) => Query.Length == 0
            || ((item.SelectionEntry ? "Current Selection " : "") + item.Title + " " + item.Path)
                .IndexOf(Query, StringComparison.OrdinalIgnoreCase) >= 0;

        private void AddMatches(PaletteItem[] items, bool recursive)
        {
            foreach (var item in items)
            {
                if (Matches(item))
                    Results.Add(item);
                // Prototype behavior: immediate children with an empty query.
                if (recursive && Query.Length > 0) AddMatches(item.Children, true);
            }
        }
    }
}
