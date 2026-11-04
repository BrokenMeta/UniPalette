using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using Unity.AppUI.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Clickable = UnityEngine.UIElements.Clickable;
using Text = Unity.AppUI.UI.Text;
using ListView = UnityEngine.UIElements.ListView;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Owns the visual tree and its callbacks; navigation belongs to the session.
    internal sealed class PaletteView : IDisposable
    {
        private const string UIPath = "Packages/com.brokenmeta.unipalette/Editor/Presentation/UI/";

        private readonly VisualElement _root;
        private readonly Dictionary<VisualElement, Clickable> _rowClicks = new();
        private ActionButton _back;
        private Image _contextIcon;
        private Text _contextLabel;
        private Text _contextTitle;
        private Text _dataLabel;
        private bool _disposed;
        private Text _hints;
        private Text _messageLabel;
        private Text _modeLabel;
        private Panel _panel;
        private IVisualElementScheduledItem _pendingSearchFocus;
        private BrowserPresentation _presentation;
        private ListView _results;
        private Text _resultsCount;
        private Text _resultsTitle;
        private SearchBar _searchBar;
        private PaletteSession _session;

        internal PaletteView(VisualElement root)
        {
            _root = root;
            LoadLayout();
            ConfigureTheme();
            FindElements();
            ConfigureResults();
            AttachCallbacks();
        }

        internal bool IsAttached => !_disposed && _searchBar.panel != null;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pendingSearchFocus?.Pause();
            _pendingSearchFocus = null;
            _back.clickable.clicked -= OnBack;
            _results.selectionChanged -= OnRowSelectionChanged;
            _searchBar.UnregisterCallback<ChangingEvent<string>>(OnQueryChanging);
            _searchBar.UnregisterValueChangedCallback(OnQueryChanged);
            _panel.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _panel.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            _results.itemsSource = null;

            foreach (var row in new List<VisualElement>(_rowClicks.Keys))
                ReleaseRow(row);

            _results.makeItem = null;
            _results.bindItem = null;
            _results.destroyItem = null;
            BackRequested = null;
            QueryChanged = null;
            RowSelectionChanged = null;
            RowActivated = null;
            KeyPressed = null;
            _session = null;
            _presentation = null;
        }

        internal event Action BackRequested;
        internal event Action<string> QueryChanged;
        internal event Action<int> RowSelectionChanged;
        internal event Action<PaletteItem, EventBase> RowActivated;
        internal event Action<KeyDownEvent> KeyPressed;

        private void LoadLayout()
        {
            _root.Clear();

            AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>(UIPath + "UniPalette.uxml")
                .CloneTree(_root);

            _panel = _root.Q<Panel>();
        }

        private void ConfigureTheme()
        {
            _panel.theme = EditorGUIUtility.isProSkin ? "editor-dark" : "editor-light";
            _panel.scale = "small";

            _panel.styleSheets.Add(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss"
                )
            );

            _panel.styleSheets.Add(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/com.unity.dt.app-ui/PackageResources/Icons/Icons.uss"
                )
            );

            _panel.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UIPath + "UniPalette.uss"));
        }

        private void FindElements()
        {
            _back = _root.Q<ActionButton>("back");
            _modeLabel = _root.Q<Text>("mode");
            _contextLabel = _root.Q<Text>("context");
            _contextTitle = _root.Q<Text>("context-title");
            _contextIcon = _root.Q<Image>("context-icon");
            _resultsTitle = _root.Q<Text>("results-title");
            _resultsCount = _root.Q<Text>("results-count");
            _hints = _root.Q<Text>("hints");
            _messageLabel = _root.Q<Text>("message");
            _dataLabel = _root.Q<Text>("data-label");
            _searchBar = _root.Q<SearchBar>("search");
            _results = _root.Q<ListView>("results");
        }

        private void ConfigureResults()
        {
            _results.makeItem = MakeRow;
            _results.bindItem = BindRow;
            _results.destroyItem = ReleaseRow;
        }

        private void BindSession(PaletteSession next)
        {
            if (ReferenceEquals(_session, next))
                return;

            _session = next;
            // Rebinding is a render operation, not a user selection change.
            _results.selectionChanged -= OnRowSelectionChanged;

            try
            {
                _results.itemsSource = _session._results;
            }
            finally
            {
                _results.selectionChanged += OnRowSelectionChanged;
            }
        }

        private void AttachCallbacks()
        {
            _back.clickable.clicked += OnBack;
            _results.selectionChanged += OnRowSelectionChanged;
            _searchBar.RegisterCallback<ChangingEvent<string>>(OnQueryChanging);
            _searchBar.RegisterValueChangedCallback(OnQueryChanged);
            _panel.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _panel.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
        }

        private void OnBack()
        {
            if (!_disposed)
                BackRequested?.Invoke();
        }

        private void OnRowSelectionChanged(IEnumerable<object> selected)
        {
            if (!_disposed)
                RowSelectionChanged?.Invoke(_results.selectedIndex);
        }

        private void OnQueryChanging(ChangingEvent<string> evt)
        {
            if (!_disposed)
                QueryChanged?.Invoke(evt.newValue);
        }

        private void OnQueryChanged(ChangeEvent<string> evt)
        {
            if (!_disposed)
                QueryChanged?.Invoke(evt.newValue);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_disposed)
                KeyPressed?.Invoke(evt);
        }

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            if (!_disposed
                && (evt.direction == NavigationMoveEvent.Direction.Up
                    || evt.direction == NavigationMoveEvent.Direction.Down))
                evt.StopPropagation();
        }

        private void ActivateRow(ListViewItem row, EventBase evt)
        {
            if (!_disposed)
                RowActivated?.Invoke(row.userData as PaletteItem, evt);
        }

        private VisualElement MakeRow()
        {
            var row = new ListViewItem();
            row.AddToClassList("unipalette__row");
            row.optionsButton.style.display = DisplayStyle.None;
            row.focusable = false;
            // Clickable owns the down/up sequence and suppresses ListView's competing mouse handling.
            var clickable = new Clickable(evt => ActivateRow(row, evt));

            clickable.activators.Add(
                new ManipulatorActivationFilter { button = 0, modifiers = EventModifiers.Control, }
            );

            row.AddManipulator(clickable);
            _rowClicks.Add(row, clickable);
            var indicator = new Text { name = "indicator", pickingMode = PickingMode.Ignore, };
            indicator.AddToClassList("unipalette__row-indicator");
            row.trailingContainer.Add(indicator);

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (_disposed || _session == null)
                return;

            var item = _session._results[index];
            var row = (ListViewItem)element;
            row.userData = item;
            var description = _presentation.Describe(item, _session.Context);
            row.title = description._title;
            row.subtitle = description._subtitle;
            row.tooltip = description._tooltip;
            row.thumbnail = ItemIcon(description._target, description._iconName);
            var indicator = row.Q<Text>("indicator");
            indicator.text = description._indicator;
            indicator.tooltip = description._indicatorTooltip;
        }

        private static Texture ItemIcon(PaletteItem item) => ItemIcon(item?.Target, item?.IconName ?? "Search Icon");

        private static Texture ItemIcon(Object target, string iconName) => target
            ? EditorGUIUtility.ObjectContent(
                    target,
                    target.GetType()
                )
                .image
            : EditorGUIUtility.IconContent(
                    iconName
                )
                .image;

        private void ReleaseRow(VisualElement row)
        {
            if (_rowClicks.TryGetValue(row, out var clickable))
            {
                row.RemoveManipulator(clickable);
                _rowClicks.Remove(row);
            }

            row.userData = null;
        }

        internal void SelectRow(int index)
        {
            if (_disposed)
                return;

            _results.SetSelectionWithoutNotify(index < 0 ? Array.Empty<int>() : new[] { index, });
        }

        internal void RevealSelection(int index)
        {
            if (_disposed)
                return;

            SelectRow(index);

            if (index >= 0)
                _results.ScrollToItem(index);
        }

        internal void Render(PaletteSession session, BrowserPresentation presentation, bool focusSearch = true)
        {
            if (_disposed)
                return;

            _presentation = presentation;
            BindSession(session);
            _searchBar.SetValueWithoutNotify(session.Query);
            var browser = session.Browser;
            _searchBar.placeholder = presentation.SearchPlaceholder;
            _modeLabel.text = presentation.Label;
            _contextTitle.text = session.Context?.ContextTitle ?? "Choose a context";
            _contextLabel.text = presentation.GetBreadcrumb(browser);
            _contextLabel.tooltip = _contextLabel.text;
            _contextIcon.image = ItemIcon(session.Context);
            _dataLabel.text = presentation.DataLabel;
            _dataLabel.tooltip = presentation.DataTooltip;
            _resultsTitle.text = presentation.ResultsTitle;
            _hints.text = presentation.Hints;
            _back.SetEnabled(session.CanGoBack);
            _back.tooltip = session.CanGoBack ? "Back \u00B7 Escape / Alt+Left" : "Already at the root";
            RefreshResults(session);

            if (focusSearch)
                ScheduleSearchFocus();
        }

        internal void RefreshResults(PaletteSession session)
        {
            if (_disposed)
                return;

            BindSession(session);
            int selected = session.SelectedIndex;
            _results.SetSelectionWithoutNotify(Array.Empty<int>());
            _results.RefreshItems();
            session.Select(selected);

            _results.SetSelectionWithoutNotify(
                session.SelectedIndex < 0
                    ? Array.Empty<int>()
                    : new[] { session.SelectedIndex, }
            );

            if (session.SelectedIndex >= 0)
                _results.ScrollToItem(session.SelectedIndex);

            _resultsCount.text = session._results.Count.ToString();
            UpdateMessage(session);
        }

        internal void UpdateMessage(PaletteSession session)
        {
            if (_disposed)
                return;

            _messageLabel.text = session.Message.Length > 0 ? session.Message : _presentation.GetNotice(session);
            _messageLabel.style.display = _messageLabel.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal void FocusSearch()
        {
            if (!_disposed)
                _searchBar.Focus();
        }

        internal void ScheduleSearchFocus()
        {
            if (_disposed)
                return;

            _pendingSearchFocus?.Pause();

            _pendingSearchFocus = _root.schedule.Execute(
                () =>
                {
                    _pendingSearchFocus = null;
                    FocusSearch();
                }
            );
        }
    }
}