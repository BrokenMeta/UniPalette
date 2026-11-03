using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BrokenMeta.UniPalette.Tests
{
    // Owned by the namespace SetUpFixture, not by individual test classes.
    // Lazy initialization keeps Fast runs free of scene/asset setup.
    internal sealed class SharedTestScene : IDisposable
    {
        private const string Template = "Packages/com.brokenmeta.unipalette/Tests/Editor/Scenes/UniPaletteTestScene.unity";
        internal static SharedTestScene Current { get; set; }
        internal Scene Scene => scene;
        private readonly Scene previousActiveScene = SceneManager.GetActiveScene();
        private Scene scene;
        private string folder, folderGuid;
        private bool disposed;

        internal Scene Use()
        {
            Assert.That(disposed, Is.False, "The test scene session has ended.");
            if (folder == null)
            {
                folder = "Assets/UniPaletteSceneSession_" + Guid.NewGuid().ToString("N");
                folderGuid = AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
                Assert.That(folderGuid, Is.Not.Empty, "Could not create the disposable scene folder.");
                scene = OpenCopy("Session");
            }
            // The runner itself opens an untitled scene. Loading a saved copy works alongside it;
            // NewScene(Additive) does not. Never save or replace that runner/user scene.
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True,
                "The session scene was closed unexpectedly; do not recreate it between tests.");
            Assert.That(scene.rootCount, Is.Zero, "A previous test left objects in the session scene.");
            if (SceneManager.GetActiveScene() != scene)
                Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            return scene;
        }

        private Scene OpenCopy(string name)
        {
            var path = folder + "/" + name + ".unity";
            Assert.That(AssetDatabase.CopyAsset(Template, path), Is.True, "Could not copy the empty scene template.");
            var copy = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Assert.That(copy.rootCount, Is.Zero, "The scene template must be empty.");
                return copy;
            }
            catch
            {
                EditorSceneManager.CloseScene(copy, true);
                throw;
            }
        }

        // Only the regression that deliberately closes its captured scene needs a second copy.
        internal Scene OpenDisposableScene() => OpenCopy("ClosedScene");

        internal void ClearObjects()
        {
            if (folder == null || !scene.IsValid()) return; // Setup may fail before opening the scene.
            Assert.That(scene.isLoaded, Is.True, "The session scene must remain loaded.");
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    ClearObjects();
                    if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                        SceneManager.SetActiveScene(previousActiveScene);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                try
                {
                    // Delete only the folder created by this session, even if a test failed.
                    if (!string.IsNullOrEmpty(folderGuid) && AssetDatabase.AssetPathToGUID(folder) == folderGuid)
                        AssetDatabase.DeleteAsset(folder);
                }
                finally { if (ReferenceEquals(Current, this)) Current = null; }
            }
        }
    }
}
