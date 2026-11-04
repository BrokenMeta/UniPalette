using System;
using System.Collections.Generic;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEditor.Search;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // A menu snapshot for this palette instance. Query edits only search the resulting local tree.
    internal sealed class MenuCommands : ICommandSource
    {
        private readonly Signal _changed = new();

        private readonly IContextSelection _contextSelection;
        private readonly List<string> _paths = new();
        private bool _disposed;
        private SearchContext _request;
        private bool _started;

        internal MenuCommands(IContextSelection contextSelection) =>
            _contextSelection =
                contextSelection ?? throw new ArgumentNullException(nameof(contextSelection));

        internal IReadOnlyList<string> Paths => _paths;

        public IDisposable SubscribeChanged(Action callback) =>
            _changed.Subscribe(callback);

        public bool IsLoading { get; private set; }
        public string Error { get; private set; } = "";

        public PaletteItem ForContext(PaletteItem context)
        {
            if (!_started)
                Start();

            return Build(
                _paths,
                context,
                _contextSelection.MatchesSelection(context),
                path => Menu.GetEnabled(path)
            );
        }

        public void Dispose()
        {
            _disposed = true;
            _changed.Dispose();
            _request?.Dispose();
            _request = null;
            IsLoading = false;
        }

        private static bool IsValidMenuPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || path != path.Trim()
                || path.Any(char.IsControl)
                || path.Contains('\\'))
                return false;

            var segments = path.Split('/');

            return segments.Length > 1 && segments.All(IsValidSegment);
        }

        private static bool IsValidSegment(string segment)
        {
            return !string.IsNullOrWhiteSpace(segment)
                && segment != "."
                && segment != ".."
                && segment.Any(c => c != '-');
        }

        private static bool? ReadAvailability(string path, Func<string, bool?> getEnabled)
        {
            try
            {
                return getEnabled(path);
            }
            catch
            {
                return null;
            }
        }

        private static string DescribeAvailability(bool? enabled, bool selectionMatches) =>
            enabled == null
                ? "Availability unverified: menu state unavailable."
                : !selectionMatches
                    ? "Availability unverified: palette context differs from Unity Selection."
                    : "Menu enabled for current Unity Selection; execution applicability is unverified.";

        private static void AddPath(Node root, string path, string note)
        {
            var node = root;
            var prefix = "";

            foreach (var segment in path.Split('/'))
            {
                prefix = prefix.Length == 0 ? segment : prefix + "/" + segment;

                if (!node._children.TryGetValue(segment, out var child))
                    node._children.Add(segment, child = new Node(segment, prefix));

                node = child;
            }

            node._note = note;
        }

        internal void Start()
        {
            if (_started || _disposed)
                return;

            _started = true;

            if (!SearchService.Providers.Any(provider => provider.id == "menu"))
            {
                Error = "Unity menu search provider is unavailable.";
                _changed.Publish();

                return;
            }

            IsLoading = true;

            try
            {
                _request = SearchService.CreateContext(new[] { "menu", }, "", SearchFlags.None);

                SearchService.Request(
                    _request,
                    (context, items) =>
                    {
                        if (_disposed)
                            return;

                        _paths.AddRange(
                            Normalize(
                                items
                                    .Where(item => item?.provider?.id == "menu")
                                    .Select(item => item.id)
                            )
                        );

                        IsLoading = false;
                        var completed = _request;
                        _request = null;
                        completed?.Dispose();
                        _changed.Publish();
                    }
                );
            }
            catch (Exception exception)
            {
                Error = "Menu discovery failed: " + exception.Message;
                IsLoading = false;
                _request?.Dispose();
                _request = null;
                _changed.Publish();
            }
        }

        internal static string[] Normalize(IEnumerable<string> source)
        {
            return (source ?? Array.Empty<string>())
                .Where(IsValidMenuPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal
                )
                .ToArray();
        }

        internal static bool InScope(string path, PaletteItem context) =>
            new MenuRelevance(path).Matches(context);

        internal static PaletteItem Build(
            IEnumerable<string> source,
            PaletteItem context,
            bool selectionMatches,
            Func<string, bool?> getEnabled)
        {
            var root = new Node("Commands", "");

            foreach (var path in Normalize(source))
            {
                if (!InScope(path, context))
                    continue;

                var enabled = ReadAvailability(path, getEnabled);

                // A false flag is evidence only for the actual global menu/Selection snapshot.
                if (selectionMatches && enabled == false)
                    continue;

                AddPath(root, path, DescribeAvailability(enabled, selectionMatches));
            }

            return root.Freeze();
        }

        private sealed class Node
        {
            internal readonly SortedDictionary<string, Node> _children = new(StringComparer.Ordinal);

            internal readonly string _path;
            internal readonly string _title;

            internal string _note;

            internal Node(string title, string path)
            {
                _title = title;
                _path = path;
            }

            internal PaletteItem Freeze()
            {
                return _children.Count > 0
                    ? new MenuCategoryItem(
                        _title,
                        _path,
                        _children
                            .Values
                            .Select(child => child.Freeze())
                            .ToArray(),
                        _note
                        ?? "Menu category"
                    )
                    : new MenuCommandItem(
                        _title,
                        _path,
                        _note
                        ?? "Menu category"
                    );
            }
        }
    }
}