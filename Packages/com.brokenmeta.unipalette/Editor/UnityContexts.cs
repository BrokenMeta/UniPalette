using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor
{
    // Concrete Editor data access. Assets retain GUIDs across moves; scene objects retain references.
    internal static class UnityContexts
    {
        internal enum Kind { Fixture, SceneRoot, AssetsRoot, GameObject, Asset, Menu }

        internal static PaletteState.Item[] Roots()
        {
            var roots = new List<PaletteState.Item>();
            var selection = CurrentSelection();
            if (selection != null) roots.Add(selection);
            roots.Add(new PaletteState.Item(Kind.SceneRoot));
            roots.Add(new PaletteState.Item(Kind.AssetsRoot));
            return roots.ToArray();
        }

        internal static PaletteState.Item CurrentSelection()
        {
            // Deliberately single-object only. Do not reinterpret components, sub-assets or multi-selection.
            if (Selection.objects.Length != 1) return null;
            var target = Selection.activeObject;
            if (target is GameObject go && !EditorUtility.IsPersistent(go) && IsSceneObject(go))
                return new PaletteState.Item(Kind.GameObject, go, selectionEntry: true);
            var path = AssetDatabase.GetAssetPath(target);
            if (target && IsProjectPath(path) && AssetDatabase.IsMainAsset(target))
                return new PaletteState.Item(Kind.Asset, guid: AssetDatabase.AssetPathToGUID(path), selectionEntry: true);
            return null;
        }

        private static bool IsProjectPath(string path) => !string.IsNullOrEmpty(path)
            && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.StartsWith("Packages/", StringComparison.Ordinal));

        private static bool IsSceneObject(GameObject go) => go && go.scene.IsValid() && go.scene.isLoaded
            && !EditorSceneManager.IsPreviewScene(go.scene);

        internal static bool IsValid(PaletteState.Item item)
        {
            switch (item.Kind)
            {
                case Kind.GameObject: return IsSceneObject(item.SceneObject);
                case Kind.Asset:
                    var path = item.AssetPath;
                    // GUIDToAssetPath may retain a deleted asset in its cache until a refresh.
                    return IsProjectPath(path)
                        && AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets) == item.AssetGuid;
                default: return true;
            }
        }

        internal static bool CanBrowse(PaletteState.Item item)
        {
            switch (item.Kind)
            {
                case Kind.SceneRoot:
                case Kind.AssetsRoot: return true;
                case Kind.GameObject: return IsValid(item) && item.SceneObject.transform.childCount > 0;
                case Kind.Asset: return AssetDatabase.IsValidFolder(item.AssetPath);
                default: return item.Children.Length > 0;
            }
        }

        internal static string Path(PaletteState.Item item)
        {
            switch (item.Kind)
            {
                case Kind.SceneRoot: return "Scene Root";
                case Kind.AssetsRoot: return "Assets";
                case Kind.Asset: return item.AssetPath;
                case Kind.GameObject:
                    if (!IsValid(item)) return "Missing object";
                    var segments = new List<string>();
                    for (var t = item.SceneObject.transform; t; t = t.parent)
                    {
                        // Sibling indices also disambiguate identical names under the same parent.
                        segments.Add(t.name + " [" + t.GetSiblingIndex() + "]");
                    }
                    segments.Reverse();
                    var scene = item.SceneObject.scene;
                    return "Scene Root/" + scene.name + " [" + scene.handle + "]/" + string.Join("/", segments);
                default: return "";
            }
        }

        internal static string Title(PaletteState.Item item)
        {
            switch (item.Kind)
            {
                case Kind.SceneRoot: return "Hierarchy";
                case Kind.AssetsRoot: return "Project";
                case Kind.GameObject: return item.SceneObject ? item.SceneObject.name : "Missing object";
                case Kind.Asset: return System.IO.Path.GetFileName(item.AssetPath);
                default: return "";
            }
        }

        internal static Object Target(PaletteState.Item item) => item.Kind == Kind.GameObject
            ? item.SceneObject : item.Kind == Kind.Asset ? AssetDatabase.LoadMainAssetAtPath(item.AssetPath) : null;

        internal static IEnumerable<PaletteState.Item> Children(PaletteState.Item context, bool recursive)
        {
            if (!IsValid(context)) yield break;
            if (context.Kind == Kind.AssetsRoot || context.Kind == Kind.Asset)
            {
                var folder = context.Kind == Kind.AssetsRoot ? "Assets" : context.AssetPath;
                if (!AssetDatabase.IsValidFolder(folder)) yield break;
                // One scoped database query, rather than a recursive query for every nested folder.
                var paths = new List<string>();
                foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith(folder + "/", StringComparison.Ordinal)
                        && (recursive || System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') == folder))
                        paths.Add(path);
                }
                paths.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (var path in paths)
                    yield return new PaletteState.Item(Kind.Asset, guid: AssetDatabase.AssetPathToGUID(path));
                yield break;
            }

            var pending = new Stack<Transform>();
            if (context.Kind == Kind.SceneRoot)
            {
                for (var i = SceneManager.sceneCount - 1; i >= 0; --i)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
                    var roots = scene.GetRootGameObjects();
                    for (var j = roots.Length - 1; j >= 0; --j) pending.Push(roots[j].transform);
                }
            }
            else if (context.Kind == Kind.GameObject)
            {
                var parent = context.SceneObject.transform;
                for (var i = parent.childCount - 1; i >= 0; --i) pending.Push(parent.GetChild(i));
            }
            while (pending.Count > 0)
            {
                var t = pending.Pop();
                if (!t) continue;
                yield return new PaletteState.Item(Kind.GameObject, t.gameObject);
                if (recursive)
                    for (var i = t.childCount - 1; i >= 0; --i) pending.Push(t.GetChild(i));
            }
        }
    }
}
