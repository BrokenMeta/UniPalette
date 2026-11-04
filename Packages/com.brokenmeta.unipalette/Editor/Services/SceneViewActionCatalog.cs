using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor.Actions;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Editor.Services
{
    internal sealed class SceneViewActionCatalog : ContextActionCatalog<SceneObjectContext>
    {
        internal override string GroupTitle => "Scene View Actions";

        internal SceneViewActionCatalog(UnityContextSelection selection)
            : base(selection) { }

        protected override void Populate(DropdownMenu menu, SceneObjectContext context) =>
            ContextMenuUtility.AddGameObjectEntriesTo(menu);
    }
}
