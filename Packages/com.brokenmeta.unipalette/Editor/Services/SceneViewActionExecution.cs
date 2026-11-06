using System;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;

namespace BrokenMeta.UniPalette.Editor.Services
{
    internal sealed class SceneViewActionExecution : ContextActionExecution<SceneObjectContext>
    {
        private readonly SceneView _window;

        protected override string WindowName => "The chosen Scene View";

        private SceneViewActionExecution(SceneViewActionCatalog catalog, ContextActionItem item, SceneView window)
            : base(catalog, item) =>
            _window = window;

        protected override bool TryFocusWindow(out EditorWindow window, out string error)
        {
            window = _window;

            if (!_window)
            {
                error = "The chosen Scene View no longer exists.";
                return false;
            }

            _window.Focus();

            var ready = EditorWindow.focusedWindow == _window && _window.rootVisualElement.panel != null;
            error = ready ? "" : "The chosen Scene View is not focused and ready.";
            return ready;
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
