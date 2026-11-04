using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor;

namespace BrokenMeta.UniPalette.Tests
{
    public abstract class ContextTestFixture
    {
        private protected Scene scene;
        private protected Object[] previousSelection;
        private protected string folder;
        private protected string temporaryRoot;
        private protected UniPaletteWindow testWindow;

        private protected SharedTestScene sharedScene;


        [OneTimeSetUp]
        public void OpenSharedScene() => sharedScene = SharedTestScene.Current;

        [SetUp]
        public void SetUp()
        {
            previousSelection = Selection.objects;
            temporaryRoot = "Assets/UniPaletteTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(temporaryRoot));
            scene = sharedScene.Use();
            AssetDatabase.CreateFolder(temporaryRoot, "Data");
            folder = temporaryRoot + "/Data";
            Selection.objects = Array.Empty<Object>();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = previousSelection;
            sharedScene.ClearObjects();
            if (!string.IsNullOrEmpty(temporaryRoot)) AssetDatabase.DeleteAsset(temporaryRoot);
            temporaryRoot = null;
            folder = null;
            if (testWindow)
                typeof(UniPaletteWindow).GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(testWindow, null);
            testWindow = null;
        }

        private protected GameObject Go(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            if (parent) go.transform.SetParent(parent.transform);
            return go;
        }

        private protected static void Choose(PaletteSession session, string title)
        {
            var index = session.Results.FindIndex(item => item.Title == title);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), title);
            session.Select(index);
        }

        private protected void CreateAssets()
        {
            AssetDatabase.CreateFolder(folder, "A");
            AssetDatabase.CreateFolder(folder, "AB");
            AssetDatabase.CreateFolder(folder + "/A", "Nested");
            WriteText(folder + "/A/Nested/Needle.txt");
            WriteText(folder + "/AB/Needle.txt");
        }

        private protected static void WriteText(string path)
        {
            File.WriteAllText(path, "UniPalette temporary test asset");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
