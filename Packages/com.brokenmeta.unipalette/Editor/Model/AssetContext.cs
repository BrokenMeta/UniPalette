using System;
using BrokenMeta.UniPalette.Editor.Services;
using UnityEditor;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class AssetContext : ContextItem
    {
        internal AssetContext(string guid, bool selectionEntry = false)
            : base(selectionEntry) =>
            AssetGuid = guid;

        internal string AssetGuid { get; }
        internal string AssetPath => AssetDatabase.GUIDToAssetPath(AssetGuid ?? "");

        internal override Object Target => AssetDatabase.LoadMainAssetAtPath(AssetPath);
        internal override string Title => System.IO.Path.GetFileName(AssetPath);
        internal override string Path => AssetPath;

        internal override bool IsValid => UnityContexts.IsProjectPath(AssetPath)
            && AssetDatabase.AssetPathToGUID(AssetPath, AssetPathToGUIDOptions.OnlyExistingAssets)
            == AssetGuid;

        internal override bool CanBrowse => AssetDatabase.IsValidFolder(AssetPath);

        internal override T Accept<T>(IContextVisitor<T> visitor) =>
            visitor.Visit(this);

        internal override bool SameIdentity(PaletteItem other) =>
            other is AssetContext context
            && SelectionEntry == context.SelectionEntry
            && AssetGuid == context.AssetGuid;

        internal override bool ContainsDescendant(PaletteItem other) =>
            !SelectionEntry
            && CanBrowse
            && other is AssetContext { SelectionEntry: false, } context
            && context.AssetPath.StartsWith(
                AssetPath
                + "/",
                StringComparison.Ordinal
            );

        internal override bool IsMenuRelevant(string path) =>
            path == "Edit/Duplicate" || path.StartsWith("Assets/", StringComparison.Ordinal);
    }
}