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
    public abstract class PaletteWindowTestFixture
    {
        private protected const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private protected UniPaletteWindow window;
        private protected PaletteState state;
        private protected ListView results;


        private protected IEnumerator SetUpWindow(PaletteItem[] roots = null)
        {
            window = EditorWindow.GetWindow<UniPaletteWindow>();
            typeof(UniPaletteWindow).GetMethod("CreateGUI", PrivateInstance).Invoke(window, null);
            state = new PaletteState(roots ?? new[] {
                new FixtureItem("Hierarchy", "Scene Root", false,
                    new FixtureItem("Parent", "Scene Root/Parent", false,
                        new FixtureItem("Leaf", "Scene Root/Parent/Leaf"))),
                new FixtureItem("Project", "Assets", false,
                    new FixtureItem("Folder", "Assets/Folder", false,
                        new FixtureItem("Asset", "Assets/Folder/Asset")))
            }, PaletteTestData.Commands());
            PaletteTestData.Install(window, state);
            results = window.rootVisualElement.Q<ListView>("results");
            window.Show();
            window.Focus();
            // Editor notifications from fixture/window setup must settle before testing input.
            // A real hierarchy/project change intentionally refreshes and clears row selection.
            var quietUntil = EditorApplication.timeSinceStartup + 0.25;
            var deadline = EditorApplication.timeSinceStartup + 5;
            void PostponeQuietPeriod() => quietUntil = EditorApplication.timeSinceStartup + 0.25;
            EditorApplication.hierarchyChanged += PostponeQuietPeriod;
            EditorApplication.projectChanged += PostponeQuietPeriod;
            try
            {
                while ((EditorApplication.timeSinceStartup < quietUntil || EditorApplication.isUpdating)
                    && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(EditorApplication.timeSinceStartup, Is.GreaterThanOrEqualTo(quietUntil), "Fixture notifications did not settle");
                Assert.That(EditorApplication.isUpdating, Is.False);
            }
            finally
            {
                EditorApplication.hierarchyChanged -= PostponeQuietPeriod;
                EditorApplication.projectChanged -= PostponeQuietPeriod;
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Restore the open user's window to live data, without closing it.
            if (window) typeof(UniPaletteWindow).GetMethod("CreateGUI", PrivateInstance).Invoke(window, null);
        }

        private protected void Click(int index, bool control = false, int count = 1, int button = 0)
        {
            var row = results.GetRootElementForIndex(index);
            Assert.That(row, Is.Not.Null, "Row must be realized before sending pointer events");
            ClickElement(row, control, count, button);
        }

        private protected static void ClickElement(VisualElement row, bool control = false, int count = 1, int button = 0)
        {
            var systemEvent = new Event { type = EventType.MouseDown, button = button, clickCount = count,
                modifiers = control ? EventModifiers.Control : EventModifiers.None, mousePosition = row.worldBound.center };
            using (var down = PointerDownEvent.GetPooled(systemEvent))
            {
                down.target = row;
                row.SendEvent(down);
            }
            systemEvent.type = EventType.MouseUp;
            using var up = PointerUpEvent.GetPooled(systemEvent);
            up.target = row;
            row.SendEvent(up);
        }

        private protected void Key(KeyCode key, EventModifiers modifiers = EventModifiers.None)
        {
            var search = window.rootVisualElement.Q<VisualElement>("search");
            var input = search.Q<TextField>();
            var target = input?.Q<TextElement>() as VisualElement ?? input?.Q<VisualElement>("unity-text-input") ?? search;
            using var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key, modifiers = modifiers });
            evt.target = target;
            target.SendEvent(evt);
        }

        private protected bool SearchHasFocus()
        {
            var search = window.rootVisualElement.Q<VisualElement>("search");
            var focused = window.rootVisualElement.panel.focusController.focusedElement;
            return focused == search || search.Contains(focused as VisualElement);
        }

        private protected IEnumerator WaitForSearchFocus()
        {
            var deadline = EditorApplication.timeSinceStartup + 0.5;
            while (!SearchHasFocus() && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(SearchHasFocus(), Is.True, "SearchBar must regain focus after pointer dispatch/layout");
        }
    }
}
