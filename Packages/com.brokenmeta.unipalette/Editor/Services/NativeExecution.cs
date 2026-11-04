using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Discovery supplies commands; Unity owns applicability and the actual result.
    internal sealed class NativeExecution
    {
        private readonly ContextItem context;
        private readonly EditorContext editorContext;
        private readonly UnityEngine.Object target;
        private readonly string path;
        private readonly Scene scene;
        private bool consumed;

        private NativeExecution(ContextItem context, string path, UnityContextSelection contextSelection)
        {
            this.context = context;
            this.path = path;
            editorContext = contextSelection.Bind(context);
            target = editorContext.Target;
            scene = editorContext.CaptureScene();
        }

        internal static bool Supports(string path, PaletteItem context) =>
            !string.IsNullOrEmpty(path) && context is ContextItem && context.IsMenuRelevant(path);

        internal static bool TryPrepare(UnityContextSelection contextSelection, PaletteItem command, PaletteItem context,
            out NativeExecution execution, out string error)
        {
            execution = null;
            if (command == null || !(command is MenuCommandItem) || command.CanBrowse
                || !Supports(command.Path, context))
            {
                error = (command == null ? "" : command.Path + " · ")
                    + "Command requires a confirmed context and a relevant menu leaf; nothing executed.";
                return false;
            }
            var candidate = new NativeExecution((ContextItem)context, command.Path, contextSelection);
            error = candidate.Validate();
            if (error.Length != 0) return false;
            execution = candidate;
            return true;
        }

        private string Validate()
        {
            if (!Supports(path, context)) return "Command is outside the confirmed context's menu families.";
            var contextError = editorContext.ValidateExecution(target, scene);
            if (contextError.Length != 0) return contextError;
            if (!editorContext.MatchesSelection()) return "Unity Selection differs from the confirmed target.";
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
                var hierarchy = editorContext.UsesHierarchy;
                if (hierarchy)
                {
                    if (!EditorApplication.ExecuteMenuItem("Window/General/Hierarchy"))
                    { Report("Cannot focus Hierarchy."); return; }
                }
                else EditorUtility.FocusProjectWindow();

                if (editorContext.RequiresFocusedPreparation)
                {
                    // Opening a root folder must never repair an external Selection change.
                    error = Validate();
                    if (error.Length != 0) { Report(error); return; }
                    if (!editorContext.PrepareFocusedWindow())
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
