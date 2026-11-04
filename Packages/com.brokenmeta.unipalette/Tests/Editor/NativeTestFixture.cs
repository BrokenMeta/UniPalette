using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor;

namespace BrokenMeta.UniPalette.Tests
{
    public abstract class NativeTestFixture
    {
        private protected const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private protected Scene testScene;
        private protected Scene[] userScenes;
        private protected bool[] dirty;
        private protected GameObject target;
        private protected string folder;
        private protected string temporaryRoot;
        private protected UniPaletteWindow window;
        private protected int undoGroup;
        private bool undoGroupStarted;
        private protected string clipboard;
        private protected string rootFolderPath, rootFolderGuid;
        private protected UnityEngine.Object[] originalSelection;

        private protected SharedTestScene sharedScene;


        [OneTimeSetUp]
        public void OpenSharedScene() => sharedScene = SharedTestScene.Current;

        [SetUp]
        public void SetUp()
        {
            undoGroupStarted = false;
            rootFolderPath = rootFolderGuid = null;
            clipboard = EditorGUIUtility.systemCopyBuffer;
            originalSelection = Selection.objects;
            userScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .Where(s => s != sharedScene.Scene).ToArray();
            dirty = userScenes.Select(s => s.isDirty).ToArray();
            temporaryRoot = "Assets/UniPaletteNativeTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(temporaryRoot));
            testScene = sharedScene.Use();
            target = new GameObject("UniPalette execution test");
            folder = temporaryRoot + "/Data";
            AssetDatabase.CreateFolder(temporaryRoot, "Data");
            File.WriteAllText(folder + "/Source.txt", "Native execution fixture");
            AssetDatabase.ImportAsset(folder + "/Source.txt");
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            undoGroupStarted = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (window) window.Close();
            if (rootFolderPath != null)
            {
                EditorUtility.FocusProjectWindow();
                EditorWindow.focusedWindow.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                if (!string.IsNullOrEmpty(rootFolderGuid) && AssetDatabase.AssetPathToGUID(rootFolderPath) == rootFolderGuid)
                    AssetDatabase.DeleteAsset(rootFolderPath); // Only the root folder created by this test.
            }
            if (undoGroupStarted) Undo.RevertAllDownToGroup(undoGroup);
            sharedScene.ClearObjects();
            if (!string.IsNullOrEmpty(temporaryRoot)) AssetDatabase.DeleteAsset(temporaryRoot);
            Selection.objects = originalSelection;
            EditorGUIUtility.systemCopyBuffer = clipboard;
            for (var i = 0; i < userScenes.Length; ++i)
                Assert.That(userScenes[i].isDirty, Is.EqualTo(dirty[i]), "User scene dirty state changed");
        }

        private protected static ContextItem CreateRoot(string kind) => kind == "SceneRoot"
            ? (ContextItem)new SceneRootContext() : new AssetsRootContext();

        private protected PaletteItem Context(string kind) => kind == "GameObject"
            ? (PaletteItem)new SceneObjectContext(target)
            : new AssetContext(guid: AssetDatabase.AssetPathToGUID(
                kind == "Folder" ? folder : folder + (kind == "OtherAsset" ? "/Source.bytes" : "/Source.txt")));

        private protected static PaletteItem Command(string path) => new MenuCommandItem(path, path, "Test menu");

        private protected void Key(KeyCode key, EventModifiers modifiers = EventModifiers.None)
        {
            var search = window.rootVisualElement.Q<VisualElement>("search");
            using var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = key, modifiers = modifiers });
            evt.target = search; search.SendEvent(evt);
        }

        private protected void Click(VisualElement row, int count)
        {
            var evt = new Event { type = EventType.MouseDown, button = 0, clickCount = count, mousePosition = row.worldBound.center };
            using (var down = PointerDownEvent.GetPooled(evt)) { down.target = row; row.SendEvent(down); }
            evt.type = EventType.MouseUp;
            using var up = PointerUpEvent.GetPooled(evt); up.target = row; row.SendEvent(up);
        }
    }
}
