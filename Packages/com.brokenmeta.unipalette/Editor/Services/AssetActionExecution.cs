using System;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // One prepared contextual handoff. Discovery and UI grouping belong to the source.
    internal sealed class AssetActionExecution
    {
        private readonly AssetActionCatalog _catalog;

        private readonly ContextActionItem _item;

        private readonly Object _target;

        private bool _consumed;

        private AssetActionExecution(AssetActionCatalog catalog, ContextActionItem item)
        {
            _catalog = catalog;
            _item = item;
            _target = item.Context.Target;
        }

        private string Validate()
        {
            var error = _catalog.Validate(_item.Context);
            return error.Length != 0 ? error : _item.Context.Target != _target
                ? "Confirmed asset target changed after preparation."
                : "";
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
                var error = Validate();

                if (error.Length != 0)
                {
                    Report(error);
                    return;
                }

                EditorUtility.FocusProjectWindow();
                var window = EditorWindow.focusedWindow;

                if (!window || window.GetType().FullName != "UnityEditor.ProjectBrowser"
                    || window.rootVisualElement.panel == null)
                {
                    Report("Project is not focused and ready.");
                    return;
                }

                // Rebuild after focus: the displayed flags and handlers may be stale.
                if (!_catalog.TryResolve(_item, out var action, out error))
                {
                    Report(error);
                    return;
                }

                error = Validate();

                if (error.Length != 0)
                {
                    Report(error);
                    return;
                }

                if (EditorWindow.focusedWindow != window)
                {
                    Report("Project lost focus before dispatch.");
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
            AssetActionCatalog catalog,
            ContextActionItem item,
            out AssetActionExecution execution,
            out string error)
        {
            execution = null;

            if (item == null)
            {
                error = "Contextual action is unavailable.";
                return false;
            }

            try
            {
                if (!catalog.TryResolve(item, out _, out error))
                    return false;

                var candidate = new AssetActionExecution(catalog, item);
                error = candidate.Validate();

                if (error.Length != 0)
                    return false;

                execution = candidate;
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
