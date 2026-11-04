using System;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Tests
{
    // Synthetic trees belong to tests, not the Editor's context model.
    internal sealed class FixtureItem : PaletteItem
    {
        internal FixtureItem(string title, string path, bool selectionEntry = false, params PaletteItem[] children)
        {
            Title = title;
            Path = path;
            SelectionEntry = selectionEntry;
            Children = children;
        }

        internal override string Title { get; }

        internal override string Path { get; }

        internal override bool SelectionEntry { get; }

        internal override PaletteItem[] Children { get; }

        internal override string ContextTitle => Path.Substring(Path.LastIndexOf('/') + 1);

        internal override string IconName => Path == "Scene Root"
            ? "SceneAsset Icon"
            : Path.StartsWith(
                "Scene Root/",
                StringComparison.Ordinal
            )
                ? "GameObject Icon"
                : "Folder Icon";

        internal override bool ContainsDescendant(PaletteItem other)
        {
            if (!(other is FixtureItem) || SelectionEntry || other.SelectionEntry)
                return false;

            foreach (var child in Children)
            {
                if (child.SameIdentity(other) || child.ContainsDescendant(other))
                    return true;
            }

            return false;
        }

        internal override bool IsMenuRelevant(string path) =>
            Path == "Scene Root"
                ? path.StartsWith(
                    "GameObject/",
                    StringComparison.Ordinal
                )
                : Path.StartsWith(
                    "Scene Root/",
                    StringComparison.Ordinal
                )
                    ? path.StartsWith(
                        "GameObject/",
                        StringComparison.Ordinal
                    )
                    || path.StartsWith(
                        "Component/",
                        StringComparison.Ordinal
                    )
                    : path.StartsWith(
                        "Assets/",
                        StringComparison.Ordinal
                    );
    }
}