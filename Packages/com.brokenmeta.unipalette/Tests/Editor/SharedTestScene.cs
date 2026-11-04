using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Tests
{
    // Owned by the namespace SetUpFixture, not by individual test classes.
    // Lazy initialization keeps Fast runs free of scene/asset setup.
    internal sealed class SharedTestScene : IDisposable
    {
        private const string Template =
            "Packages/com.brokenmeta.unipalette/Tests/Editor/Scenes/UniPaletteTestScene.unity";

        private readonly Scene _previousActiveScene = SceneManager.GetActiveScene();

        private bool _disposed;

        private string _folder;

        private string _folderGuid;

        private Scene _scene;

        internal static SharedTestScene Current { get; set; }

        internal Scene Scene => _scene;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                if (_scene.IsValid() && _scene.isLoaded)
                {
                    ClearObjects();

                    if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
                        SceneManager.SetActiveScene(_previousActiveScene);

                    EditorSceneManager.CloseScene(_scene, true);
                }
            }
            finally
            {
                try
                {
                    // Delete only the folder created by this session, even if a test failed.
                    if (!string.IsNullOrEmpty(_folderGuid) && AssetDatabase.AssetPathToGUID(_folder) == _folderGuid)
                        AssetDatabase.DeleteAsset(_folder);
                }
                finally
                {
                    if (ReferenceEquals(Current, this))
                        Current = null;
                }
            }
        }

        private Scene OpenCopy(string name)
        {
            var path = _folder + "/" + name + ".unity";

            Assert.That(
                AssetDatabase.CopyAsset(Template, path),
                Is.True,
                "Could not copy the empty scene template."
            );

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

        internal Scene Use()
        {
            Assert.That(_disposed, Is.False, "The test scene session has ended.");

            if (_folder == null)
            {
                _folder = "Assets/UniPaletteSceneSession_"
                    + Guid
                        .NewGuid()
                        .ToString("N");

                _folderGuid = AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
                Assert.That(_folderGuid, Is.Not.Empty, "Could not create the disposable scene folder.");
                _scene = OpenCopy("Session");
            }

            // The runner itself opens an untitled scene. Loading a saved copy works alongside it;
            // NewScene(Additive) does not. Never save or replace that runner/user scene.
            Assert.That(
                _scene.IsValid() && _scene.isLoaded,
                Is.True,
                "The session scene was closed unexpectedly; do not recreate it between tests."
            );

            Assert.That(_scene.rootCount, Is.Zero, "A previous test left objects in the session scene.");

            if (SceneManager.GetActiveScene() != _scene)
                Assert.That(SceneManager.SetActiveScene(_scene), Is.True);

            return _scene;
        }

        // Only the regression that deliberately closes its captured scene needs a second copy.
        internal Scene OpenDisposableScene() =>
            OpenCopy("ClosedScene");

        internal void ClearObjects()
        {
            if (_folder == null || !_scene.IsValid()) return; // Setup may fail before opening the scene.

            Assert.That(_scene.isLoaded, Is.True, "The session scene must remain loaded.");

            foreach (var root in _scene.GetRootGameObjects())
                Object.DestroyImmediate(root);
        }
    }
}