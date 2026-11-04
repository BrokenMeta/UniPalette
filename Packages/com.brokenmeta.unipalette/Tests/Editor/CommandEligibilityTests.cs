using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("CommandDiscovery")]
    [Category("Fast")]
    public class CommandEligibilityTests
    {
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
                    ? new AssetContext(
                        "missing-test-guid"
                    )
                    : kind == "SceneRoot"
                        ? (PaletteItem)new SceneRootContext()
                        : new AssetsRootContext();

            foreach (string path in new[] { "GameObject/Create Empty", "GameObject/Create Empty Child", }
            )
                Assert.That(NativeExecution.Supports(path, context), Is.EqualTo(gameObject), path);

            foreach (string path in new[] { "Component/Physics/Box Collider", "Component/Physics/Sphere Collider", }
            )
                Assert.That(NativeExecution.Supports(path, context), Is.EqualTo(component), path);

            foreach (string path in new[] { "Assets/Copy Path", "Assets/Create/Folder", }
            )
                Assert.That(NativeExecution.Supports(path, context), Is.EqualTo(asset), path);

            Assert.That(NativeExecution.Supports("Edit/Duplicate", context), Is.EqualTo(duplicate));
            Assert.That(NativeExecution.Supports("Window/General/Inspector", context), Is.False);
            Assert.That(NativeExecution.Supports(null, context), Is.False);
            Assert.That(NativeExecution.Supports("", context), Is.False);
            Assert.That(NativeExecution.Supports("Assets/Copy Path", null), Is.False);
        }
    }
}