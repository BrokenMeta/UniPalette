using System;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEngine;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // A prepared handoff can be consumed once after the palette closes.
    internal abstract class ContextActionExecution<TContext> where TContext : ContextItem
    {
        private readonly ContextActionCatalog<TContext> _catalog;

        private readonly ContextActionItem _item;

        private bool _consumed;

        protected ContextItem Context => _item.Context;

        protected abstract string WindowName { get; }

        protected ContextActionExecution(ContextActionCatalog<TContext> catalog, ContextActionItem item)
        {
            _catalog = catalog;
            _item = item;
        }

        private void Report(string error) =>
            Debug.LogWarning($"UniPalette contextual action: {_item.Name}. {error} No retry was attempted.");

        protected virtual string ValidateBeforeFocus() =>
            _catalog.Validate(Context);

        protected abstract bool TryFocusWindow(out EditorWindow window, out string error);

        // TryResolve already checks context and Selection. Asset execution also checks the captured target.
        protected virtual string ValidateBeforeDispatch() => "";

        internal void ExecuteAfterClose()
        {
            if (_consumed)
                return;

            _consumed = true;

            try
            {
                var error = ValidateBeforeFocus();

                if (error.Length != 0)
                {
                    Report(error);
                    return;
                }

                if (!TryFocusWindow(out var window, out error))
                {
                    Report(error);
                    return;
                }

                // Rebuild after focus: the displayed flags and handlers may be stale.
                if (!_catalog.TryResolve(_item, out var action, out error))
                {
                    Report(error);
                    return;
                }

                error = ValidateBeforeDispatch();

                if (error.Length != 0)
                {
                    Report(error);
                    return;
                }

                if (EditorWindow.focusedWindow != window)
                {
                    Report($"{WindowName} lost focus before dispatch.");
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
}
