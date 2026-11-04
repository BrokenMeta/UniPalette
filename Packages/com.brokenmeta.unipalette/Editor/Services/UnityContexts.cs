using System;
using System.Collections.Generic;
using System.IO;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrokenMeta.UniPalette.Editor.Services
{
    internal static class UnityContexts
    {
        private static IEnumerable<PaletteItem> Walk(Stack<Transform> pending, bool recursive)
        {
            while (pending.Count > 0)
            {
                var t = pending.Pop();

                if (!t)
                    continue;

                yield return new SceneObjectContext(t.gameObject);

                if (recursive)
                    for (var i = t.childCount - 1; i >= 0; --i)
                        pending.Push(t.GetChild(i));
            }
        }

        internal static PaletteItem[] Roots()
        {
            var roots = new List<PaletteItem>();
            var selection = CurrentSelection();

            if (selection != null)
                roots.Add(selection);

            roots.Add(new SceneRootContext());
            roots.Add(new AssetsRootContext());

            return roots.ToArray();
        }

        internal static ContextItem CurrentSelection()
        {
            if (Selection.objects.Length != 1)
                return null;

            var target = Selection.activeObject;

            if (target is GameObject go && !EditorUtility.IsPersistent(go) && IsSceneObject(go))
                return new SceneObjectContext(go, true);

            var path = AssetDatabase.GetAssetPath(target);

            return target && IsProjectPath(path) && AssetDatabase.IsMainAsset(target)
                ? new AssetContext(
                    AssetDatabase.AssetPathToGUID(path),
                    true
                )
                : null;
        }

        internal static bool IsProjectPath(string path) =>
            !string.IsNullOrEmpty(path)
            && (path == "Assets"
                || path.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal
                )
                || path.StartsWith(
                    "Packages/",
                    StringComparison.Ordinal
                ));

        internal static bool IsSceneObject(GameObject go) =>
            go
            && go.scene.IsValid()
            && go.scene.isLoaded
            && !EditorSceneManager.IsPreviewScene(go.scene);

        internal static IEnumerable<PaletteItem> AssetChildren(string folder, bool recursive)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                yield break;

            // One scoped database query, rather than a recursive query for every nested folder.
            var paths = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("", new[] { folder, }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.StartsWith(folder + "/", StringComparison.Ordinal)
                    && (recursive
                        || Path
                            .GetDirectoryName(path)
                            ?.Replace(
                                '\\',
                                '/'
                            )
                        == folder))
                    paths.Add(path);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
                yield return new AssetContext(AssetDatabase.AssetPathToGUID(path));
        }

        internal static IEnumerable<PaletteItem> SceneRoots(bool recursive)
        {
            var pending = new Stack<Transform>();

            for (var i = SceneManager.sceneCount - 1; i >= 0; --i)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                    continue;

                var roots = scene.GetRootGameObjects();

                for (var j = roots.Length - 1; j >= 0; --j)
                    pending.Push(roots[j].transform);
            }

            return Walk(pending, recursive);
        }

        internal static IEnumerable<PaletteItem> SceneChildren(Transform parent, bool recursive)
        {
            var pending = new Stack<Transform>();

            for (var i = parent.childCount - 1; i >= 0; --i)
                pending.Push(parent.GetChild(i));

            return Walk(pending, recursive);
        }
    }
}