using System;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("CommandDiscovery")]
    [Category("Fast")]
    public class CommandDiscoveryTests : CommandTestFixture
    {
        [Test]
        public void NormalizationRemovesDuplicatesMalformedPathsAndSeparators()
        {
            var paths = new[]
            {
                "Assets/Open",
                "Assets/Open",
                null,
                "",
                " ",
                "Assets",
                "/Assets/Open",
                "Assets/Open/",
                "Assets//Open",
                "Assets/---",
                "Assets/./Open",
                "Assets/../Open",
                "Assets\\Open",
                "Assets/Op\nen",
                " Assets/Open",
            };

            Assert.That(MenuCommands.Normalize(paths), Is.EqualTo(new[] { "Assets/Open", }));
        }

        [Test]
        public void ContextFamiliesDoNotClaimUniversalApplicability()
        {
            Assert.That(
                Build(context: SceneRoot())
                    .Children
                    .Select(item => item.Path),
                Is.EqualTo(new[] { "GameObject", })
            );

            Assert.That(
                Build(context: Go())
                    .Children
                    .Select(item => item.Path),
                Is.EqualTo(new[] { "Component", "GameObject", })
            );

            Assert.That(
                Build()
                    .Children
                    .Select(item => item.Path),
                Is.EqualTo(new[] { "Assets", })
            );

            Assert.That(
                Build().Children[0].Children.Single(item => item.Path == "Assets/Open").AvailabilityNote,
                Does.Contain("unverified")
            );
        }

        [Test]
        public void EditCommandsFollowLiveAvailabilityInRealContexts()
        {
            var paths = new[] { "Edit/Rename", "Edit/Copy", };

            var contexts = new[] { Go(), new AssetContext("missing-test-guid"), };

            foreach (var context in contexts)
            {
                var tree = MenuCommands.Build(paths, context, true, path => path == "Edit/Rename");
                var group = tree.Children.Single();
                Assert.That(group.Path, Is.EqualTo("Edit"));
                Assert.That(group.Children.Select(item => item.Path), Is.EqualTo(new[] { "Edit/Rename", }));
            }
        }

        [Test]
        public void DisabledMenuFlagsAreHiddenOnlyForMatchingGlobalSelection()
        {
            var paths = new[] { "Assets/Open", "Assets/Create/Folder", };

            var matching = MenuCommands.Build(
                paths,
                Assets(),
                true,
                path => path != "Assets/Open"
            );

            Assert.That(matching.Children[0].Children.Any(item => item.Path == "Assets/Open"), Is.False);
            var mismatch = MenuCommands.Build(paths, Assets(), false, _ => false);

            Assert.That(
                mismatch.Children[0].Children.Single(item => item.Path == "Assets/Open").AvailabilityNote,
                Does.Contain("differs from Unity Selection")
            );
        }

        [Test]
        public void UnknownAvailabilityIsRetainedAndEmptyCategoriesArePruned()
        {
            var unknown = MenuCommands.Build(
                new[] { "Assets/Open", },
                Assets(),
                true,
                _ => null
            );

            Assert.That(unknown.Children[0].Children[0].AvailabilityNote, Does.Contain("unverified"));

            var failed = MenuCommands.Build(
                new[] { "Assets/Open", },
                Assets(),
                true,
                _ => throw new InvalidOperationException()
            );

            Assert.That(failed.Children.Length, Is.EqualTo(1));

            var empty = MenuCommands.Build(
                new[] { "Assets/Create/Folder", },
                Assets(),
                true,
                _ => false
            );

            using var session = PaletteTestData.CreateSession(
                new[] { Assets(), },
                empty
            );

            session.Select(0);
            session.OpenContextCommands();
            Assert.That(session.Results, Is.Empty);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
        }
    }
}