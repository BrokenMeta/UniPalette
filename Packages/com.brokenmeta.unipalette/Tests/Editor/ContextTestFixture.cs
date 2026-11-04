using System;
using System.IO;
using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using BrokenMeta.UniPalette.Editor.Application;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Tests
{
    public abstract class ContextTestFixture
    {
        private protected string _folder;

        private protected Object[] _previousSelection;
        private protected Scene _scene;

        private protected SharedTestScene _sharedScene;

        private protected string _temporaryRoot;

        private protected UniPaletteWindow _testWindow;

        private protected GameObject Go(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, _scene);

            if (parent)
                go.transform.SetParent(parent.transform);

            return go;
        }

        private protected static void Choose(PaletteSession session, string title)
        {
            var index = session.FindResultIndex(item => item.Title == title);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), title);
            session.Select(index);
        }

        private protected void CreateAssets()
        {
            AssetDatabase.CreateFolder(_folder, "A");
            AssetDatabase.CreateFolder(_folder, "AB");
            AssetDatabase.CreateFolder(_folder + "/A", "Nested");
            WriteText(_folder + "/A/Nested/Needle.txt");
            WriteText(_folder + "/AB/Needle.txt");
        }

        private protected static void WriteText(string path)
        {
            File.WriteAllText(path, "UniPalette temporary test asset");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        [OneTimeSetUp]
        public void OpenSharedScene()
        {
            _sharedScene = SharedTestScene.Current;
        }

        [SetUp]
        public void SetUp()
        {
            _previousSelection = Selection.objects;

            _temporaryRoot = "Assets/UniPaletteTests_"
                + Guid
                    .NewGuid()
                    .ToString("N");

            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryRoot));
            _scene = _sharedScene.Use();
            AssetDatabase.CreateFolder(_temporaryRoot, "Data");
            _folder = _temporaryRoot + "/Data";
            Selection.objects = Array.Empty<Object>();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = _previousSelection;
            _sharedScene.ClearObjects();

            if (!string.IsNullOrEmpty(_temporaryRoot))
                AssetDatabase.DeleteAsset(_temporaryRoot);

            _temporaryRoot = null;
            _folder = null;

            if (_testWindow)
                typeof(UniPaletteWindow)
                    .GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(
                        _testWindow,
                        null
                    );

            _testWindow = null;
        }
    }
}