using System;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;

namespace BrokenMeta.UniPalette.Editor.Services
{
    internal sealed class CommandExecutionPreparation
    {
        private readonly UnityContextSelection _contextSelection;

        private readonly SceneViewActionCatalog _sceneViewActions;

        private readonly AssetActionCatalog _assetActions;

        private readonly SceneView _contextSceneView;

        internal CommandExecutionPreparation(
            UnityContextSelection contextSelection,
            SceneViewActionCatalog sceneViewActions,
            AssetActionCatalog assetActions,
            SceneView contextSceneView)
        {
            _contextSelection = contextSelection ?? throw new ArgumentNullException(nameof(contextSelection));
            _sceneViewActions = sceneViewActions ?? throw new ArgumentNullException(nameof(sceneViewActions));
            _assetActions = assetActions ?? throw new ArgumentNullException(nameof(assetActions));
            _contextSceneView = contextSceneView;
        }

        internal bool TryPrepare(
            PaletteItem command,
            PaletteItem context,
            out Action executeAfterClose,
            out string error)
        {
            executeAfterClose = null;

            if (!(command is ContextActionItem action))
            {
                if (!NativeExecution.TryPrepare(_contextSelection, command, context, out var execution, out error))
                    return false;

                executeAfterClose = execution.ExecuteAfterClose;
                return true;
            }

            if (!action.Context.SameIdentity(context))
            {
                error = "Contextual action does not belong to the confirmed context.";
                return false;
            }

            if (action.Context is AssetContext)
            {
                if (!AssetActionExecution.TryPrepare(_assetActions, action, out var assetExecution, out error))
                    return false;

                executeAfterClose = assetExecution.ExecuteAfterClose;
                return true;
            }

            if (!SceneViewActionExecution.TryPrepare(
                _sceneViewActions,
                action,
                _contextSceneView,
                out var sceneExecution,
                out error
            ))
            {
                return false;
            }

            executeAfterClose = sceneExecution.ExecuteAfterClose;
            return true;
        }
    }
}
