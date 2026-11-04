using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEditor.Actions;
using UnityEngine;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Experimental source: public Scene View actions, not a copy of Hierarchy/Project's right-click menu.
    // DropdownMenu is only an in-memory collection. This source never displays a native menu.
    internal sealed class SceneViewContextActions
    {
        internal sealed class PreparedAction
        {
            private readonly SceneViewContextActions _source;

            private readonly ContextActionItem _item;

            private readonly SceneView _window;

            private bool _consumed;

            internal PreparedAction(SceneViewContextActions source, ContextActionItem item, SceneView window)
            {
                _source = source;
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
                    var error = _source.Validate(_item.Context);

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
                    if (!_source.TryResolve(_item, out var action, out error))
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
        }

        private readonly UnityContextSelection _selection;

        internal SceneViewContextActions(UnityContextSelection selection) =>
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));

        private static bool IsEnabled(DropdownMenuAction action) =>
            action.status != DropdownMenuAction.Status.None
            && (action.status & (DropdownMenuAction.Status.Disabled | DropdownMenuAction.Status.Hidden)) == 0;

        private static DropdownMenu Collect()
        {
            var menu = new DropdownMenu();
            ContextMenuUtility.AddGameObjectEntriesTo(menu);
            return menu;
        }

        private string Validate(SceneObjectContext context)
        {
            if (context == null || !context.IsValid)
                return "Confirmed scene object no longer exists.";

            if (!_selection.MatchesSelection(context))
                return "Unity Selection differs from the confirmed target.";

            return EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode
                    ? "Unity Editor is not ready for contextual execution."
                    : "";
        }

        private bool TryResolve(ContextActionItem item, out DropdownMenuAction result, out string error)
        {
            result = null;
            error = Validate(item.Context);

            if (error.Length != 0)
                return false;

            foreach (var entry in Collect().MenuItems())
            {
                if (!(entry is DropdownMenuAction action) || action.name != item.Name)
                    continue;

                if (result != null)
                {
                    error = "Contextual action name is ambiguous.";
                    return false;
                }

                result = action;
            }

            if (result == null)
            {
                error = "Contextual action is no longer present.";
                return false;
            }

            result.UpdateActionStatus(null);

            if (!IsEnabled(result))
            {
                error = "Contextual action is currently disabled or hidden.";
                return false;
            }

            error = Validate(item.Context);
            return error.Length == 0;
        }

        internal bool TryGetItems(
            SceneObjectContext context,
            out IReadOnlyList<ContextActionItem> items,
            out string error)
        {
            items = Array.Empty<ContextActionItem>();
            error = Validate(context);

            if (error.Length != 0)
                return false;

            try
            {
                var snapshot = new List<ContextActionItem>();

                foreach (var entry in Collect().MenuItems())
                {
                    if (!(entry is DropdownMenuAction action))
                        continue;

                    action.UpdateActionStatus(null);
                    snapshot.Add(new ContextActionItem(context, action.name, IsEnabled(action)));
                }

                error = Validate(context);

                if (error.Length != 0)
                    return false;

                items = snapshot.AsReadOnly();
                return true;
            }
            catch (Exception exception)
            {
                error = "Cannot collect contextual actions: " + exception.Message;
                return false;
            }
        }

        internal bool TryPrepare(
            ContextActionItem item,
            SceneView window,
            out PreparedAction execution,
            out string error)
        {
            execution = null;

            if (item == null || !window)
            {
                error = "A contextual action and an explicit Scene View are required.";
                return false;
            }

            try
            {
                if (!TryResolve(item, out _, out error))
                    return false;

                execution = new PreparedAction(this, item, window);
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
