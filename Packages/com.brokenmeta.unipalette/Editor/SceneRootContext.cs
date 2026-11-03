using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor
{
    internal sealed class SceneRootContext : ContextItem
    {
        internal override string Title => "Hierarchy";
        internal override string Path => "Scene Root";
        internal override string ContextTitle => Path;
        internal override string IconName => "SceneAsset Icon";
        internal override bool CanBrowse => true;
        internal override bool UsesHierarchy => true;
        internal override bool SameIdentity(PaletteItem other) => other is SceneRootContext;
        internal override IEnumerable<PaletteItem> GetChildren(bool recursive) => UnityContexts.SceneRoots(recursive);
        internal override bool MatchesSelection() => Selection.count == 0;
        internal override bool IsMenuRelevant(string path) => path.StartsWith("GameObject/", StringComparison.Ordinal);
        internal override Scene CaptureScene() => SceneManager.GetActiveScene();

        private static string SceneError()
        {
            var active = SceneManager.GetActiveScene();
            return !active.IsValid() || !active.isLoaded || EditorSceneManager.IsPreviewScene(active)
                || !StageUtility.GetCurrentStageHandle().Equals(StageUtility.GetMainStageHandle())
                ? "Scene Root requires a loaded active scene in the main stage." : "";
        }
        internal override bool TryConfirm(out string error)
        {
            error = SceneError();
            if (error.Length != 0) return false;
            if (Selection.count != 0) Selection.objects = Array.Empty<Object>();
            if (MatchesSelection()) return true;
            error = "Unity Selection could not be prepared for the confirmed root.";
            return false;
        }
        internal override string ValidateExecution(Object target, Scene scene)
        {
            var error = SceneError();
            if (error.Length != 0) return error;
            return !scene.IsValid() || !scene.isLoaded || SceneManager.GetActiveScene() != scene
                ? "The active scene changed after preparing Scene Root." : "";
        }
    }
}
