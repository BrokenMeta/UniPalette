using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("CommandDiscovery")]
    [Category("UI")]
    [Category("EditorIntegration")]
    public class WindowDiscoveryTests : CommandTestFixture
    {

        [UnityTest]
        public IEnumerator LiveWindowRetainsKeyboardFlowAndFocusWithDiscoveredCommands()
        {
            var before = Selection.objects;
            var window = EditorWindow.GetWindow<UniPaletteWindow>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(UniPaletteWindow).GetMethod("CreateGUI", flags).Invoke(window, null);
            window.Show(); window.Focus();
            try
            {
                var state = (PaletteState)typeof(UniPaletteWindow).GetField("state", flags).GetValue(window);
                var results = window.rootVisualElement.Q<ListView>("results");
                var search = window.rootVisualElement.Q<VisualElement>("search");
                var input = search.Q<TextField>();
                void Key(KeyCode key, EventModifiers modifiers = EventModifiers.None)
                {
                    var target = input.Q<TextElement>() as VisualElement ?? input;
                    using var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key, modifiers = modifiers });
                    evt.target = target; target.SendEvent(evt);
                }
                var hierarchy = state.Results.FindIndex(item => item is SceneRootContext);
                state.Select(hierarchy); results.SetSelectionWithoutNotify(new[] { hierarchy });
                Key(KeyCode.Return, EventModifiers.Control);
                var deadline = EditorApplication.timeSinceStartup + 5;
                while (state.CommandsLoading && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(state.Results.Any(item => item.Path == "GameObject"), Is.True);
                Assert.That(state.SelectedIndex, Is.Zero);
                Assert.That(results.selectedIndex, Is.EqualTo(state.SelectedIndex));
                Key(KeyCode.RightArrow, EventModifiers.Alt);
                Assert.That(state.Location, Is.EqualTo("GameObject"));
                input.value = "Cube";
                Assert.That(state.SelectedIndex, Is.EqualTo(-1));
                Assert.That(state.Results.Any(item => item.Path == "GameObject/3D Object/Cube"), Is.True);
                var cube = state.Results.FindIndex(item => item.Path == "GameObject/3D Object/Cube");
                state.Select(cube); results.SetSelectionWithoutNotify(new[] { cube });
                Key(KeyCode.RightArrow, EventModifiers.Alt); // A root leaf is now executable; navigation must not run it.
                Assert.That(state.Message, Is.Empty);
                Assert.That(Selection.count, Is.Zero);
                Key(KeyCode.LeftArrow, EventModifiers.Alt);
                Assert.That(state.Query, Is.Empty);
                Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo("GameObject"));
                yield return null;
                var focused = window.rootVisualElement.panel.focusController.focusedElement;
                Assert.That(focused == search || search.Contains(focused as VisualElement), Is.True);
            }
            finally { window.Close(); }
        }
    }
}
