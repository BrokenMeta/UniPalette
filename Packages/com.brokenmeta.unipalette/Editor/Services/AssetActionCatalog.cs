using BrokenMeta.UniPalette.Editor.Model;
using UnityEditor.Actions;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Type-specific CONTEXT actions, not a complete Project right-click menu.
    internal sealed class AssetActionCatalog : ContextActionCatalog<AssetContext>
    {
        internal override string GroupTitle => "Asset Actions";

        internal AssetActionCatalog(UnityContextSelection selection)
            : base(selection) { }

        protected override void Populate(DropdownMenu menu, AssetContext context)
        {
            var target = context.Target;
            ContextMenuUtility.AddMenuItemsForType(menu, target.GetType(), new Object[] { target, }, null);
        }
    }
}
