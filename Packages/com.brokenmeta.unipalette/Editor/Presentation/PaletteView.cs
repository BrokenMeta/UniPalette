using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;
using Unity.AppUI.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Clickable = UnityEngine.UIElements.Clickable;
using ListView = UnityEngine.UIElements.ListView;
using Object = UnityEngine.Object;
using Text = Unity.AppUI.UI.Text;

namespace BrokenMeta.UniPalette.Editor.Presentation
{
    // Owns the visual tree and its callbacks; navigation belongs to the session.
    internal sealed class PaletteView : IDisposable
    {
        private const string UIPath = "Packages/com.brokenmeta.unipalette/Editor/Presentation/UI/";

        private readonly SubscriptionScope _subscriptions = new();

        private readonly Signal _backRequested = new();

        private readonly Signal<string> _queryChanged = new();

        private readonly Signal<int> _rowSelectionChanged = new();

        private readonly Signal<PaletteItem> _activationRequested = new();

        private readonly Signal<PaletteItem> _contextCommandsRequested = new();

        private readonly Signal _browseRequested = new();

        private readonly Signal _dismissRequested = new();

        private readonly Signal<int> _selectionMoveRequested = new();

        private readonly VisualElement _root;
        private readonly Dictionary<VisualElement, Clickable> _rowClicks = new();
        private readonly List<PaletteRow> _rows = new();

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
        private ListView _results;
        private Text _resultsCount;
        private Text _resultsTitle;
        private SearchBar _searchBar;

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
            _subscriptions.Dispose();
            _results.itemsSource = null;

            foreach (var row in new List<VisualElement>(_rowClicks.Keys))
                ReleaseRow(row);

            _results.makeItem = null;
            _results.bindItem = null;
            _results.destroyItem = null;
            _backRequested.Dispose();
            _queryChanged.Dispose();
            _rowSelectionChanged.Dispose();
            _activationRequested.Dispose();
            _contextCommandsRequested.Dispose();
            _browseRequested.Dispose();
            _dismissRequested.Dispose();
            _selectionMoveRequested.Dispose();
            _rows.Clear();
        }

        internal IDisposable SubscribeBackRequested(Action callback) =>
            _backRequested.Subscribe(callback);

        internal IDisposable SubscribeQueryChanged(Action<string> callback) =>
            _queryChanged.Subscribe(callback);

        internal IDisposable SubscribeRowSelectionChanged(Action<int> callback) =>
            _rowSelectionChanged.Subscribe(callback);

        // A null target refers to the current selection. Clicks carry the bound item.
        internal IDisposable SubscribeActivationRequested(Action<PaletteItem> callback) =>
            _activationRequested.Subscribe(callback);

        internal IDisposable SubscribeContextCommandsRequested(Action<PaletteItem> callback) =>
            _contextCommandsRequested.Subscribe(callback);

        internal IDisposable SubscribeBrowseRequested(Action callback) =>
            _browseRequested.Subscribe(callback);

        internal IDisposable SubscribeDismissRequested(Action callback) =>
            _dismissRequested.Subscribe(callback);

        internal IDisposable SubscribeSelectionMoveRequested(Action<int> callback) =>
            _selectionMoveRequested.Subscribe(callback);

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
            _results.itemsSource = _rows;
        }

        private void AttachCallbacks()
        {
            Subscription
                .Create(
                    () => _back.clickable.clicked += OnBack,
                    () => _back.clickable.clicked -= OnBack
                )
                .AddTo(_subscriptions);

            Subscription
                .Create(
                    () => _results.selectionChanged += OnRowSelectionChanged,
                    () => _results.selectionChanged -= OnRowSelectionChanged
                )
                .AddTo(_subscriptions);

            Subscription
                .Create(
                    () => _searchBar.RegisterCallback<ChangingEvent<string>>(OnQueryChanging),
                    () => _searchBar.UnregisterCallback<ChangingEvent<string>>(OnQueryChanging)
                )
                .AddTo(_subscriptions);

            Subscription
                .Create(
                    () => _searchBar.RegisterValueChangedCallback(OnQueryChanged),
                    () => _searchBar.UnregisterValueChangedCallback(OnQueryChanged)
                )
                .AddTo(_subscriptions);

            Subscription
                .Create(
                    () => _panel.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown),
                    () => _panel.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown)
                )
                .AddTo(_subscriptions);

            Subscription
                .Create(
                    () => _panel.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown),
                    () => _panel.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown)
                )
                .AddTo(_subscriptions);
        }

        private void OnBack()
        {
            if (!_disposed)
                _backRequested.Publish();
        }

        private void OnRowSelectionChanged(IEnumerable<object> selected)
        {
            if (!_disposed)
                _rowSelectionChanged.Publish(_results.selectedIndex);
        }

        private void OnQueryChanging(ChangingEvent<string> evt)
        {
            if (!_disposed)
                _queryChanged.Publish(evt.newValue);
        }

        private void OnQueryChanged(ChangeEvent<string> evt)
        {
            if (!_disposed)
                _queryChanged.Publish(evt.newValue);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_disposed)
                return;

            var enter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;

            if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopImmediatePropagation();
                _dismissRequested.Publish();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.LeftArrow)
            {
                evt.StopImmediatePropagation();
                _backRequested.Publish();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.RightArrow)
            {
                evt.StopImmediatePropagation();

                if (!evt.ctrlKey && !evt.commandKey && !evt.shiftKey)
                    _browseRequested.Publish();
            }
            else if (!evt.ctrlKey
                && !evt.commandKey
                && !evt.shiftKey
                && (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow))
            {
                evt.StopImmediatePropagation();
                _selectionMoveRequested.Publish(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
            }
            else if (enter)
            {
                evt.StopImmediatePropagation();

                if (!evt.altKey && !evt.shiftKey && !evt.commandKey)
                    PublishActivation(null, evt.ctrlKey);
            }
            // Unmodified Left/Right are intentionally left to the text input.
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
            if (_disposed || !(row.userData is PaletteItem item))
                return;

            var pointer = evt as IPointerEvent;
            var mouse = evt as IMouseEvent;

            // A second click must not activate the row rebound beneath the first click.
            if ((pointer?.clickCount ?? mouse?.clickCount ?? 1) > 1)
                return;

            PublishActivation(item, pointer?.ctrlKey ?? mouse?.ctrlKey ?? false);
        }

        private void PublishActivation(PaletteItem item, bool openContextCommands)
        {
            if (openContextCommands)
                _contextCommandsRequested.Publish(item);
            else
                _activationRequested.Publish(item);
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
            if (_disposed)
                return;

            var description = _rows[index];
            var row = (ListViewItem)element;
            row.userData = description._item;
            row.title = description._title;
            row.subtitle = description._subtitle;
            row.tooltip = description._tooltip;
            row.thumbnail = ItemIcon(description._resolveIconTarget?.Invoke(), description._iconName);
            var indicator = row.Q<Text>("indicator");
            var activation = description.GetIndicator();
            indicator.text = activation.Text;
            indicator.tooltip = activation.Tooltip;
        }

        private static Texture ItemIcon(Object target, string iconName) =>
            target
                ? EditorGUIUtility.ObjectContent(
                        target,
                        target.GetType()
                    )
                    .image
                : EditorGUIUtility.IconContent(iconName)
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

        internal void ScrollToSelection(int index)
        {
            if (_disposed)
                return;

            if (index >= 0)
                _results.ScrollToItem(index);
        }

        internal void Render(PaletteScreenState state, bool focusSearch)
        {
            if (_disposed)
                return;

            _searchBar.SetValueWithoutNotify(state.Query);
            _searchBar.placeholder = state.SearchPlaceholder;
            _modeLabel.text = state.ModeLabel;
            _contextTitle.text = state.ContextTitle;
            _contextLabel.text = state.Breadcrumb;
            _contextLabel.tooltip = state.Breadcrumb;
            _contextIcon.image = ItemIcon(state.ContextTarget, state.ContextIconName);
            _dataLabel.text = state.DataLabel;
            _dataLabel.tooltip = state.DataTooltip;
            _resultsTitle.text = state.ResultsTitle;
            _hints.text = state.Hints;
            _back.SetEnabled(state.CanGoBack);
            _back.tooltip = state.CanGoBack ? "Back \u00B7 Escape / Alt+Left" : "Already at the root";
            RefreshResults(state.Rows, state.SelectedIndex, state.Message);

            if (focusSearch)
                ScheduleSearchFocus();
        }

        internal void RefreshResults(PaletteRow[] rows, int selectedIndex, string message)
        {
            if (_disposed)
                return;

            _rows.Clear();
            _rows.AddRange(rows);
            // Refreshing recycled rows is a render operation, not a user selection change.
            _results.selectionChanged -= OnRowSelectionChanged;
            try
            {
                _results.SetSelectionWithoutNotify(Array.Empty<int>());
                _results.RefreshItems();
                SelectRow(selectedIndex);
                ScrollToSelection(selectedIndex);
            }
            finally
            {
                if (!_disposed)
                    _results.selectionChanged += OnRowSelectionChanged;
            }

            _resultsCount.text = _rows.Count.ToString();
            UpdateMessage(message);
        }

        internal void UpdateMessage(string message)
        {
            if (_disposed)
                return;

            _messageLabel.text = message;
            _messageLabel.style.display = message.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
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