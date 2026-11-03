using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BrokenMeta.UniPalette.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("ExecutionPreparation")]
    [Category("EditorIntegration")]
    public class ExecutionPreparationTests : NativeTestFixture
    {

        [TestCase("GameObject")]
        [TestCase("Asset")]
        [TestCase("Folder")]
        public void PreparationRequiresExactSingleSelection(string kind)
        {
            var context = Context(kind);
            Selection.activeObject = context.Target;
            Assert.That(MenuCommands.MatchesSelection(context), Is.True);
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out _, out var error), Is.True, error);
            Selection.activeObject = kind == "GameObject" ? AssetDatabase.LoadMainAssetAtPath(folder + "/Source.txt") : target;
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection"));
            Selection.objects = new UnityEngine.Object[] { target, AssetDatabase.LoadMainAssetAtPath(folder + "/Source.txt") };
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MenuCategoryCannotPrepareAsAnEnabledCommand(bool hasChildren)
        {
            var context = Context("GameObject");
            Selection.activeObject = target;
            Assert.That(Menu.GetEnabled("Edit/Duplicate"), Is.True);
            var category = new MenuCategoryItem("Duplicate", "Edit/Duplicate", hasChildren ? new[] { Command("Edit/Duplicate/Child") } : Array.Empty<PaletteItem>());
            Assert.That(NativeExecution.TryPrepare(category, context, out _, out _), Is.False);
            Assert.That(Selection.activeGameObject, Is.EqualTo(target));
            Assert.That(testScene.rootCount, Is.EqualTo(1));
        }

        [Test]
        public void DeletedTargetCannotPrepare()
        {
            var context = Context("GameObject");
            Selection.activeObject = target;
            UnityEngine.Object.DestroyImmediate(target);
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("no longer exists"));
        }

        [Test]
        public void SelectionChangedAfterCloseAbortsOnceAndReportsHonestly()
        {
            var context = Context("GameObject");
            Selection.activeObject = target;
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), context, out var request, out _), Is.True);
            Selection.activeObject = null;
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch Edit/Duplicate.*Selection"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(testScene.rootCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualRootsNeverPrepareAgainstAmbientSelection()
        {
            Selection.activeObject = target;
            foreach (var kind in new[] { "SceneRoot", "AssetsRoot" })
                Assert.That(NativeExecution.TryPrepare(Command(kind == "SceneRoot"
                    ? "GameObject/Create Empty" : "Assets/Copy Path"), CreateRoot(kind), out _, out _), Is.False);
            Assert.That(Selection.activeGameObject, Is.EqualTo(target));
            Assert.That(testScene.rootCount, Is.EqualTo(1));
        }

        [TestCase("GameObject", "GameObject/Create Empty Child")]
        [TestCase("Asset", "Assets/Copy Path")]
        public void SelectionMismatchConsumesRequestEvenIfSelectionIsRestored(string kind, string path)
        {
            var context = Context(kind);
            Selection.activeObject = context.Target;
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out var request, out var error), Is.True, error);
            Selection.objects = new UnityEngine.Object[] { target, Context("Asset").Target };
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection"));
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*Selection"));
            request.ExecuteAfterClose();
            Selection.activeObject = context.Target;
            request.ExecuteAfterClose();
            Assert.That(target.transform.childCount, Is.Zero);
            Assert.That(target.GetComponents<BoxCollider>(), Is.Empty);
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(clipboard));
        }

        [Test]
        public void DeletedTargetAfterPreparationAbortsOnce()
        {
            Selection.activeObject = target;
            Assert.That(NativeExecution.TryPrepare(Command("Edit/Duplicate"), Context("GameObject"), out var request, out _), Is.True);
            UnityEngine.Object.DestroyImmediate(target);
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*no longer exists"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(testScene.rootCount, Is.Zero);
        }

        [Test]
        public void DeletedAssetAfterPreparationAbortsOnce()
        {
            var context = Context("Asset");
            Selection.activeObject = context.Target;
            Assert.That(NativeExecution.TryPrepare(Command("Assets/Copy Path"), context, out var request, out _), Is.True);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(context.Target)); // Only this test's disposable asset.
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*no longer exists"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(clipboard));
        }

        [Test]
        public void DisabledMenuFlagIsReadInsteadOfCachedEnabledNote()
        {
            Selection.activeObject = target;
            var context = Context("GameObject");
            // A removed menu leaf can survive in an old discovery snapshot. Never dispatch it.
            const string path = "GameObject/UniPalette Missing Command 004.4";
            var staleTree = MenuCommands.Build(new[] { path }, context, true, _ => true);
            var staleCommand = staleTree.Children.Single().Children.Single();
            Assert.That(staleCommand.AvailabilityNote, Does.Contain("Menu enabled"));
            Assert.That(Menu.GetEnabled(path), Is.False);
            Assert.That(NativeExecution.TryPrepare(staleCommand, context, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("disabled"));
            Assert.That(Selection.activeGameObject, Is.EqualTo(target));
            Assert.That(testScene.rootCount, Is.EqualTo(1));
        }

        [TestCase("SceneRoot", "GameObject/Create Empty")]
        [TestCase("AssetsRoot", "Assets/Copy Path")]
        public void RootRequestRejectsChangedSelectionAndNeverRepairsIt(string kindName, string path)
        {
            var kind = kindName;
            var context = CreateRoot(kind);
            Selection.activeObject = target;
            Assert.That(context.TryConfirm(out var error), Is.True, error);
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out var request, out error), Is.True, error);
            Selection.activeObject = target;
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out _, out error), Is.False);
            Assert.That(error, Does.Contain("Selection"));
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*Selection"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(Selection.activeGameObject, Is.EqualTo(target));
            Assert.That(testScene.rootCount, Is.EqualTo(1));
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(clipboard));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneRootRequestRejectsChangedOrClosedScene(bool closeScene)
        {
            var alternate = closeScene ? default : userScenes.FirstOrDefault(s => s.IsValid() && s.isLoaded);
            var openedAlternate = false;
            if (!alternate.IsValid())
            {
                alternate = sharedScene.OpenDisposableScene();
                openedAlternate = true;
            }
            var alternateWasDirty = alternate.isDirty;
            // Closing the captured scene must never close the shared session scene.
            SceneManager.SetActiveScene(closeScene ? alternate : testScene);
            try
            {
                var context = new SceneRootContext();
                Assert.That(context.TryConfirm(out _), Is.True);
                Assert.That(NativeExecution.TryPrepare(Command("GameObject/Create Empty"), context, out var request, out _), Is.True);
                if (closeScene)
                {
                    SceneManager.SetActiveScene(testScene);
                    EditorSceneManager.CloseScene(alternate, true);
                }
                else SceneManager.SetActiveScene(alternate);
                LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*active scene changed"));
                request.ExecuteAfterClose();
                request.ExecuteAfterClose();
                Assert.That(Selection.count, Is.Zero);
                if (alternate.IsValid() && alternate.isLoaded)
                    Assert.That(alternate.isDirty, Is.EqualTo(alternateWasDirty));
                Assert.That(testScene.IsValid() && testScene.isLoaded, Is.True, "The main session scene stays loaded");
                Assert.That(testScene.rootCount, Is.EqualTo(1));
            }
            finally
            {
                SceneManager.SetActiveScene(testScene);
                if (openedAlternate && alternate.IsValid() && alternate.isLoaded)
                    EditorSceneManager.CloseScene(alternate, true);
            }
        }

        [TestCase("SceneRoot", "GameObject/UniPalette Missing Command 004.5")]
        [TestCase("AssetsRoot", "Assets/UniPalette Missing Command 004.5")]
        public void VirtualRootRejectsDisabledMenu(string kindName, string path)
        {
            var kind = kindName;
            var context = CreateRoot(kind);
            Assert.That(context.TryConfirm(out _), Is.True);
            Assert.That(Menu.GetEnabled(path), Is.False);
            Assert.That(NativeExecution.TryPrepare(Command(path), context, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("disabled"));
        }
    }
}
