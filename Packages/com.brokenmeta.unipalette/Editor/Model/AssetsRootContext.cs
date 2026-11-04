using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class AssetsRootContext : ContextItem
    {
        internal override string Title => "Project";
        internal override string Path => "Assets";
        internal override string ContextTitle => Path;
        internal override bool CanBrowse => true;
        internal override bool UsesHierarchy => false;
        internal override Object ExecutionTarget => AssetDatabase.LoadMainAssetAtPath("Assets");
        internal override bool SameIdentity(PaletteItem other) => other is AssetsRootContext;
        internal override IEnumerable<PaletteItem> GetChildren(bool recursive) => UnityContexts.AssetChildren("Assets", recursive);
        internal override bool IsMenuRelevant(string path) => path.StartsWith("Assets/", StringComparison.Ordinal);
        internal override bool MatchesSelection() => Selection.count == 1 && Selection.activeObject
            && AssetDatabase.IsValidFolder("Assets") && Selection.activeObject == ExecutionTarget;
        internal override bool TryConfirm(out string error)
        {
            error = "";
            var root = ExecutionTarget;
            if (!root || !AssetDatabase.IsValidFolder("Assets"))
            { error = "Cannot resolve the Assets root folder."; return false; }
            if (Selection.count != 1 || Selection.activeObject != root) Selection.activeObject = root;
            if (MatchesSelection()) return true;
            error = "Unity Selection could not be prepared for the confirmed root.";
            return false;
        }
        internal override string ValidateExecution(Object target, Scene scene) =>
            !target || !AssetDatabase.IsValidFolder("Assets") || ExecutionTarget != target
                ? "The confirmed Assets root no longer exists." : "";
        internal override bool RequiresFocusedPreparation => true;
        internal override bool PrepareFocusedWindow() => AssetDatabase.OpenAsset(ExecutionTarget);
    }
}
