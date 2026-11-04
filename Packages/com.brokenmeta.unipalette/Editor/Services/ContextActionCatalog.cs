using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Shared live validation and descriptor lookup; derived catalogs only populate an in-memory menu.
    internal abstract class ContextActionCatalog<TContext> where TContext : ContextItem
    {
        private readonly UnityContextSelection _selection;

        internal abstract string GroupTitle { get; }

        protected ContextActionCatalog(UnityContextSelection selection) =>
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));

        private static bool IsEnabled(DropdownMenuAction action) =>
            action.status != DropdownMenuAction.Status.None
            && (action.status & (DropdownMenuAction.Status.Disabled | DropdownMenuAction.Status.Hidden)) == 0;

        private DropdownMenu Collect(TContext context)
        {
            var menu = new DropdownMenu();
            Populate(menu, context);
            return menu;
        }

        protected abstract void Populate(DropdownMenu menu, TContext context);

        internal string Validate(ContextItem context)
        {
            if (!(context is TContext) || !context.IsValid)
                return "Confirmed context no longer exists or belongs to another action source.";

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

            foreach (var entry in Collect((TContext)item.Context).MenuItems())
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
            TContext context,
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

                foreach (var entry in Collect(context).MenuItems())
                {
                    if (!(entry is DropdownMenuAction action))
                        continue;

                    action.UpdateActionStatus(null);
                    snapshot.Add(new ContextActionItem(context, action.name, IsEnabled(action), GroupTitle));
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
