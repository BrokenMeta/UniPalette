using System.Collections;
using BrokenMeta.UniPalette.Editor;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Presentation;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using TextField = UnityEngine.UIElements.TextField;

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
            PaletteTestData.Install(_window, firstSession, first);

            typeof(UniPaletteWindow)
                .GetMethod("CreateGUI", PrivateInstance)
                .Invoke(_window, null);

            Assert.That(first.Subscribers, Is.Zero);
            Assert.That(first.DisposeCalls, Is.EqualTo(1));
            var second = new TestCommandSource(PaletteTestData.Commands());
            var secondSession = new PaletteSession(second, new UnityContextSelection(), new UnityContextSource());
            PaletteTestData.Install(_window, secondSession, second);
            _window.Close();

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
            Assert.That(_session.Context.Path, Is.EqualTo("Assets"));
            var oldSearch = _window.rootVisualElement.Q<VisualElement>("search");
            var oldInput = oldSearch.Q<TextField>();
            oldInput.value = "Folder";

            yield return null;
            var retiredSession = _session;
            var retiredPresenter = PaletteTestData.GetPresenter(_window);
            var oldResults = _results;
            var oldBack = _window.rootVisualElement.Q<ActionButton>("back");

            var retiredView = (PaletteView)typeof(UniPaletteWindow)
                .GetField("_view", PrivateInstance)
                .GetValue(_window);

            retiredView.ScheduleSearchFocus();
            retiredView.Dispose();
            retiredView.Dispose(); // Disposal is safe when the window subsequently releases it again.
            oldInput.value = "stale query";
            oldResults.itemsSource = retiredSession._results;
            oldResults.SetSelection(0);
            ClickElement(oldBack);

            using (var staleKey = KeyDownEvent.GetPooled(
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape, }
                ))
            {
                staleKey.target = oldSearch;
                oldSearch.SendEvent(staleKey);
            }

            Assert.That(retiredSession.Query.Text, Is.EqualTo("Folder"));
            Assert.That(retiredSession.SelectedIndex, Is.EqualTo(-1));
            Assert.That(retiredSession.Context.Path, Is.EqualTo("Assets"));

            typeof(UniPaletteWindow)
                .GetMethod("CreateGUI", PrivateInstance)
                .Invoke(_window, null);

            var retiredBrowser = retiredSession.Browser;
            retiredSession.Select(0);
            retiredPresenter.ActivateSelected();

            Assert.That(
                retiredSession.Browser,
                Is.SameAs(retiredBrowser),
                "Disposed presenter must ignore activation"
            );

            retiredPresenter.GoBack();

            Assert.That(
                retiredSession.Browser,
                Is.SameAs(retiredBrowser),
                "Disposed presenter must not navigate its retired session"
            );

            _session = PaletteTestData.CreateSession(
                new[]
                {
                    new FixtureItem(
                        "Project",
                        "Assets",
                        false,
                        new FixtureItem(
                            "Folder",
                            "Assets/Folder",
                            false,
                            new FixtureItem(
                                "Leaf",
                                "Assets/Folder/Leaf"
                            )
                        )
                    ),
                },
                PaletteTestData.Commands()
            );

            PaletteTestData.Install(_window, _session);
            _results = _window.rootVisualElement.Q<ListView>("results");

            var input = _window
                .rootVisualElement
                .Q<VisualElement>("search")
                .Q<TextField>();

            input.value = "Project";
            oldInput.value = "late retired input";
            oldResults.SetSelection(0);
            Assert.That(_session.Query.Text, Is.EqualTo("Project"));
            Assert.That(_session.SelectedIndex, Is.EqualTo(-1));
            Key(KeyCode.DownArrow);
            Key(KeyCode.Return);

            Assert.That(
                _session.Context.Path,
                Is.EqualTo("Assets"),
                "One key press must cause exactly one transition"
            );

            Key(KeyCode.Escape);
            Assert.That(_session.Context, Is.Null);
            Assert.That(_session.CanGoBack, Is.False);

            yield return WaitForSearchFocus();
        }

        [UnityTest]
        public IEnumerator RebindingViewUpdatesRowsAndUsesCurrentRowData()
        {
            yield return SetUpWindow();
            yield return null;
            Assert.That(_window.rootVisualElement.Q<Text>("mode").text, Is.EqualTo("Context"));

            Assert.That(
                _window.rootVisualElement.Q<SearchBar>("search").placeholder,
                Is.EqualTo("Search contexts...")
            );

            var category = new MenuCategoryItem(
                "Create",
                "Assets/Create",
                new PaletteItem[]
                {
                    new MenuCommandItem(
                        "Folder",
                        "Assets/Create/Folder"
                    ),
                }
            );

            var command = new MenuCommandItem("Copy Path", "Assets/Copy Path", "Current binding note");

            _session = PaletteTestData.CreateSession(
                new[] { new FixtureItem("Project", "Assets"), },
                new MenuCategoryItem(
                    "Commands",
                    "",
                    new PaletteItem[] { category, command, }
                )
            );

            _session.Select(0);
            _session.OpenContextCommands();
            PaletteTestData.Install(_window, _session);
            Assert.That(_session.SelectedIndex, Is.Zero, "Rendering a new model must preserve its selection");
            Assert.That(_results.selectedIndex, Is.Zero);
            Assert.That(_window.rootVisualElement.Q<Text>("mode").text, Is.EqualTo("Commands"));

            Assert.That(
                _window.rootVisualElement.Q<Text>("data-label").text,
                Is.EqualTo("Unity commands")
            );

            Assert.That(
                _window.rootVisualElement.Q<Text>("context").text,
                Is.EqualTo("Assets / Commands")
            );

            Assert.That(
                _window.rootVisualElement.Q<SearchBar>("search").placeholder,
                Is.EqualTo("Search commands...")
            );

            yield return null;
            var row = (ListViewItem)_results.GetRootElementForIndex(0);
            Assert.That(row.title, Is.EqualTo("Create"));
            Assert.That(row.Q<Text>("indicator").text, Is.EqualTo("\u203A"));

            var input = _window
                .rootVisualElement
                .Q<VisualElement>("search")
                .Q<TextField>();

            input.value = "Copy Path";

            yield return null;
            row = (ListViewItem)_results.GetRootElementForIndex(0);
            Assert.That(row.userData, Is.SameAs(command));
            Assert.That(row.title, Is.EqualTo("Copy Path"));

            Assert.That(
                row.subtitle,
                Does
                    .Contain("Assets/Copy Path")
                    .And
                    .Contain("Execution unavailable")
            );

            Assert.That(row.tooltip, Does.Contain("Current binding note"));
            Assert.That(row.Q<Text>("indicator").text, Is.EqualTo("\u2014"));
            var location = _session.Location;
            Click(0);

            Assert.That(
                _session.Location,
                Is.EqualTo(location),
                "Rebound leaf must not activate the former category"
            );

            Assert.That(
                _session.Message,
                Does
                    .Contain("Assets/Copy Path")
                    .And
                    .Contain("nothing executed")
            );

            yield return WaitForSearchFocus();

            PaletteTestData
                .GetPresenter(_window)
                .GoBack();

            yield return null;
            Assert.That(_window.rootVisualElement.Q<Text>("mode").text, Is.EqualTo("Context"));

            Assert.That(
                _window.rootVisualElement.Q<Text>("data-label").text,
                Is.EqualTo("Unity Editor")
            );

            Assert.That(
                _window.rootVisualElement.Q<SearchBar>("search").placeholder,
                Is.EqualTo("Search contexts...")
            );

            row = (ListViewItem)_results.GetRootElementForIndex(0);
            Assert.That(row.title, Is.EqualTo("Project"));
            Assert.That(row.subtitle, Is.EqualTo("Assets"));
            Assert.That(row.Q<Text>("indicator").text, Is.EqualTo("\u2197"));

            yield return WaitForSearchFocus();
        }
    }
}