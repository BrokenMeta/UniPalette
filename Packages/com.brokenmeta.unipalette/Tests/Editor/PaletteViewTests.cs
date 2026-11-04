using BrokenMeta.UniPalette.Editor.Services;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Presentation;
using BrokenMeta.UniPalette.Editor;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("View")]
    [Category("UI")]
    public class PaletteViewTests : PaletteWindowTestFixture
    {

        [UnityTest]
        public IEnumerator RecreatingAndClosingWindowReleaseOwnedCommandSources()
        {
            yield return SetUpWindow();
            var first = new TestCommandSource(PaletteTestData.Commands());
            var firstSession = new PaletteSession(first, new UnityContextSelection(), new UnityContextSource());
            PaletteTestData.Install(window, firstSession, first);
            typeof(UniPaletteWindow).GetMethod("CreateGUI", PrivateInstance).Invoke(window, null);
            Assert.That(first.Subscribers, Is.Zero);
            Assert.That(first.DisposeCalls, Is.EqualTo(1));

            var second = new TestCommandSource(PaletteTestData.Commands());
            var secondSession = new PaletteSession(second, new UnityContextSelection(), new UnityContextSource());
            PaletteTestData.Install(window, secondSession, second);
            window.Close();
            yield return null;
            Assert.That(second.Subscribers, Is.Zero);
            Assert.That(second.DisposeCalls, Is.EqualTo(1));
            Assert.That(first.DisposeCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisposedViewAndRecreatedGuiDoNotRetainInputCallbacks()
        {
            yield return SetUpWindow();
            yield return null;
            Key(KeyCode.DownArrow);
            Key(KeyCode.DownArrow);
            Key(KeyCode.Return);
            Assert.That(session.Context.Path, Is.EqualTo("Assets"));
            var oldSearch = window.rootVisualElement.Q<VisualElement>("search");
            var oldInput = oldSearch.Q<TextField>();
            oldInput.value = "Folder";
            yield return null;
            var retiredSession = session;
            var retiredPresenter = PaletteTestData.GetPresenter(window);
            var oldResults = results;
            var oldBack = window.rootVisualElement.Q<Unity.AppUI.UI.ActionButton>("back");
            var retiredView = (PaletteView)typeof(UniPaletteWindow).GetField("view", PrivateInstance).GetValue(window);
            retiredView.ScheduleSearchFocus();
            retiredView.Dispose();
            retiredView.Dispose(); // Disposal is safe when the window subsequently releases it again.
            oldInput.value = "stale query";
            oldResults.itemsSource = retiredSession.Results;
            oldResults.SetSelection(0);
            ClickElement(oldBack);
            using (var staleKey = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            {
                staleKey.target = oldSearch;
                oldSearch.SendEvent(staleKey);
            }
            Assert.That(retiredSession.Query, Is.EqualTo("Folder"));
            Assert.That(retiredSession.SelectedIndex, Is.EqualTo(-1));
            Assert.That(retiredSession.Context.Path, Is.EqualTo("Assets"));

            typeof(UniPaletteWindow).GetMethod("CreateGUI", PrivateInstance).Invoke(window, null);
            var retiredBrowser = retiredSession.Browser;
            retiredSession.Select(0);
            retiredPresenter.ActivateSelected();
            Assert.That(retiredSession.Browser, Is.SameAs(retiredBrowser), "Disposed presenter must ignore activation");
            retiredPresenter.GoBack();
            Assert.That(retiredSession.Browser, Is.SameAs(retiredBrowser), "Disposed presenter must not navigate its retired session");
            session = PaletteTestData.CreateSession(new[] {
                new FixtureItem("Project", "Assets", false,
                    new FixtureItem("Folder", "Assets/Folder", false,
                        new FixtureItem("Leaf", "Assets/Folder/Leaf")))
            }, PaletteTestData.Commands());
            PaletteTestData.Install(window, session);
            results = window.rootVisualElement.Q<ListView>("results");
            var input = window.rootVisualElement.Q<VisualElement>("search").Q<TextField>();
            input.value = "Project";
            oldInput.value = "late retired input";
            oldResults.SetSelection(0);
            Assert.That(session.Query, Is.EqualTo("Project"));
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
            Key(KeyCode.DownArrow);
            Key(KeyCode.Return);
            Assert.That(session.Context.Path, Is.EqualTo("Assets"), "One key press must cause exactly one transition");
            Key(KeyCode.Escape);
            Assert.That(session.Context, Is.Null);
            Assert.That(session.CanGoBack, Is.False);
            yield return WaitForSearchFocus();
        }

        [UnityTest]
        public IEnumerator RebindingViewUpdatesRowsAndUsesCurrentRowData()
        {
            yield return SetUpWindow();
            yield return null;
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.Text>("mode").text, Is.EqualTo("Context"));
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.SearchBar>("search").placeholder, Is.EqualTo("Search contexts..."));
            var category = new MenuCategoryItem("Create", "Assets/Create", new PaletteItem[] {
                new MenuCommandItem("Folder", "Assets/Create/Folder")
            });
            var command = new MenuCommandItem("Copy Path", "Assets/Copy Path", "Current binding note");
            session = PaletteTestData.CreateSession(new[] { new FixtureItem("Project", "Assets") },
                new MenuCategoryItem("Commands", "", new PaletteItem[] { category, command }));
            session.Select(0);
            session.OpenContextCommands();
            PaletteTestData.Install(window, session);
            Assert.That(session.SelectedIndex, Is.Zero, "Rendering a new model must preserve its selection");
            Assert.That(results.selectedIndex, Is.Zero);
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.Text>("mode").text, Is.EqualTo("Commands"));
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.Text>("data-label").text, Is.EqualTo("Unity menu"));
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.Text>("context").text, Is.EqualTo("Assets / Commands"));
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.SearchBar>("search").placeholder, Is.EqualTo("Search commands..."));
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
            var location = session.Location;
            Click(0);
            Assert.That(session.Location, Is.EqualTo(location), "Rebound leaf must not activate the former category");
            Assert.That(session.Message, Does.Contain("Assets/Copy Path").And.Contain("nothing executed"));
            yield return WaitForSearchFocus();
            PaletteTestData.GetPresenter(window).GoBack();
            yield return null;
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.Text>("mode").text, Is.EqualTo("Context"));
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.Text>("data-label").text, Is.EqualTo("Unity Editor"));
            Assert.That(window.rootVisualElement.Q<Unity.AppUI.UI.SearchBar>("search").placeholder, Is.EqualTo("Search contexts..."));
            row = (Unity.AppUI.UI.ListViewItem)results.GetRootElementForIndex(0);
            Assert.That(row.title, Is.EqualTo("Project"));
            Assert.That(row.subtitle, Is.EqualTo("Assets"));
            Assert.That(row.Q<Unity.AppUI.UI.Text>("indicator").text, Is.EqualTo("↗"));
            yield return WaitForSearchFocus();
        }
    }
}
