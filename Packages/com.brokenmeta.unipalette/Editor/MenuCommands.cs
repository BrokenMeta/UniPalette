using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;

namespace BrokenMeta.UniPalette.Editor
{
    // A menu snapshot for this palette instance. Query edits only search the resulting local tree.
    internal sealed class MenuCommands : IDisposable
    {
        private SearchContext request;
        private bool disposed;
        private bool started;
        private readonly List<string> paths = new List<string>();
        internal event Action Changed;
        internal bool IsLoading { get; private set; }
        internal string Error { get; private set; } = "";
        internal IReadOnlyList<string> Paths => paths;

        internal PaletteState.Item ForContext(PaletteState.Item context)
        {
            if (!started) Start();
            return Build(paths, context, MatchesSelection(context), path => Menu.GetEnabled(path));
        }

        internal void Start()
        {
            if (started || disposed) return;
            started = true;
            if (!SearchService.Providers.Any(provider => provider.id == "menu"))
            {
                Error = "Unity menu search provider is unavailable.";
                Changed?.Invoke();
                return;
            }
            IsLoading = true;
            try
            {
                request = SearchService.CreateContext(new[] { "menu" }, "", SearchFlags.None);
                SearchService.Request(request, (SearchContext context, IList<SearchItem> items) =>
                {
                    if (disposed) return;
                    paths.AddRange(Normalize(items.Where(item => item?.provider?.id == "menu").Select(item => item.id)));
                    IsLoading = false;
                    var completed = request;
                    request = null;
                    completed?.Dispose();
                    Changed?.Invoke();
                }, SearchFlags.None);
            }
            catch (Exception exception)
            {
                Error = "Menu discovery failed: " + exception.Message;
                IsLoading = false;
                request?.Dispose();
                request = null;
                Changed?.Invoke();
            }
        }

        internal static string[] Normalize(IEnumerable<string> source)
        {
            return (source ?? Array.Empty<string>()).Where(path => !string.IsNullOrWhiteSpace(path)
                && path == path.Trim() && !path.Any(char.IsControl) && !path.Contains('\\')
                && path.Split('/').Length > 1
                && path.Split('/').All(segment => !string.IsNullOrWhiteSpace(segment) && segment != "." && segment != ".."
                    && segment.Any(c => c != '-')))
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        internal static bool MatchesSelection(PaletteState.Item context)
        {
            if (context == null) return false;
            // Virtual roots have explicit prepared states; never treat ambient Selection as their target.
            if (context.Kind == UnityContexts.Kind.SceneRoot) return Selection.count == 0;
            if (context.Kind == UnityContexts.Kind.AssetsRoot)
                return Selection.count == 1 && Selection.activeObject && AssetDatabase.IsValidFolder("Assets")
                    && Selection.activeObject == AssetDatabase.LoadMainAssetAtPath("Assets");
            if (Selection.objects.Length != 1 || !context.Target || Selection.activeObject != context.Target) return false;
            if (context.Kind != UnityContexts.Kind.Asset) return true;
            // Folders are DefaultAssets too; compare their GUID, never activeGameObject or activeTransform.
            var guids = Selection.assetGUIDs;
            return guids.Length == 1 && guids[0] == context.AssetGuid;
        }

        internal static bool InScope(string path, PaletteState.Item context)
        {
            if (context == null) return false;
            if (path == "Edit/Duplicate" && (context.Kind == UnityContexts.Kind.GameObject
                || context.Kind == UnityContexts.Kind.Asset)) return true;
            var sceneRoot = context.Kind == UnityContexts.Kind.SceneRoot || context.Path == "Scene Root";
            var gameObject = context.Kind == UnityContexts.Kind.GameObject
                || (context.Kind == UnityContexts.Kind.Fixture && context.Path.StartsWith("Scene Root/", StringComparison.Ordinal));
            // This is a menu-family relevance policy, not proof of per-object applicability.
            return sceneRoot ? path.StartsWith("GameObject/", StringComparison.Ordinal)
                : gameObject ? path.StartsWith("GameObject/", StringComparison.Ordinal) || path.StartsWith("Component/", StringComparison.Ordinal)
                : path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private sealed class Node
        {
            internal readonly string Title;
            internal readonly string Path;
            internal string Note;
            internal readonly SortedDictionary<string, Node> Children = new SortedDictionary<string, Node>(StringComparer.Ordinal);
            internal Node(string title, string path) { Title = title; Path = path; }
            internal PaletteState.Item Freeze() => new PaletteState.Item(Title, Path,
                Children.Values.Select(child => child.Freeze()).ToArray(), Note ?? "Menu category");
        }

        internal static PaletteState.Item Build(IEnumerable<string> source, PaletteState.Item context,
            bool selectionMatches, Func<string, bool?> getEnabled)
        {
            var root = new Node("Commands", "");
            foreach (var path in Normalize(source))
            {
                if (!InScope(path, context)) continue;
                bool? enabled;
                try { enabled = getEnabled(path); }
                catch { enabled = null; }
                // A false flag is evidence only for the actual global menu/Selection snapshot.
                if (selectionMatches && enabled == false) continue;
                var node = root;
                var prefix = "";
                foreach (var segment in path.Split('/'))
                {
                    prefix = prefix.Length == 0 ? segment : prefix + "/" + segment;
                    if (!node.Children.TryGetValue(segment, out var child))
                        node.Children.Add(segment, child = new Node(segment, prefix));
                    node = child;
                }
                node.Note = enabled == null ? "Availability unverified: menu state unavailable."
                    : !selectionMatches ? "Availability unverified: palette context differs from Unity Selection."
                    : "Menu enabled for current Unity Selection; execution applicability is unverified.";
            }
            return root.Freeze();
        }

        public void Dispose()
        {
            disposed = true;
            Changed = null;
            request?.Dispose();
            request = null;
            IsLoading = false;
        }
    }
}
