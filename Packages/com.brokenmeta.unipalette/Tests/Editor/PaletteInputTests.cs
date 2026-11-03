using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("Input")]
    [Category("UI")]
    public class PaletteInputTests : PaletteWindowTestFixture
    {

        [UnityTest]
        public IEnumerator AltArrowFlowSelectsScrollsAndKeepsTextInputFocus()
        {
            var children = Enumerable.Range(0, 60).Select(i => new FixtureItem("Group " + i, "Scene Root/Group " + i, false,
                new FixtureItem("Leaf " + i, "Scene Root/Group " + i + "/Leaf"))).ToArray();
            yield return SetUpWindow(new[] {
                new FixtureItem("Hierarchy", "Scene Root", false, children),
                new FixtureItem("Project", "Assets", false, children),
                new FixtureItem("Third", "Third", false, children),
                new FixtureItem("Fourth", "Fourth", false, children)
            });
            yield return null;
            Assert.That(results.selectedIndex, Is.EqualTo(-1), "Opening root never selects automatically");
            Key(KeyCode.DownArrow, EventModifiers.Alt);
            Assert.That(state.SelectedIndex, Is.Zero);
            Key(KeyCode.DownArrow, EventModifiers.Alt);
            Assert.That(state.SelectedIndex, Is.EqualTo(1), "Exactly one step through the inner text field");
            Key(KeyCode.UpArrow, EventModifiers.Alt);
            Assert.That(state.SelectedIndex, Is.Zero);
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root"));
            Assert.That(results.selectedIndex, Is.Zero, "Forward selection reaches ListView");
            yield return null;
            for (var i = 0; i < 45; ++i) Key(KeyCode.DownArrow, EventModifiers.Alt);
            Assert.That(state.SelectedIndex, Is.EqualTo(45));
            Assert.That(results.selectedIndex, Is.EqualTo(45));
            yield return null;
            var scroll = results.Q<ScrollView>();
            Assert.That(scroll.scrollOffset.y, Is.GreaterThan(0), "Selected row is scrolled into view");
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Group 45"));
            Assert.That(results.selectedIndex, Is.Zero);
            yield return null;
            yield return WaitForSearchFocus();
            Key(KeyCode.LeftArrow, EventModifiers.Alt);
            Assert.That(results.selectedIndex, Is.EqualTo(45), "Back restores the entry, not first row");
            yield return null;
            yield return WaitForSearchFocus();
            var input = window.rootVisualElement.Q<VisualElement>("search").Q<TextField>();
            input.value = "Group";
            Assert.That(results.selectedIndex, Is.EqualTo(-1));
            input.value = "";
            Assert.That(results.selectedIndex, Is.EqualTo(-1), "Clearing query is not a forward transition");
            Key(KeyCode.DownArrow);
            Assert.That(results.selectedIndex, Is.Zero);
            Key(KeyCode.DownArrow);
            Key(KeyCode.UpArrow);
            Assert.That(results.selectedIndex, Is.Zero);
            input.value = "Group";
            input.cursorIndex = input.selectIndex = 2;
            var caretEvents = 0;
            var search = window.rootVisualElement.Q<VisualElement>("search");
            EventCallback<KeyDownEvent> observe = evt =>
            {
                if (evt.keyCode == KeyCode.RightArrow || evt.keyCode == KeyCode.LeftArrow) ++caretEvents;
            };
            search.RegisterCallback(observe, TrickleDown.TrickleDown);
            try { Key(KeyCode.RightArrow); Key(KeyCode.LeftArrow); }
            finally { search.UnregisterCallback(observe, TrickleDown.TrickleDown); }
            Assert.That(caretEvents, Is.EqualTo(2), "Plain Left/Right reach the text input instead of being consumed as navigation");
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root"));
        }

        [UnityTest]
        public IEnumerator BackRendersRestoredSelectionWithoutMovingSearchFocus()
        {
            yield return SetUpWindow();
            yield return null;
            Click(1);
            yield return null;
            Click(0);
            Key(KeyCode.LeftArrow, EventModifiers.Alt);
            Assert.That(state.Context.Path, Is.EqualTo("Assets"));
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Folder"));
            Assert.That(results.selectedIndex, Is.EqualTo(state.SelectedIndex));
            yield return WaitForSearchFocus();
            Key(KeyCode.Escape);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Project"));
            Assert.That(results.selectedIndex, Is.EqualTo(1));
            var input = window.rootVisualElement.Q<VisualElement>("search").Q<TextField>();
            input.value = "Hierarchy";
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            Assert.That(results.selectedIndex, Is.EqualTo(-1));
            yield return WaitForSearchFocus();
        }

        [UnityTest]
        public IEnumerator AltRightUsesSmartEnterAndNeverActivatesCommandLeaves()
        {
            yield return SetUpWindow();
            yield return null;
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Context, Is.Null, "No implicit selection");
            Key(KeyCode.DownArrow);
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root"));
            Assert.That(state.SelectedIndex, Is.Zero);
            yield return WaitForSearchFocus();
            // Query changes flow through the native text field inside the App UI SearchBar.
            var input = window.rootVisualElement.Q<VisualElement>("search").Q<TextField>();
            input.value = "Parent";
            Key(KeyCode.DownArrow);
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Parent"));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.SelectedIndex, Is.Zero);
            Key(KeyCode.DownArrow);
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Parent/Leaf"));
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Key(KeyCode.LeftArrow, EventModifiers.Alt);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Leaf"));
            Key(KeyCode.LeftArrow, EventModifiers.Alt);
            Key(KeyCode.LeftArrow, EventModifiers.Alt);
            Assert.That(state.Context, Is.Null, "One back per transition");
            Key(KeyCode.DownArrow); Key(KeyCode.DownArrow);
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Context.Path, Is.EqualTo("Assets"));
            Key(KeyCode.DownArrow); Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Context.Path, Is.EqualTo("Assets/Folder"));
            var searchInput = window.rootVisualElement.Q<VisualElement>("search").Q<TextField>();
            searchInput.value = "not found"; // Clear the automatic selection to target the current folder.
            Key(KeyCode.Return, EventModifiers.Control);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.SelectedIndex, Is.Zero); // Inspect is selected on entry.
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Message, Is.Empty, "Alt Right must not preview or execute a command");
            Key(KeyCode.DownArrow); // Create category.
            Key(KeyCode.RightArrow, EventModifiers.Alt);
            Assert.That(state.Location, Is.EqualTo("Mock commands/Create"));
            Key(KeyCode.LeftArrow, EventModifiers.Alt);
            Assert.That(state.Location, Is.EqualTo("Mock commands"));
            Assert.That(state.Query, Is.Empty);
            Assert.That(state.Results[state.SelectedIndex].Title, Is.EqualTo("Create (experimental category)"));
            yield return WaitForSearchFocus();
        }

        [UnityTest]
        public IEnumerator LeftClickNavigatesAndLeafKeepsEnterSemantics()
        {
            yield return SetUpWindow();
            yield return null;
            Click(0);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root"));
            Assert.That(state.SelectedIndex, Is.Zero);
            yield return null;
            yield return WaitForSearchFocus();
            Click(0);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Parent"));
            yield return null;
            Click(0);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Parent/Leaf"));
            Assert.That(state.SelectedIndex, Is.Zero);
            yield return null;
            yield return WaitForSearchFocus();
            Key(KeyCode.Escape);
            Assert.That(results.selectedIndex, Is.Zero, "Return restores the leaf row");
            yield return null;
            Click(0, control: true);
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Parent/Leaf"));
            Key(KeyCode.Escape);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root/Parent"), "Exactly one command level was pushed");
        }

        [UnityTest]
        public IEnumerator ControlClickFromRootAndDoubleClickDoNotDuplicateNavigation()
        {
            yield return SetUpWindow();
            yield return null;
            Click(1, control: true);
            Assert.That(state.Context.Path, Is.EqualTo("Assets"));
            Assert.That(state.Mode, Is.EqualTo(PaletteState.Browser.Commands));
            Key(KeyCode.Escape);
            Assert.That(state.Context, Is.Null);
            yield return null;
            Click(1);
            Assert.That(state.Context.Path, Is.EqualTo("Assets"));
            yield return null;
            Click(0, count: 2);
            Assert.That(state.Context.Path, Is.EqualTo("Assets"), "Second click must not enter a rebound row");
            Click(0);
            Assert.That(state.Context.Path, Is.EqualTo("Assets/Folder"));
            yield return null;
            yield return WaitForSearchFocus();
        }

        [UnityTest]
        public IEnumerator KeyboardAndRepeatedShortcutUseOneWindowAndKeepSearchFocus()
        {
            yield return SetUpWindow();
            yield return null;
            Key(KeyCode.DownArrow);
            Assert.That(state.SelectedIndex, Is.Zero);
            Key(KeyCode.Return);
            Assert.That(state.Context.Path, Is.EqualTo("Scene Root"));
            var method = typeof(UniPaletteWindow).GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method.GetCustomAttributes(typeof(ShortcutAttribute), false).Length, Is.EqualTo(1));
            Assert.That(ShortcutManager.instance.GetAvailableShortcutIds(), Does.Contain("UniPalette/Open"));
            method.Invoke(null, null);
            method.Invoke(null, null);
            yield return null;
            Assert.That(Resources.FindObjectsOfTypeAll<UniPaletteWindow>().Length, Is.EqualTo(1));
            yield return WaitForSearchFocus();
            Key(KeyCode.DownArrow);
            Assert.That(state.SelectedIndex, Is.Zero, "One arrow step after repeated activation");
        }
    }
}
