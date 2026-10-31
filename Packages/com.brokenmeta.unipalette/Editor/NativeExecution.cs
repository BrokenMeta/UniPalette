using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrokenMeta.UniPalette.Editor
{
    // Discovery supplies commands; Unity owns applicability and the actual result.
    internal sealed class NativeExecution
    {
        private readonly PaletteState.Item context;
        private readonly UnityEngine.Object target;
        private readonly string path;
        private readonly Scene scene;
        private bool consumed;

        private NativeExecution(PaletteState.Item context, string path)
        {
            this.context = context;
            this.path = path;
            target = context.Kind == UnityContexts.Kind.AssetsRoot ? AssetDatabase.LoadMainAssetAtPath("Assets") : context.Target;
            if (context.Kind == UnityContexts.Kind.SceneRoot) scene = SceneManager.GetActiveScene();
        }

        // Only explicit confirmation changes Selection. Preflight must never silently adopt a new selection.
        internal static bool TryConfirmRoot(PaletteState.Item context, out string error)
        {
            error = "";
            if (context.Kind == UnityContexts.Kind.SceneRoot)
            {
                error = SceneRootError();
                if (error.Length != 0) return false;
                if (Selection.count != 0) Selection.objects = Array.Empty<UnityEngine.Object>();
            }
            else if (context.Kind == UnityContexts.Kind.AssetsRoot)
            {
                var root = AssetDatabase.LoadMainAssetAtPath("Assets");
                if (!root || !AssetDatabase.IsValidFolder("Assets"))
                { error = "Cannot resolve the Assets root folder."; return false; }
                if (Selection.count != 1 || Selection.activeObject != root) Selection.activeObject = root;
            }
            else { error = "Not a virtual root context."; return false; }
            if (MenuCommands.MatchesSelection(context)) return true;
            error = "Unity Selection could not be prepared for the confirmed root.";
            return false;
        }

        private static string SceneRootError()
        {
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded || EditorSceneManager.IsPreviewScene(active)
                || !StageUtility.GetCurrentStageHandle().Equals(StageUtility.GetMainStageHandle()))
                return "Scene Root requires a loaded active scene in the main stage.";
            return "";
        }

        internal static bool Supports(string path, PaletteState.Item context)
        {
            if (context == null || string.IsNullOrEmpty(path)) return false;
            if (context.Kind != UnityContexts.Kind.GameObject && context.Kind != UnityContexts.Kind.Asset
                && context.Kind != UnityContexts.Kind.SceneRoot && context.Kind != UnityContexts.Kind.AssetsRoot) return false;
            return MenuCommands.InScope(path, context);
        }

        internal static bool TryPrepare(PaletteState.Item command, PaletteState.Item context,
            out NativeExecution execution, out string error)
        {
            execution = null;
            if (command == null || command.Kind != UnityContexts.Kind.Menu || command.CanBrowse
                || !Supports(command.Path, context))
            { error = "Command requires a confirmed context and a relevant menu leaf; nothing executed."; return false; }
            var candidate = new NativeExecution(context, command.Path);
            error = candidate.Validate();
            if (error.Length != 0) return false;
            execution = candidate;
            return true;
        }

        private string Validate()
        {
            if (!Supports(path, context)) return "Command is outside the confirmed context's menu families.";
            if (context.Kind == UnityContexts.Kind.SceneRoot)
            {
                var error = SceneRootError();
                if (error.Length != 0) return error;
                if (!scene.IsValid() || !scene.isLoaded || SceneManager.GetActiveScene() != scene)
                    return "The active scene changed after preparing Scene Root.";
            }
            else if (context.Kind == UnityContexts.Kind.AssetsRoot)
            {
                if (!target || !AssetDatabase.IsValidFolder("Assets") || AssetDatabase.LoadMainAssetAtPath("Assets") != target)
                    return "The confirmed Assets root no longer exists.";
            }
            else if (!context.IsValid || !target || context.Target != target) return "Confirmed target no longer exists.";
            if (!MenuCommands.MatchesSelection(context)) return "Unity Selection differs from the confirmed target.";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return "Unity Editor is not ready for native execution.";
            try { return Menu.GetEnabled(path) ? "" : "Unity menu command is disabled: " + path; }
            catch (Exception exception) { return "Cannot read Unity menu state: " + exception.Message; }
        }

        // Runs synchronously after Close. No queued callback survives the palette's lifecycle.
        internal void ExecuteAfterClose()
        {
            if (consumed) return;
            consumed = true;
            try
            {
                var error = Validate();
                if (error.Length != 0) { Report(error); return; }
                // These are the standard recipients for object/asset context. Commands needing
                // a specific Scene View, Inspector or other UI state may still have no effect.
                var hierarchy = context.Kind == UnityContexts.Kind.GameObject || context.Kind == UnityContexts.Kind.SceneRoot;
                if (hierarchy)
                {
                    if (!EditorApplication.ExecuteMenuItem("Window/General/Hierarchy"))
                    { Report("Cannot focus Hierarchy."); return; }
                }
                else EditorUtility.FocusProjectWindow();

                if (context.Kind == UnityContexts.Kind.AssetsRoot)
                {
                    // Never repair an external Selection change made while focusing Project.
                    error = Validate();
                    if (error.Length != 0) { Report(error); return; }
                    // The public folder-open route prepares the visible directory too.
                    if (!AssetDatabase.OpenAsset(target))
                    { Report("Cannot open the Assets root in Project."); return; }
                }

                var window = EditorWindow.focusedWindow;
                // Read the actual focused window's runtime identity, without resolving or invoking internal APIs.
                var expected = hierarchy ? "Unity.Hierarchy.Editor.HierarchyWindow" : "UnityEditor.ProjectBrowser";
                if (!window || window.GetType().FullName != expected || window.rootVisualElement.panel == null)
                { Report("Target editor window is not focused and ready: " + expected); return; }
                error = Validate();
                if (error.Length != 0) { Report(error); return; }
                if (EditorWindow.focusedWindow != window)
                { Report("Target editor window lost focus before execution."); return; }
                // A true return only means Unity accepted dispatch, not that a result was produced.
                if (!EditorApplication.ExecuteMenuItem(path)) Report("Unity rejected native dispatch.");
            }
            catch (Exception exception) { Report(exception.Message); }
        }

        private void Report(string error) => Debug.LogWarning("UniPalette: could not dispatch " + path
            + " after closing the palette. " + error + " No retry was attempted; inspect Unity state before retrying.");
    }
}
