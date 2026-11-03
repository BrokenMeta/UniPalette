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
    [Category("View")]
    [Category("UI")]
    public class PaletteViewTests : PaletteWindowTestFixture
    {

        [UnityTest]
        public IEnumerator DisposedViewAndRecreatedGuiDoNotRetainInputCallbacks()
        {
            yield return SetUpWindow();
            yield return null;
            Key(KeyCode.DownArrow);
            Key(KeyCode.DownArrow);
            Key(KeyCode.Return);
            Assert.That(state.Context.Path, Is.EqualTo("Assets"));
            var oldSearch = window.rootVisualElement.Q<VisualElement>("search");
            var oldInput = oldSearch.Q<TextField>();
            oldInput.value = "Folder";
            yield return null;
            var retiredState = state;
            var oldResults = results;
            var oldBack = window.rootVisualElement.Q<Unity.AppUI.UI.ActionButton>("back");
            var retiredView = (PaletteView)typeof(UniPaletteWindow).GetField("view", PrivateInstance).GetValue(window);
            retiredView.ScheduleSearchFocus();
            retiredView.Dispose();
            retiredView.Dispose(); // Disposal is safe when the window subsequently releases it again.
            oldInput.value = "stale query";
            oldResults.itemsSource = retiredState.Results;
            oldResults.SetSelection(0);
            ClickElement(oldBack);
            using (var staleKey = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            {
                staleKey.target = oldSearch;
                oldSearch.SendEvent(staleKey);
            }
            Assert.That(retiredState.Query, Is.EqualTo("Folder"));
            Assert.That(retiredState.SelectedIndex, Is.EqualTo(-1));
            Assert.That(retiredState.Context.Path, Is.EqualTo("Assets"));

            typeof(UniPaletteWindow).GetMethod("CreateGUI", PrivateInstance).Invoke(window, null);
            state = new PaletteState(new[] {
                new FixtureItem("Project", "Assets", false,
                    new FixtureItem("Folder", "Assets/Folder", false,
                        new FixtureItem("Leaf", "Assets/Folder/Leaf")))
            }, PaletteTestData.Commands());
            PaletteTestData.Install(window, state);
            results = window.rootVisualElement.Q<ListView>("results");
            var input = window.rootVisualElement.Q<VisualElement>("search").Q<TextField>();
            input.value = "Project";
            oldInput.value = "late retired input";
            oldResults.SetSelection(0);
            Assert.That(state.Query, Is.EqualTo("Project"));
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            Key(KeyCode.DownArrow);
            Key(KeyCode.Return);
            Assert.That(state.Context.Path, Is.EqualTo("Assets"), "One key press must cause exactly one transition");
            Key(KeyCode.Escape);
            Assert.That(state.Context, Is.Null);
            Assert.That(state.CanGoBack, Is.False);
            yield return WaitForSearchFocus();
        }

        [UnityTest]
        public IEnumerator RebindingViewUpdatesRowsAndUsesCurrentRowData()
        {
            yield return SetUpWindow();
            yield return null;
            var category = new MenuCategoryItem("Create", "Assets/Create", new PaletteItem[] {
                new MenuCommandItem("Folder", "Assets/Create/Folder")
            });
            var command = new MenuCommandItem("Copy Path", "Assets/Copy Path", "Current binding note");
            state = new PaletteState(new[] { new FixtureItem("Project", "Assets") },
                new MenuCategoryItem("Commands", "", new PaletteItem[] { category, command }));
            state.Select(0);
            state.OpenContextCommands();
            PaletteTestData.Install(window, state);
            Assert.That(state.SelectedIndex, Is.Zero, "Rendering a new model must preserve its selection");
            Assert.That(results.selectedIndex, Is.Zero);
            yield return null;
            var row = (Unity.AppUI.UI.ListViewItem)results.GetRootElementForIndex(0);
            Assert.That(row.title, Is.EqualTo("Create"));
            Assert.That(row.Q<Unity.AppUI.UI.Text>("indicator").text, Is.EqualTo("›"));
            var input = window.rootVisualElement.Q<VisualElement>("search").Q<TextField>();
            input.value = "Copy Path";
            yield return null;
            row = (Unity.AppUI.UI.ListViewItem)results.GetRootElementForIndex(0);
            Assert.That(row.userData, Is.SameAs(command));
            Assert.That(row.title, Is.EqualTo("Copy Path"));
            Assert.That(row.subtitle, Does.Contain("Assets/Copy Path").And.Contain("Execution unavailable"));
            Assert.That(row.tooltip, Does.Contain("Current binding note"));
            Assert.That(row.Q<Unity.AppUI.UI.Text>("indicator").text, Is.EqualTo("—"));
            var location = state.Location;
            Click(0);
            Assert.That(state.Location, Is.EqualTo(location), "Rebound leaf must not activate the former category");
            Assert.That(state.Message, Does.Contain("Assets/Copy Path").And.Contain("nothing executed"));
            yield return WaitForSearchFocus();
        }
    }
}
