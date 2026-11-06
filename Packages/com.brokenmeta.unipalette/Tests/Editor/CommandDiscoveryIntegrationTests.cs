using System;
using System.Collections;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("CommandDiscovery")]
    [Category("EditorIntegration")]
    public class CommandDiscoveryIntegrationTests : CommandTestFixture
    {
        [UnityTest]
        public IEnumerator LiveDiscoveryUsesMenuIdsAndKeepsSelectionUntouched()
        {
            var before = Selection.objects;
            using var commands = new MenuCommands(new UnityContextSelection());
            commands.Start();
            var end = EditorApplication.timeSinceStartup + 5;

            while (commands.IsLoading && EditorApplication.timeSinceStartup < end)
                yield return null;

            Assert.That(commands.IsLoading, Is.False);
            Assert.That(commands.Error, Is.Empty);
            Assert.That(commands.Paths, Does.Contain("Assets/Create/Folder"));
            Assert.That(commands.Paths, Does.Contain("Edit/Rename"));

            Assert.That(
                commands.Paths.Count,
                Is.EqualTo(
                    commands
                        .Paths
                        .Distinct(StringComparer.Ordinal)
                        .Count()
                )
            );

            Assert.That(commands.Paths.All(path => path.Contains('/')), Is.True);
            var count = commands.Paths.Count;
            var root = commands.ForContext(Assets());
            Assert.That(root.Children.Select(item => item.Path), Is.EqualTo(new[] { "Assets", }));

            var realContextKinds = new[] { Go(), new AssetContext("missing-test-guid"), };

            foreach (var context in realContextKinds)
            {
                var edit = commands.ForContext(context).Children.Single(item => item.Path == "Edit");
                Assert.That(edit.Children.Select(item => item.Path), Does.Contain("Edit/Rename"));
            }

            commands.ForContext(SceneRoot());

            Assert.That(
                commands.Paths.Count,
                Is.EqualTo(count),
                "Context/query changes reuse the discovery snapshot"
            );

            Assert.That(Selection.objects, Is.EqualTo(before));
            commands.Dispose();

            yield return null;
            Assert.That(commands.IsLoading, Is.False);
        }
    }
}