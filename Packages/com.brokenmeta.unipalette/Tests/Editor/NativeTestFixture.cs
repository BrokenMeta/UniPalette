using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BrokenMeta.UniPalette.Editor;
using BrokenMeta.UniPalette.Editor.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Tests
{
    public abstract class NativeTestFixture
    {
        private protected const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private protected string _clipboard;

        private protected bool[] _dirty;

        private protected string _folder;

        private protected Object[] _originalSelection;

        private protected string _rootFolderGuid;

        private protected string _rootFolderPath;

        private protected SharedTestScene _sharedScene;

        private protected GameObject _target;

        private protected string _temporaryRoot;

        private protected Scene _testScene;

        private protected int _undoGroup;

        private bool _undoGroupStarted;

        private protected Scene[] _userScenes;

        private protected UniPaletteWindow _window;

        private protected static ContextItem CreateRoot(string kind) =>
            kind == "SceneRoot"
                ? new SceneRootContext()
                : new AssetsRootContext();

        private protected PaletteItem Context(string kind) =>
            kind == "GameObject"
                ? new SceneObjectContext(_target)
                : new AssetContext(
                    AssetDatabase.AssetPathToGUID(
                        kind == "Folder"
                            ? _folder
                            : _folder
                            + (kind == "OtherAsset"
                                ? "/Source.bytes"
                                : "/Source.txt")
                    )
                );

        private protected static PaletteItem Command(string path) =>
            new MenuCommandItem(path, path, "Test menu");

        private protected void Key(KeyCode key, EventModifiers modifiers = EventModifiers.None)
        {
            var search = _window.rootVisualElement.Q<VisualElement>("search");

            using var evt = KeyDownEvent.GetPooled(
                new Event { type = EventType.KeyDown, keyCode = key, modifiers = modifiers, }
            );

            evt.target = search;
            search.SendEvent(evt);
        }

        private protected void Click(VisualElement row, int count)
        {
            var evt = new Event
            {
                type = EventType.MouseDown, button = 0, clickCount = count, mousePosition = row.worldBound.center,
            };

            using (var down = PointerDownEvent.GetPooled(evt))
            {
                down.target = row;
                row.SendEvent(down);
            }

            evt.type = EventType.MouseUp;
            using var up = PointerUpEvent.GetPooled(evt);
            up.target = row;
            row.SendEvent(up);
        }

        [OneTimeSetUp]
        public void OpenSharedScene()
        {
            _sharedScene = SharedTestScene.Current;
        }

        [SetUp]
        public void SetUp()
        {
            _undoGroupStarted = false;
            _rootFolderPath = _rootFolderGuid = null;
            _clipboard = EditorGUIUtility.systemCopyBuffer;
            _originalSelection = Selection.objects;

            _userScenes = Enumerable
                .Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(s => s != _sharedScene.Scene)
                .ToArray();

            _dirty = _userScenes
                .Select(s => s.isDirty)
                .ToArray();

            _temporaryRoot = "Assets/UniPaletteNativeTest_"
                + Guid
                    .NewGuid()
                    .ToString("N");

            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_temporaryRoot));
            _testScene = _sharedScene.Use();
            _target = new GameObject("UniPalette execution test");
            _folder = _temporaryRoot + "/Data";
            AssetDatabase.CreateFolder(_temporaryRoot, "Data");
            File.WriteAllText(_folder + "/Source.txt", "Native execution fixture");
            AssetDatabase.ImportAsset(_folder + "/Source.txt");
            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            _undoGroupStarted = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_window)
                _window.Close();

            if (_rootFolderPath != null)
            {
                EditorUtility.FocusProjectWindow();

                EditorWindow.focusedWindow.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape, });

                // The generated root path was asserted absent before this test dispatched Create Folder.
                // Focusing Project during teardown can finish its pending rename after an early failure.
                if (string.IsNullOrEmpty(_rootFolderGuid)
                    && AssetDatabase.IsValidFolder(_rootFolderPath)
                    && Directory.GetFileSystemEntries(_rootFolderPath).Length == 0)
                    _rootFolderGuid = AssetDatabase.AssetPathToGUID(_rootFolderPath);

                if (!string.IsNullOrEmpty(_rootFolderGuid)
                    && AssetDatabase.AssetPathToGUID(_rootFolderPath) == _rootFolderGuid)
                    AssetDatabase.DeleteAsset(_rootFolderPath); // Only the root folder created by this test.
            }

            if (_undoGroupStarted)
                Undo.RevertAllDownToGroup(_undoGroup);

            _sharedScene.ClearObjects();

            if (!string.IsNullOrEmpty(_temporaryRoot))
                AssetDatabase.DeleteAsset(_temporaryRoot);

            Selection.objects = _originalSelection;
            EditorGUIUtility.systemCopyBuffer = _clipboard;

            for (var i = 0; i < _userScenes.Length; ++i)
                Assert.That(_userScenes[i].isDirty, Is.EqualTo(_dirty[i]), "User scene dirty state changed");
        }
    }
}