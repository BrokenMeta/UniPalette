using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // A live Editor binding. NativeExecution captures its target/scene, then revalidates them.
    internal abstract class EditorContext
    {
        protected readonly ContextItem Context;

        protected EditorContext(ContextItem context) => Context = context;

        internal virtual Object Target => Context.Target;
        internal abstract bool UsesHierarchy { get; }
        internal virtual bool RequiresFocusedPreparation => false;
        internal virtual Scene CaptureScene() => default;

        internal virtual string ValidateExecution(Object target, Scene scene) => !Context.IsValid
            || !target
            || Context.Target != target
                ? "Confirmed target no longer exists."
                : "";

        internal virtual bool PrepareFocusedWindow() => true;

        internal virtual bool TryConfirm(out string error)
        {
            error = "";
            var target = Target;

            if (!target)
            {
                error = "Context no longer exists.";

                return false;
            }

            if (Selection.count != 1 || Selection.activeObject != target)
                Selection.activeObject = target;

            return true;
        }

        internal virtual bool MatchesSelection() => Selection.objects.Length == 1
            && Target
            && Selection.activeObject == Target;
    }
}