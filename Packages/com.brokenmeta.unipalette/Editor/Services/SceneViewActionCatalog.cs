using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEditor.Actions;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Public Scene View actions, not a copy of Hierarchy/Project's right-click menu.
    // DropdownMenu is only an in-memory collection. This source never displays a native menu.
    internal sealed class SceneViewActionCatalog
    {
        private readonly UnityContextSelection _selection;

        internal SceneViewActionCatalog(UnityContextSelection selection) =>
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

        internal string Validate(SceneObjectContext context)
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

        internal bool TryResolve(ContextActionItem item, out DropdownMenuAction result, out string error)
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

    }
}
