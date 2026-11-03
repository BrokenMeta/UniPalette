using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrokenMeta.UniPalette.Tests
{
    // One saved, empty scene per fixture. Never clear an arbitrary active/user scene.
    internal sealed class SharedTestScene : IDisposable
    {
        internal const string ScenePath = "Packages/com.brokenmeta.unipalette/Tests/Editor/Scenes/UniPaletteTestScene.unity";
        private readonly Scene previousActiveScene;
        private readonly bool wasActive;
        private readonly bool wasLoaded;
        private Scene scene;

        internal SharedTestScene()
        {
            previousActiveScene = SceneManager.GetActiveScene();
            wasActive = previousActiveScene.IsValid() && previousActiveScene.path == ScenePath;
            scene = SceneManager.GetSceneByPath(ScenePath);
            wasLoaded = scene.IsValid() && scene.isLoaded;
            if (wasLoaded)
            {
                Assert.That(scene.isDirty, Is.False, "The saved test scene has unsaved changes; refusing to use it.");
                Assert.That(scene.rootCount, Is.Zero, "The shared test scene must start empty.");
            }
            try
            {
                EnsureLoaded();
                Assert.That(scene.rootCount, Is.Zero, "The saved test fixture must be empty.");
            }
            catch
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                throw;
            }
            finally { RestorePreviousActiveScene(); }
        }

        internal Scene Use()
        {
            EnsureLoaded();
            Assert.That(scene.rootCount, Is.Zero, "A previous test left objects in the shared scene.");
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            return scene;
        }

        internal void ClearObjects()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return; // The closed-scene test deliberately unloads it.
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
        }

        private void EnsureLoaded()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        private void RestorePreviousActiveScene()
        {
            var previous = previousActiveScene;
            if (wasActive) previous = SceneManager.GetSceneByPath(ScenePath);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }

        public void Dispose()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            // Restore the initial loaded state without saving test mutations to the fixture asset.
            if (wasLoaded) EnsureLoaded();
            RestorePreviousActiveScene();
        }
    }
}
