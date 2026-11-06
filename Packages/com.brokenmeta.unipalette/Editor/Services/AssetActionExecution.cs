using System;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Services
{
    internal sealed class AssetActionExecution : ContextActionExecution<AssetContext>
    {
        private readonly Object _target;

        protected override string WindowName => "Project";

        private AssetActionExecution(AssetActionCatalog catalog, ContextActionItem item)
            : base(catalog, item) =>
            _target = item.Context.Target;

        protected override string ValidateBeforeFocus()
        {
            var error = base.ValidateBeforeFocus();
            return error.Length != 0 ? error : Context.Target != _target
                ? "Confirmed asset target changed after preparation."
                : "";
        }

        protected override bool TryFocusWindow(out EditorWindow window, out string error)
        {
            EditorUtility.FocusProjectWindow();
            window = EditorWindow.focusedWindow;

            var ready = window
                && window.GetType().FullName == "UnityEditor.ProjectBrowser"
                && window.rootVisualElement.panel != null;
            error = ready ? "" : "Project is not focused and ready.";
            return ready;
        }

        protected override string ValidateBeforeDispatch() =>
            ValidateBeforeFocus();

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
                error = candidate.ValidateBeforeFocus();

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
