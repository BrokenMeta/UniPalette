using System;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEngine;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // One prepared contextual handoff. Discovery and UI grouping belong to the source.
    internal sealed class SceneViewActionExecution
    {
        private readonly SceneViewActionCatalog _catalog;

        private readonly ContextActionItem _item;

        private readonly SceneView _window;

        private bool _consumed;

        private SceneViewActionExecution(SceneViewActionCatalog catalog, ContextActionItem item, SceneView window)
        {
            _catalog = catalog;
            _item = item;
            _window = window;
        }

        private void Report(string error) =>
            Debug.LogWarning($"UniPalette contextual action: {_item.Name}. {error} No retry was attempted.");

        internal void ExecuteAfterClose()
        {
            if (_consumed)
                return;

            _consumed = true;

            try
            {
                var error = _catalog.Validate(_item.Context);

                if (error.Length != 0 || !_window)
                {
                    Report(error.Length != 0 ? error : "The chosen Scene View no longer exists.");
                    return;
                }

                _window.Focus();

                if (EditorWindow.focusedWindow != _window || _window.rootVisualElement.panel == null)
                {
                    Report("The chosen Scene View is not focused and ready.");
                    return;
                }

                // Rebuild after focus: the displayed flags and handlers may be stale.
                if (!_catalog.TryResolve(_item, out var action, out error))
                {
                    Report(error);
                    return;
                }

                if (EditorWindow.focusedWindow != _window)
                {
                    Report("The chosen Scene View lost focus before dispatch.");
                    return;
                }

                // Unity may queue work internally. This is one handoff, not a result guarantee.
                action.Execute();
            }
            catch (Exception exception)
            {
                Report(exception.Message);
            }
        }

        internal static bool TryPrepare(
            SceneViewActionCatalog catalog,
            ContextActionItem item,
            SceneView window,
            out SceneViewActionExecution execution,
            out string error)
        {
            execution = null;

            if (item == null)
            {
                error = "Contextual action is unavailable.";
                return false;
            }

            if (!window)
            {
                error = "The chosen Scene View is unavailable. Open a Scene View and reopen UniPalette.";
                return false;
            }

            try
            {
                if (!catalog.TryResolve(item, out _, out error))
                    return false;

                execution = new SceneViewActionExecution(catalog, item, window);
                return true;
            }
            catch (Exception exception)
            {
                error = "Cannot prepare contextual action: " + exception.Message;
                return false;
            }
        }
    }
}
