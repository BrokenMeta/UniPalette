using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("CommandDiscovery")]
    [Category("Fast")]
    public class CommandEligibilityTests
    {
        private static void AssertRelevance(string path, PaletteItem context, bool expected)
        {
            Assert.That(new MenuRelevance(path).Matches(context), Is.EqualTo(expected), path);
            Assert.That(MenuCommands.InScope(path, context), Is.EqualTo(expected), path);
        }

        [Test]
        public void RelevanceRequiresNativeContextAndOrdinalFamilyPrefix()
        {
            AssertRelevance("Assets/Copy Path", new FixtureItem("Asset", "Assets/Asset"), false);
            AssertRelevance("AssetsExtra/Copy Path", new AssetsRootContext(), false);
            AssertRelevance("assets/Copy Path", new AssetsRootContext(), false);
            AssertRelevance("GameObjectExtra/Create Empty", new SceneRootContext(), false);
            AssertRelevance("ComponentExtra/Box Collider", new SceneObjectContext(null), false);
        }

        // Family relevance does not inspect targets or Unity menu state.
        // Invalid targets are intentional here; execution validation has its own tests.
        [TestCase("Object", true, true, false, true)]
        [TestCase("Asset", false, false, true, true)]
        [TestCase("SceneRoot", true, false, false, false)]
        [TestCase("AssetsRoot", false, false, true, false)]
        public void ContextAllowsOnlyItsMenuFamilies(
            string kind,
            bool gameObject,
            bool component,
            bool asset,
            bool duplicate)
        {
            var context = kind == "Object"
                ? new SceneObjectContext(null)
                : kind == "Asset"
                    ? new AssetContext("missing-test-guid")
                    : kind == "SceneRoot"
                        ? (PaletteItem)new SceneRootContext()
                        : new AssetsRootContext();

            foreach (var path in new[] { "GameObject/Create Empty", "GameObject/Create Empty Child", }
            )
                AssertRelevance(path, context, gameObject);

            foreach (var path in new[] { "Component/Physics/Box Collider", "Component/Physics/Sphere Collider", }
            )
                AssertRelevance(path, context, component);

            foreach (var path in new[] { "Assets/Copy Path", "Assets/Create/Folder", }
            )
                AssertRelevance(path, context, asset);

            AssertRelevance("Edit/Duplicate", context, duplicate);
            AssertRelevance("Window/General/Inspector", context, false);
            AssertRelevance(null, context, false);
            AssertRelevance("", context, false);
            AssertRelevance("Assets/Copy Path", null, false);
        }
    }
}