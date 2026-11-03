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
    [Category("Fast")]
    public class CommandDiscoveryTests : CommandTestFixture
    {

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
            state.Select(0); state.OpenContextCommands();
            Assert.That(state.Results, Is.Empty);
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
        }
    }
}
