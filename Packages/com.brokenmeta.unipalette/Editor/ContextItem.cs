using UnityEditor;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor
{
    internal abstract class ContextItem : PaletteItem
    {
        private readonly bool selectionEntry;
        protected ContextItem(bool selectionEntry = false) { this.selectionEntry = selectionEntry; }
        internal override bool SelectionEntry => selectionEntry;
        internal virtual Object ExecutionTarget => Target;
        internal abstract bool UsesHierarchy { get; }
        internal virtual Scene CaptureScene() => default;
        internal virtual string ValidateExecution(Object target, Scene scene) =>
            !IsValid || !target || Target != target ? "Confirmed target no longer exists." : "";
        internal virtual bool RequiresFocusedPreparation => false;
        internal virtual bool PrepareFocusedWindow() => true;

        internal virtual bool TryConfirm(out string error)
        {
            error = "";
            var target = Target;
            if (!target) { error = "Context no longer exists."; return false; }
            if (Selection.count != 1 || Selection.activeObject != target) Selection.activeObject = target;
            return true;
        }

        internal virtual bool MatchesSelection() => Selection.objects.Length == 1
            && Target && Selection.activeObject == Target;
    }
}
