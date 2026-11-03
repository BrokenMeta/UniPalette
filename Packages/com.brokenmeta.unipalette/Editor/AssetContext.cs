using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor
{
    internal sealed class AssetContext : ContextItem
    {
        internal string AssetGuid { get; }
        internal string AssetPath => AssetDatabase.GUIDToAssetPath(AssetGuid ?? "");
        internal AssetContext(string guid, bool selectionEntry = false) : base(selectionEntry) { AssetGuid = guid; }
        internal override Object Target => AssetDatabase.LoadMainAssetAtPath(AssetPath);
        internal override string Title => System.IO.Path.GetFileName(AssetPath);
        internal override string Path => AssetPath;
        internal override bool IsValid => UnityContexts.IsProjectPath(AssetPath)
            && AssetDatabase.AssetPathToGUID(AssetPath, AssetPathToGUIDOptions.OnlyExistingAssets) == AssetGuid;
        internal override bool CanBrowse => AssetDatabase.IsValidFolder(AssetPath);
        internal override bool HasBrowseChildren => CanBrowse && GetChildren(false).Any();
        internal override bool UsesHierarchy => false;
        internal override bool SameIdentity(PaletteItem other) => other is AssetContext context
            && SelectionEntry == context.SelectionEntry && AssetGuid == context.AssetGuid;
        internal override bool ContainsDescendant(PaletteItem other) => !SelectionEntry && CanBrowse
            && other is AssetContext context && !context.SelectionEntry
            && context.AssetPath.StartsWith(AssetPath + "/", StringComparison.Ordinal);
        internal override IEnumerable<PaletteItem> GetChildren(bool recursive) =>
            IsValid ? UnityContexts.AssetChildren(AssetPath, recursive) : Array.Empty<PaletteItem>();
        internal override bool MatchesSelection()
        {
            if (!base.MatchesSelection()) return false;
            var guids = Selection.assetGUIDs;
            return guids.Length == 1 && guids[0] == AssetGuid;
        }
        internal override bool IsMenuRelevant(string path) => path == "Edit/Duplicate"
            || path.StartsWith("Assets/", StringComparison.Ordinal);
    }
}
