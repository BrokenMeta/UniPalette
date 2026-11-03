using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using ListView = UnityEngine.UIElements.ListView;
using Text = Unity.AppUI.UI.Text;

namespace BrokenMeta.UniPalette.Editor
{
    public class UniPaletteWindow : EditorWindow
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
        private UnityEngine.Object observedSelection;
        private int observedSelectionCount;
        private double nextContextCheck;
        private bool executing;

        private void OnEnable()
        {
            executing = false;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += RefreshContexts;
            EditorApplication.projectChanged += RefreshContexts;
            EditorApplication.update += CheckContextChanges;
        }

        private void OnDisable()
        {
            executing = true;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= RefreshContexts;
            EditorApplication.projectChanged -= RefreshContexts;
            EditorApplication.update -= CheckContextChanges;
            state?.Dispose();
        }

        private void OnFocus() => RefreshContexts();

        private void OnSelectionChanged()
        {
            // A delayed notification for the context we just confirmed is already reflected in the UI.
            if (ReferenceEquals(observedSelection, Selection.activeObject) && observedSelectionCount == Selection.count) return;
            RefreshContexts();
        }

        private void CheckContextChanges()
        {
            if (state == null || EditorApplication.timeSinceStartup < nextContextCheck) return;
            nextContextCheck = EditorApplication.timeSinceStartup + 0.1;
            // Unity 6.6 can change Selection through API without delivering selectionChanged.
            // Compare only the active object/count and current target, never search every tick.
            if (!ReferenceEquals(observedSelection, Selection.activeObject) || observedSelectionCount != Selection.count
                || (state.Context != null && !state.Context.IsValid)) RefreshContexts();
        }

        private void RefreshContexts()
        {
            if (!this || state == null || searchBar?.panel == null) return;
            observedSelection = Selection.activeObject;
            observedSelectionCount = Selection.count;
            if (state.ConfirmingContext) return;
            state.Refresh();
            Render(false);
        }

        [UnityEditor.MenuItem("Window/UniPalette")]
        [Shortcut("UniPalette/Open", KeyCode.U, ShortcutModifiers.Control | ShortcutModifiers.Alt)]
        private static void Open()
        {
            var isNewWindow = !HasOpenInstances<UniPaletteWindow>();
            var window = GetWindow<UniPaletteWindow>();
            if (isNewWindow)
            {
                var bounds = window.position;
                bounds.size = new Vector2(760, 460);
                bounds.center = EditorGUIUtility.GetMainWindowPosition().center;
                window.position = bounds;
            }
            window.titleContent = new GUIContent("UniPalette");
            window.Show();
            window.Focus();
            window.ScheduleSearchFocus();
        }

        private void CreateGUI()
        {
            state?.Dispose();
            titleContent = new GUIContent("UniPalette");
            minSize = new Vector2(360, 240);
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UIPath + "UniPalette.uxml").CloneTree(rootVisualElement);
            var panel = rootVisualElement.Q<Panel>();
            panel.theme = EditorGUIUtility.isProSkin ? "editor-dark" : "editor-light";
            panel.scale = "small";
            panel.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss"));
            panel.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity.dt.app-ui/PackageResources/Icons/Icons.uss"));
            panel.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UIPath + "UniPalette.uss"));
            state = new PaletteState();
            state.CommandResultsChanged += OnCommandResultsChanged;
            state.ExecuteCommand += ExecuteCommand;
            observedSelection = Selection.activeObject;
            observedSelectionCount = Selection.count;
            back = rootVisualElement.Q<ActionButton>("back");
            back.clickable.clicked += GoBack;
            modeLabel = rootVisualElement.Q<Text>("mode");
            contextLabel = rootVisualElement.Q<Text>("context");
            contextTitle = rootVisualElement.Q<Text>("context-title");
            contextIcon = rootVisualElement.Q<Image>("context-icon");
            resultsTitle = rootVisualElement.Q<Text>("results-title");
            resultsCount = rootVisualElement.Q<Text>("results-count");
            hints = rootVisualElement.Q<Text>("hints");
            messageLabel = rootVisualElement.Q<Text>("message");
            dataLabel = rootVisualElement.Q<Text>("data-label");
            searchBar = rootVisualElement.Q<SearchBar>("search");
            results = rootVisualElement.Q<ListView>("results");
            results.makeItem = MakeRow;
            results.bindItem = BindRow;
            results.itemsSource = state.Results;
            results.selectionChanged += _ => state.Select(results.selectedIndex);
            searchBar.RegisterCallback<ChangingEvent<string>>(evt => UpdateQuery(evt.newValue));
            searchBar.RegisterValueChangedCallback(evt => UpdateQuery(evt.newValue));
            // Capture before the native text input and ListView consume navigation keys.
            panel.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            panel.RegisterCallback<NavigationMoveEvent>(evt =>
            {
                if (evt.direction == NavigationMoveEvent.Direction.Up || evt.direction == NavigationMoveEvent.Direction.Down)
                    evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            Render();
        }

        private void OnCommandResultsChanged() => Render(false);

        private void UpdateQuery(string query)
        {
            if (query == state.Query) return;
            state.SetQuery(query);
            RefreshResults();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            var enter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
            if (evt.keyCode == KeyCode.Escape)
            {
                Consume(evt);
                if (state.CanGoBack) GoBack(); else Close();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.LeftArrow)
            {
                Consume(evt);
                GoBack();
            }
            else if (evt.altKey && evt.keyCode == KeyCode.RightArrow)
            {
                Consume(evt);
                if (!evt.ctrlKey && !evt.commandKey && !evt.shiftKey) ActivateResult(false, browseOnly: true);
            }
            else if (!evt.ctrlKey && !evt.commandKey && !evt.shiftKey &&
                     (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow))
            {
                Consume(evt);
                state.MoveSelection(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                results.SetSelectionWithoutNotify(state.SelectedIndex < 0 ? System.Array.Empty<int>() : new[] { state.SelectedIndex });
                if (state.SelectedIndex >= 0) results.ScrollToItem(state.SelectedIndex);
                searchBar.Focus();
            }
            else if (enter)
            {
                Consume(evt);
                if (evt.altKey || evt.shiftKey || evt.commandKey) return;
                ActivateResult(evt.ctrlKey);
            }
            // Unmodified Left/Right are intentionally left to the text input.
        }

        private static void Consume(KeyDownEvent evt)
        {
            evt.StopImmediatePropagation();
        }

        private void ActivateResult(bool control, bool browseOnly = false)
        {
            if (executing || !this) return;
            if (state.Enter(control, browseOnly)) Render();
            else
            {
                if (executing || !this) return;
                UpdateMessage();
                ScheduleSearchFocus();
            }
            searchBar.Focus();
        }

        private void ExecuteCommand(PaletteItem command, PaletteItem context)
        {
            if (executing) return;
            if (!NativeExecution.TryPrepare(command, context, out var execution, out var error))
            {
                state.ShowExecutionError(error);
                return;
            }
            executing = true;
            Close();
            execution.ExecuteAfterClose();
        }

        private void ActivateRow(ListViewItem row, EventBase evt)
        {
            var pointer = evt as IPointerEvent;
            var mouse = evt as IMouseEvent;
            // A second click must not activate the row rebound beneath the first click.
            if ((pointer?.clickCount ?? mouse?.clickCount ?? 1) > 1) return;
            var index = state.Results.IndexOf(row.userData as PaletteItem);
            if (index < 0) return;
            state.Select(index);
            results.SetSelectionWithoutNotify(new[] { index });
            ActivateResult(pointer?.ctrlKey ?? mouse?.ctrlKey ?? false);
        }

        private void GoBack()
        {
            if (state.Back()) Render();
            else ScheduleSearchFocus();
        }

        private void Render(bool focusSearch = true)
        {
            // Also acknowledge API changes when Unity does not deliver selectionChanged.
            observedSelection = Selection.activeObject;
            observedSelectionCount = Selection.count;
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
            RefreshResults();
            if (focusSearch) ScheduleSearchFocus();
        }

        private void RefreshResults()
        {
            var selected = state.SelectedIndex;
            results.SetSelectionWithoutNotify(System.Array.Empty<int>());
            results.RefreshItems();
            state.Select(selected);
            results.SetSelectionWithoutNotify(state.SelectedIndex < 0 ? System.Array.Empty<int>() : new[] { state.SelectedIndex });
            if (state.SelectedIndex >= 0) results.ScrollToItem(state.SelectedIndex);
            resultsCount.text = state.Results.Count.ToString();
            UpdateMessage();
        }

        private void UpdateMessage()
        {
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
            var indicator = new Text { name = "indicator", pickingMode = PickingMode.Ignore };
            indicator.AddToClassList("unipalette__row-indicator");
            row.trailingContainer.Add(indicator);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
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

        private void ScheduleSearchFocus()
        {
            // Resolve the current field after attachment/layout, including GUI recreation.
            rootVisualElement.schedule.Execute(() => searchBar?.Focus());
        }
    }
}
