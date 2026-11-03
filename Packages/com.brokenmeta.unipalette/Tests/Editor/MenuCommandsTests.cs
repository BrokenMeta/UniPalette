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
    public class MenuCommandsTests
    {
        private UnityEngine.Object[] originalSelection;
        [SetUp] public void SetUp() => originalSelection = Selection.objects;
        [TearDown] public void TearDown() => Selection.objects = originalSelection;

        private static readonly string[] Paths = {
            "Assets/Create/Material", "Assets/Create/Folder", "Assets/CreateMore/Other", "Assets/Open",
            "GameObject/Create Empty", "GameObject/3D Object/Cube", "Component/Physics/Box Collider",
            "Window/General/Inspector"
        };
        private static PaletteItem Assets() => new AssetsRootContext();
        private static PaletteItem SceneRoot() => new SceneRootContext();
        private static PaletteItem Go() => new FixtureItem("Player", "Scene Root/Player");
        private static PaletteItem Build(string[] paths = null, PaletteItem context = null) =>
            MenuCommands.Build(paths ?? Paths, context ?? Assets(), false, _ => true);
        private static void Choose(PaletteState state, string path)
        {
            var index = state.Results.FindIndex(item => item.Path == path);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), path);
            state.Select(index);
        }

        [Test]
        public void NormalizationRemovesDuplicatesMalformedPathsAndSeparators()
        {
            var paths = new[] { "Assets/Open", "Assets/Open", null, "", " ", "Assets", "/Assets/Open", "Assets/Open/",
                "Assets//Open", "Assets/---", "Assets/./Open", "Assets/../Open", "Assets\\Open", "Assets/Op\nen", " Assets/Open" };
            Assert.That(MenuCommands.Normalize(paths), Is.EqualTo(new[] { "Assets/Open" }));
        }

        [Test]
        public void ContextFamiliesDoNotClaimUniversalApplicability()
        {
            Assert.That(Build(context: SceneRoot()).Children.Select(item => item.Path), Is.EqualTo(new[] { "GameObject" }));
            Assert.That(Build(context: Go()).Children.Select(item => item.Path), Is.EqualTo(new[] { "Component", "GameObject" }));
            Assert.That(Build().Children.Select(item => item.Path), Is.EqualTo(new[] { "Assets" }));
            Assert.That(Build().Children[0].Children.Single(item => item.Path == "Assets/Open").AvailabilityNote,
                Does.Contain("unverified"));
        }

        [Test]
        public void EmptyQueryShowsChildrenAndRecursiveQueryStaysInsideCategory()
        {
            using var state = new PaletteState(new[] { Assets() }, Build());
            state.Select(0); state.Enter(true);
            Assert.That(state.Results.Select(item => item.Path), Is.EqualTo(new[] { "Assets" }));
            Choose(state, "Assets"); state.Enter(false);
            Choose(state, "Assets/Create"); state.Enter(false, browseOnly: true);
            Assert.That(state.Results.Select(item => item.Path), Is.EqualTo(new[] { "Assets/Create/Folder", "Assets/Create/Material" }));
            state.SetQuery("Other");
            Assert.That(state.Results, Is.Empty, "CreateMore is not a descendant of Create");
            state.SetQuery("CREATE/material");
            Assert.That(state.Results.Single().Path, Is.EqualTo("Assets/Create/Material"));
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.Back();
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo("Assets/Create"));
        }

        [Test]
        public void RepeatedQueryAndDeepCategoryReturnHaveNoDuplicates()
        {
            var root = Build(new[] { "Assets/Create/Shapes/Cube", "Assets/Create/Shapes/Cube", "Assets/Open" });
            using var state = new PaletteState(new[] { Assets() }, root);
            state.Select(0); state.Enter(true);
            state.SetQuery("Shapes");
            Choose(state, "Assets/Create/Shapes"); state.Enter(false);
            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Results.Single().Path, Is.EqualTo("Assets/Create/Shapes/Cube"));
            state.Back();
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo("Assets"), "Restore visible ancestor at menu root");
            for (var i = 0; i < 3; ++i)
            {
                state.SetQuery("Cube");
                Assert.That(state.Results.Single().Path, Is.EqualTo("Assets/Create/Shapes/Cube"));
                state.SetQuery("");
                Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            }
        }

        [Test]
        public void DisabledMenuFlagsAreHiddenOnlyForMatchingGlobalSelection()
        {
            var paths = new[] { "Assets/Open", "Assets/Create/Folder" };
            var matching = MenuCommands.Build(paths, Assets(), true, path => path != "Assets/Open");
            Assert.That(matching.Children[0].Children.Any(item => item.Path == "Assets/Open"), Is.False);
            var mismatch = MenuCommands.Build(paths, Assets(), false, _ => false);
            Assert.That(mismatch.Children[0].Children.Single(item => item.Path == "Assets/Open").AvailabilityNote,
                Does.Contain("differs from Unity Selection"));
        }

        [Test]
        public void UnknownAvailabilityIsRetainedAndEmptyCategoriesArePruned()
        {
            var unknown = MenuCommands.Build(new[] { "Assets/Open" }, Assets(), true, _ => null);
            Assert.That(unknown.Children[0].Children[0].AvailabilityNote, Does.Contain("unverified"));
            var failed = MenuCommands.Build(new[] { "Assets/Open" }, Assets(), true, _ => throw new InvalidOperationException());
            Assert.That(failed.Children.Length, Is.EqualTo(1));
            var empty = MenuCommands.Build(new[] { "Assets/Create/Folder" }, Assets(), true, _ => false);
            using var state = new PaletteState(new[] { Assets() }, empty);
            state.Select(0); state.Enter(true);
            Assert.That(state.Results, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void LeafWithoutExecutorKeepsConfirmedSelectionAndDoesNotDispatch()
        {
            using var state = new PaletteState(new[] { Assets() }, Build());
            state.Select(0); state.Enter(true);
            var before = Selection.objects;
            state.SetQuery("Assets/Open"); state.Select(0);
            Assert.That(state.Enter(false, browseOnly: true), Is.False);
            Assert.That(state.Message, Is.Empty);
            Assert.That(state.Enter(true), Is.False);
            Assert.That(state.Enter(false), Is.False);
            Assert.That(state.Message, Does.Contain("Native execution is unavailable").And.Contain("nothing executed"));
            Assert.That(Selection.objects, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator LiveDiscoveryUsesMenuIdsAndKeepsSelectionUntouched()
        {
            var before = Selection.objects;
            using var commands = new MenuCommands();
            commands.Start();
            var end = EditorApplication.timeSinceStartup + 5;
            while (commands.IsLoading && EditorApplication.timeSinceStartup < end) yield return null;
            Assert.That(commands.IsLoading, Is.False);
            Assert.That(commands.Error, Is.Empty);
            Assert.That(commands.Paths, Does.Contain("Assets/Create/Folder"));
            Assert.That(commands.Paths.Count, Is.EqualTo(commands.Paths.Distinct(StringComparer.Ordinal).Count()));
            Assert.That(commands.Paths.All(path => path.Contains('/')), Is.True);
            var count = commands.Paths.Count;
            var root = commands.ForContext(Assets());
            Assert.That(root.Children.Select(item => item.Path), Is.EqualTo(new[] { "Assets" }));
            commands.ForContext(SceneRoot());
            Assert.That(commands.Paths.Count, Is.EqualTo(count), "Context/query changes reuse the discovery snapshot");
            Assert.That(Selection.objects, Is.EqualTo(before));
            commands.Dispose();
            yield return null;
            Assert.That(commands.IsLoading, Is.False);
        }

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
