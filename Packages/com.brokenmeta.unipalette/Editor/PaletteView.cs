using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using ListView = UnityEngine.UIElements.ListView;
using Text = Unity.AppUI.UI.Text;

namespace BrokenMeta.UniPalette.Editor
{
    // Owns the visual tree and its callbacks; navigation and execution stay in the window/state.
    internal sealed class PaletteView : IDisposable
    {
        private const string UIPath = "Packages/com.brokenmeta.unipalette/Editor/UI/";
        private SearchBar searchBar;
        private ListView results;
        private ActionButton back;
        private Text modeLabel;
        private Text contextLabel;
        private Text contextTitle;
        private Image contextIcon;
        private Text resultsTitle;
        private Text resultsCount;
        private Text hints;
        private Text messageLabel;
        private Text dataLabel;
        private PaletteState state;

        private readonly VisualElement root;
        private Panel panel;
        private bool disposed;
        private IVisualElementScheduledItem pendingSearchFocus;
        private readonly Dictionary<VisualElement, UnityEngine.UIElements.Clickable> rowClicks =
            new Dictionary<VisualElement, UnityEngine.UIElements.Clickable>();

        internal event Action BackRequested;
        internal event Action<string> QueryChanged;
        internal event Action<int> RowSelectionChanged;
        internal event Action<PaletteItem, EventBase> RowActivated;
        internal event Action<KeyDownEvent> KeyPressed;
        internal bool IsAttached => !disposed && searchBar.panel != null;

        internal PaletteView(VisualElement root)
        {
            this.root = root;
            LoadLayout();
            ConfigureTheme();
            FindElements();
            ConfigureResults();
            AttachCallbacks();
        }

        private void LoadLayout()
        {
            root.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UIPath + "UniPalette.uxml").CloneTree(root);
            panel = root.Q<Panel>();
        }

        private void ConfigureTheme()
        {
            panel.theme = EditorGUIUtility.isProSkin ? "editor-dark" : "editor-light";
            panel.scale = "small";
            panel.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss"));
            panel.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity.dt.app-ui/PackageResources/Icons/Icons.uss"));
            panel.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UIPath + "UniPalette.uss"));
        }

        private void FindElements()
        {
            back = root.Q<ActionButton>("back");
            modeLabel = root.Q<Text>("mode");
            contextLabel = root.Q<Text>("context");
            contextTitle = root.Q<Text>("context-title");
            contextIcon = root.Q<Image>("context-icon");
            resultsTitle = root.Q<Text>("results-title");
            resultsCount = root.Q<Text>("results-count");
            hints = root.Q<Text>("hints");
            messageLabel = root.Q<Text>("message");
            dataLabel = root.Q<Text>("data-label");
            searchBar = root.Q<SearchBar>("search");
            results = root.Q<ListView>("results");
        }

        private void ConfigureResults()
        {
            results.makeItem = MakeRow;
            results.bindItem = BindRow;
            results.destroyItem = ReleaseRow;
        }

        private void BindState(PaletteState next)
        {
            if (ReferenceEquals(state, next)) return;
            state = next;
            // Rebinding is a render operation, not a user selection change.
            results.selectionChanged -= OnRowSelectionChanged;
            try { results.itemsSource = state.Results; }
            finally { results.selectionChanged += OnRowSelectionChanged; }
        }

        private void AttachCallbacks()
        {
            back.clickable.clicked += OnBack;
            results.selectionChanged += OnRowSelectionChanged;
            searchBar.RegisterCallback<ChangingEvent<string>>(OnQueryChanging);
            searchBar.RegisterValueChangedCallback(OnQueryChanged);
            panel.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            panel.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
        }

        private void OnBack() { if (!disposed) BackRequested?.Invoke(); }
        private void OnRowSelectionChanged(IEnumerable<object> selected)
        { if (!disposed) RowSelectionChanged?.Invoke(results.selectedIndex); }
        private void OnQueryChanging(ChangingEvent<string> evt)
        { if (!disposed) QueryChanged?.Invoke(evt.newValue); }
        private void OnQueryChanged(ChangeEvent<string> evt)
        { if (!disposed) QueryChanged?.Invoke(evt.newValue); }
        private void OnKeyDown(KeyDownEvent evt)
        { if (!disposed) KeyPressed?.Invoke(evt); }
        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            if (!disposed && (evt.direction == NavigationMoveEvent.Direction.Up || evt.direction == NavigationMoveEvent.Direction.Down))
                evt.StopPropagation();
        }
        private void ActivateRow(ListViewItem row, EventBase evt)
        { if (!disposed) RowActivated?.Invoke(row.userData as PaletteItem, evt); }

        internal void SelectRow(int index)
        {
            if (disposed) return;
            results.SetSelectionWithoutNotify(index < 0 ? Array.Empty<int>() : new[] { index });
        }

        internal void RevealSelection(int index)
        {
            if (disposed) return;
            SelectRow(index);
            if (index >= 0) results.ScrollToItem(index);
        }

        internal void Render(PaletteState state, bool focusSearch = true)
        {
            if (disposed) return;
            BindState(state);
            searchBar.SetValueWithoutNotify(state.Query);
            var commands = state.Mode == PaletteState.Browser.Commands;
            searchBar.placeholder = commands ? "Search commands..." : "Search contexts...";
            modeLabel.text = commands ? "Commands" : "Context";
            var contextPath = state.Context?.Path;
            contextTitle.text = state.Context?.ContextTitle ?? "Choose a context";
            contextLabel.text = contextPath == null ? "Open scenes / Assets" : commands
                ? contextPath + " / " + state.Location : contextPath;
            contextLabel.tooltip = contextLabel.text;
            contextIcon.image = ItemIcon(state.Context);
            dataLabel.text = commands ? "Unity menu" : "Unity Editor";
            dataLabel.tooltip = commands ? "Native execution for discovered, enabled commands in a confirmed context. Scene Root uses no Selection and the active main-stage scene; Assets Root selects Assets. Unity determines applicability and results; special window/UI state may still be required."
                : "Live scene objects and project assets. Single-object Selection only.";
            resultsTitle.text = commands ? "Commands" : "Contexts";
            hints.text = commands ? "↑↓ Select · Enter Open / Run · Esc Back"
                : "↑↓ Select · Enter Browse / Commands · Ctrl+Enter Commands · Esc Back";
            back.SetEnabled(state.CanGoBack);
            back.tooltip = state.CanGoBack ? "Back · Escape / Alt+Left" : "Already at the root";
            RefreshResults(state);
            if (focusSearch) ScheduleSearchFocus();
        }

        internal void RefreshResults(PaletteState state)
        {
            if (disposed) return;
            BindState(state);
            var selected = state.SelectedIndex;
            results.SetSelectionWithoutNotify(System.Array.Empty<int>());
            results.RefreshItems();
            state.Select(selected);
            results.SetSelectionWithoutNotify(state.SelectedIndex < 0 ? System.Array.Empty<int>() : new[] { state.SelectedIndex });
            if (state.SelectedIndex >= 0) results.ScrollToItem(state.SelectedIndex);
            resultsCount.text = state.Results.Count.ToString();
            UpdateMessage(state);
        }

        internal void UpdateMessage(PaletteState state)
        {
            if (disposed) return;
            messageLabel.text = state.Message.Length > 0 ? state.Message
                : state.Mode == PaletteState.Browser.Commands
                    ? state.CommandsLoading ? state.CommandNotice
                        : state.Results.Count == 0 ? "No menu commands found. " + state.CommandNotice : state.CommandNotice
                    : state.Results.Count == 0 ? "No results" : "";
            messageLabel.style.display = messageLabel.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement MakeRow()
        {
            var row = new ListViewItem();
            row.AddToClassList("unipalette__row");
            row.optionsButton.style.display = DisplayStyle.None;
            row.focusable = false;
            // Clickable owns the down/up sequence and suppresses ListView's competing mouse handling.
            var clickable = new UnityEngine.UIElements.Clickable(evt => ActivateRow(row, evt));
            clickable.activators.Add(new ManipulatorActivationFilter { button = 0, modifiers = EventModifiers.Control });
            row.AddManipulator(clickable);
            rowClicks.Add(row, clickable);
            var indicator = new Text { name = "indicator", pickingMode = PickingMode.Ignore };
            indicator.AddToClassList("unipalette__row-indicator");
            row.trailingContainer.Add(indicator);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (disposed || state == null) return;
            var item = state.Results[index];
            var row = (ListViewItem)element;
            row.userData = item;
            var commands = state.Mode == PaletteState.Browser.Commands;
            row.title = item.SelectionEntry ? "Current Selection" : item.Title;
            var container = item.CanBrowse;
            var supported = commands && !container && NativeExecution.Supports(item.Path, state.Context);
            row.subtitle = item.SelectionEntry ? item.Title + " · " + item.Path : item.Path
                + (commands && !container ? supported ? " · Native execution" : " · Execution unavailable" : "");
            row.tooltip = row.subtitle + (item.AvailabilityNote == null ? "" : "\n" + item.AvailabilityNote);
            row.thumbnail = commands ? EditorGUIUtility.IconContent(container ? "Folder Icon" : "TextAsset Icon").image : ItemIcon(item);
            var indicator = row.Q<Text>("indicator");
            indicator.text = commands ? (container ? "›" : supported ? "↵" : "—") : item.SelectionEntry ? "↵" : container ? "›" : "↗";
            indicator.tooltip = commands ? (container ? "Open menu category" : supported ? "Run native Unity command" : "Relevant confirmed context required · No execution")
                : item.SelectionEntry ? "Open selection commands" : container ? "Enter Browse / Commands · Ctrl+Enter for commands" : "Enter for commands";
        }

        private static Texture ItemIcon(PaletteItem item)
        {
            var target = item?.Target;
            return target ? EditorGUIUtility.ObjectContent(target, target.GetType()).image
                : EditorGUIUtility.IconContent(item?.IconName ?? "Search Icon").image;
        }

        internal void FocusSearch()
        {
            if (!disposed) searchBar.Focus();
        }

        internal void ScheduleSearchFocus()
        {
            if (disposed) return;
            pendingSearchFocus?.Pause();
            pendingSearchFocus = root.schedule.Execute(() =>
            {
                pendingSearchFocus = null;
                FocusSearch();
            });
        }

        private void ReleaseRow(VisualElement row)
        {
            if (rowClicks.TryGetValue(row, out var clickable))
            {
                row.RemoveManipulator(clickable);
                rowClicks.Remove(row);
            }
            row.userData = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pendingSearchFocus?.Pause();
            pendingSearchFocus = null;
            back.clickable.clicked -= OnBack;
            results.selectionChanged -= OnRowSelectionChanged;
            searchBar.UnregisterCallback<ChangingEvent<string>>(OnQueryChanging);
            searchBar.UnregisterValueChangedCallback(OnQueryChanged);
            panel.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            panel.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            results.itemsSource = null;
            foreach (var row in new List<VisualElement>(rowClicks.Keys)) ReleaseRow(row);
            results.makeItem = null;
            results.bindItem = null;
            results.destroyItem = null;
            BackRequested = null;
            QueryChanged = null;
            RowSelectionChanged = null;
            RowActivated = null;
            KeyPressed = null;
            state = null;
        }
    }
}
