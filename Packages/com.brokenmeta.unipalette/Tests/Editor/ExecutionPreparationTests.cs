using System;
using System.Linq;
using System.Text.RegularExpressions;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

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
            Assert.That(new UnityContextSelection().MatchesSelection(context), Is.True);

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command("Edit/Duplicate"),
                    context,
                    out _,
                    out string error
                ),
                Is.True,
                error
            );

            Selection.activeObject = kind == "GameObject"
                ? AssetDatabase.LoadMainAssetAtPath(
                    _folder
                    + "/Source.txt"
                )
                : _target;

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command("Edit/Duplicate"),
                    context,
                    out _,
                    out error
                ),
                Is.False
            );

            Assert.That(error, Does.Contain("Selection"));
            Selection.objects = new[] { _target, AssetDatabase.LoadMainAssetAtPath(_folder + "/Source.txt"), };

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command("Edit/Duplicate"),
                    context,
                    out _,
                    out error
                ),
                Is.False
            );

            Assert.That(error, Does.Contain("Selection"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MenuCategoryCannotPrepareAsAnEnabledCommand(bool hasChildren)
        {
            var context = Context("GameObject");
            Selection.activeObject = _target;
            Assert.That(Menu.GetEnabled("Edit/Duplicate"), Is.True);

            var category = new MenuCategoryItem(
                "Duplicate",
                "Edit/Duplicate",
                hasChildren
                    ? new[]
                    {
                        Command(
                            "Edit/Duplicate/Child"
                        ),
                    }
                    : Array.Empty<PaletteItem>()
            );

            Assert.That(
                NativeExecution.TryPrepare(new UnityContextSelection(), category, context, out _, out _),
                Is.False
            );

            Assert.That(Selection.activeGameObject, Is.EqualTo(_target));
            Assert.That(_testScene.rootCount, Is.EqualTo(1));
        }

        [Test]
        public void DeletedTargetCannotPrepare()
        {
            var context = Context("GameObject");
            Selection.activeObject = _target;
            Object.DestroyImmediate(_target);

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command("Edit/Duplicate"),
                    context,
                    out _,
                    out string error
                ),
                Is.False
            );

            Assert.That(error, Does.Contain("no longer exists"));
        }

        [Test]
        public void SelectionChangedAfterCloseAbortsOnceAndReportsHonestly()
        {
            var context = Context("GameObject");
            Selection.activeObject = _target;

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command("Edit/Duplicate"),
                    context,
                    out var request,
                    out _
                ),
                Is.True
            );

            Selection.activeObject = null;
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch Edit/Duplicate.*Selection"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(_testScene.rootCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualRootsNeverPrepareAgainstAmbientSelection()
        {
            Selection.activeObject = _target;

            foreach (string kind in new[] { "SceneRoot", "AssetsRoot", }
            )
            {
                Assert.That(
                    NativeExecution.TryPrepare(
                        new UnityContextSelection(),
                        Command(
                            kind == "SceneRoot"
                                ? "GameObject/Create Empty"
                                : "Assets/Copy Path"
                        ),
                        CreateRoot(
                            kind
                        ),
                        out _,
                        out _
                    ),
                    Is.False
                );
            }

            Assert.That(Selection.activeGameObject, Is.EqualTo(_target));
            Assert.That(_testScene.rootCount, Is.EqualTo(1));
        }

        [TestCase("GameObject", "GameObject/Create Empty Child")]
        [TestCase("Asset", "Assets/Copy Path")]
        public void SelectionMismatchConsumesRequestEvenIfSelectionIsRestored(string kind, string path)
        {
            var context = Context(kind);
            Selection.activeObject = context.Target;

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command(path),
                    context,
                    out var request,
                    out string error
                ),
                Is.True,
                error
            );

            Selection.objects = new[] { _target, Context("Asset").Target, };

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command(path),
                    context,
                    out _,
                    out error
                ),
                Is.False
            );

            Assert.That(error, Does.Contain("Selection"));
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*Selection"));
            request.ExecuteAfterClose();
            Selection.activeObject = context.Target;
            request.ExecuteAfterClose();
            Assert.That(_target.transform.childCount, Is.Zero);
            Assert.That(_target.GetComponents<BoxCollider>(), Is.Empty);
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(_clipboard));
        }

        [Test]
        public void DeletedTargetAfterPreparationAbortsOnce()
        {
            Selection.activeObject = _target;

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command("Edit/Duplicate"),
                    Context(
                        "GameObject"
                    ),
                    out var request,
                    out _
                ),
                Is.True
            );

            Object.DestroyImmediate(_target);
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*no longer exists"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(_testScene.rootCount, Is.Zero);
        }

        [Test]
        public void DeletedAssetAfterPreparationAbortsOnce()
        {
            var context = Context("Asset");
            Selection.activeObject = context.Target;

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command("Assets/Copy Path"),
                    context,
                    out var request,
                    out _
                ),
                Is.True
            );

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(context.Target)); // Only this test's disposable asset.
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*no longer exists"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(_clipboard));
        }

        [Test]
        public void DisabledMenuFlagIsReadInsteadOfCachedEnabledNote()
        {
            Selection.activeObject = _target;
            var context = Context("GameObject");
            // A removed menu leaf can survive in an old discovery snapshot. Never dispatch it.
            const string Path = "GameObject/UniPalette Missing Command 004.4";
            var staleTree = MenuCommands.Build(new[] { Path, }, context, true, _ => true);

            var staleCommand = staleTree
                .Children
                .Single()
                .Children
                .Single();

            Assert.That(staleCommand.AvailabilityNote, Does.Contain("Menu enabled"));
            Assert.That(Menu.GetEnabled(Path), Is.False);

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    staleCommand,
                    context,
                    out _,
                    out string error
                ),
                Is.False
            );

            Assert.That(error, Does.Contain("disabled"));
            Assert.That(Selection.activeGameObject, Is.EqualTo(_target));
            Assert.That(_testScene.rootCount, Is.EqualTo(1));
        }

        [TestCase("SceneRoot", "GameObject/Create Empty")]
        [TestCase("AssetsRoot", "Assets/Copy Path")]
        public void RootRequestRejectsChangedSelectionAndNeverRepairsIt(string kindName, string path)
        {
            string kind = kindName;
            var context = CreateRoot(kind);
            Selection.activeObject = _target;
            Assert.That(new UnityContextSelection().TryConfirm(context, out string error), Is.True, error);

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command(path),
                    context,
                    out var request,
                    out error
                ),
                Is.True,
                error
            );

            Selection.activeObject = _target;

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command(path),
                    context,
                    out _,
                    out error
                ),
                Is.False
            );

            Assert.That(error, Does.Contain("Selection"));
            LogAssert.Expect(LogType.Warning, new Regex("UniPalette: could not dispatch .*Selection"));
            request.ExecuteAfterClose();
            request.ExecuteAfterClose();
            Assert.That(Selection.activeGameObject, Is.EqualTo(_target));
            Assert.That(_testScene.rootCount, Is.EqualTo(1));
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(_clipboard));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneRootRequestRejectsChangedOrClosedScene(bool closeScene)
        {
            var alternate = closeScene ? default : _userScenes.FirstOrDefault(s => s.IsValid() && s.isLoaded);
            var openedAlternate = false;

            if (!alternate.IsValid())
            {
                alternate = _sharedScene.OpenDisposableScene();
                openedAlternate = true;
            }

            bool alternateWasDirty = alternate.isDirty;
            // Closing the captured scene must never close the shared session scene.
            SceneManager.SetActiveScene(closeScene ? alternate : _testScene);

            try
            {
                var context = new SceneRootContext();
                Assert.That(new UnityContextSelection().TryConfirm(context, out _), Is.True);

                Assert.That(
                    NativeExecution.TryPrepare(
                        new UnityContextSelection(),
                        Command(
                            "GameObject/Create Empty"
                        ),
                        context,
                        out var request,
                        out _
                    ),
                    Is.True
                );

                if (closeScene)
                {
                    SceneManager.SetActiveScene(_testScene);
                    EditorSceneManager.CloseScene(alternate, true);
                }
                else
                {
                    SceneManager.SetActiveScene(alternate);
                }

                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("UniPalette: could not dispatch .*active scene changed")
                );

                request.ExecuteAfterClose();
                request.ExecuteAfterClose();
                Assert.That(Selection.count, Is.Zero);

                if (alternate.IsValid() && alternate.isLoaded)
                    Assert.That(alternate.isDirty, Is.EqualTo(alternateWasDirty));

                Assert.That(
                    _testScene.IsValid() && _testScene.isLoaded,
                    Is.True,
                    "The main session scene stays loaded"
                );

                Assert.That(_testScene.rootCount, Is.EqualTo(1));
            }
            finally
            {
                SceneManager.SetActiveScene(_testScene);

                if (openedAlternate && alternate.IsValid() && alternate.isLoaded)
                    EditorSceneManager.CloseScene(alternate, true);
            }
        }

        [TestCase("SceneRoot", "GameObject/UniPalette Missing Command 004.5")]
        [TestCase("AssetsRoot", "Assets/UniPalette Missing Command 004.5")]
        public void VirtualRootRejectsDisabledMenu(string kindName, string path)
        {
            string kind = kindName;
            var context = CreateRoot(kind);
            Assert.That(new UnityContextSelection().TryConfirm(context, out _), Is.True);
            Assert.That(Menu.GetEnabled(path), Is.False);

            Assert.That(
                NativeExecution.TryPrepare(
                    new UnityContextSelection(),
                    Command(path),
                    context,
                    out _,
                    out string error
                ),
                Is.False
            );

            Assert.That(error, Does.Contain("disabled"));
        }
    }
}