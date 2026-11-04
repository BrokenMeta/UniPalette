using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Services
{
    internal sealed class UnityContextSelection : IContextSelection, IContextVisitor<EditorContext>
    {
        public bool TryConfirm(ContextItem context, out string error) =>
            Bind(context)
                .TryConfirm(out error);

        public bool MatchesSelection(PaletteItem context) =>
            context is ContextItem item
            && Bind(item)
                .MatchesSelection();

        public EditorContext Visit(SceneObjectContext context) =>
            new SceneObjectBinding(context);

        public EditorContext Visit(AssetContext context) =>
            new AssetBinding(context);

        public EditorContext Visit(SceneRootContext context) =>
            new SceneRootBinding(context);

        public EditorContext Visit(AssetsRootContext context) =>
            new AssetsRootBinding(context);

        internal EditorContext Bind(ContextItem context) =>
            context.Accept(this);

        private sealed class SceneObjectBinding : EditorContext
        {
            internal SceneObjectBinding(SceneObjectContext context)
                : base(context) { }

            internal override bool UsesHierarchy => true;
        }

        private sealed class AssetBinding : EditorContext
        {
            private readonly AssetContext _asset;

            internal AssetBinding(AssetContext context)
                : base(context) =>
                _asset = context;

            internal override bool UsesHierarchy => false;

            internal override bool MatchesSelection()
            {
                if (!base.MatchesSelection())
                    return false;

                var guids = Selection.assetGUIDs;

                return guids.Length == 1 && guids[0] == _asset.AssetGuid;
            }
        }

        private sealed class SceneRootBinding : EditorContext
        {
            internal SceneRootBinding(SceneRootContext context)
                : base(context) { }

            internal override bool UsesHierarchy => true;

            private static string SceneError()
            {
                var active = SceneManager.GetActiveScene();

                return !active.IsValid()
                    || !active.isLoaded
                    || EditorSceneManager.IsPreviewScene(active)
                    || !StageUtility
                        .GetCurrentStageHandle()
                        .Equals(StageUtility.GetMainStageHandle())
                        ? "Scene Root requires a loaded active scene in the main stage."
                        : "";
            }

            internal override bool MatchesSelection() =>
                Selection.count == 0;

            internal override Scene CaptureScene() =>
                SceneManager.GetActiveScene();

            internal override bool TryConfirm(out string error)
            {
                error = SceneError();

                if (error.Length != 0)
                    return false;

                if (Selection.count != 0)
                    Selection.objects = Array.Empty<Object>();

                if (MatchesSelection())
                    return true;

                error = "Unity Selection could not be prepared for the confirmed root.";

                return false;
            }

            internal override string ValidateExecution(Object target, Scene scene)
            {
                var error = SceneError();

                if (error.Length != 0)
                    return error;

                return !scene.IsValid() || !scene.isLoaded || SceneManager.GetActiveScene() != scene
                    ? "The active scene changed after preparing Scene Root."
                    : "";
            }
        }

        private sealed class AssetsRootBinding : EditorContext
        {
            internal AssetsRootBinding(AssetsRootContext context)
                : base(context) { }

            internal override bool UsesHierarchy => false;

            internal override Object Target => AssetDatabase.LoadMainAssetAtPath("Assets");

            internal override bool RequiresFocusedPreparation => true;

            internal override bool MatchesSelection() =>
                Selection.count == 1
                && Selection.activeObject
                && AssetDatabase.IsValidFolder("Assets")
                && Selection.activeObject == Target;

            internal override bool TryConfirm(out string error)
            {
                error = "";
                var root = Target;

                if (!root || !AssetDatabase.IsValidFolder("Assets"))
                {
                    error = "Cannot resolve the Assets root folder.";

                    return false;
                }

                if (Selection.count != 1 || Selection.activeObject != root)
                    Selection.activeObject = root;

                if (MatchesSelection())
                    return true;

                error = "Unity Selection could not be prepared for the confirmed root.";

                return false;
            }

            internal override string ValidateExecution(Object target, Scene scene) =>
                !target
                || !AssetDatabase.IsValidFolder("Assets")
                || Target != target
                    ? "The confirmed Assets root no longer exists."
                    : "";

            internal override bool PrepareFocusedWindow() =>
                AssetDatabase.OpenAsset(Target);
        }
    }
}