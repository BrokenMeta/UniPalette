using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class SceneRootContext : ContextItem
    {
        internal override string Title => "Hierarchy";
        internal override string Path => "Scene Root";
        internal override string ContextTitle => Path;
        internal override string IconName => "SceneAsset Icon";
        internal override bool CanBrowse => true;

        internal override T Accept<T>(IContextVisitor<T> visitor) =>
            visitor.Visit(this);

        internal override bool SameIdentity(PaletteItem other) =>
            other is SceneRootContext;

        internal override IEnumerable<PaletteItem> GetChildren(bool recursive) =>
            UnityContexts.SceneRoots(recursive);

        internal override bool IsMenuRelevant(string path) =>
            path.StartsWith(
                "GameObject/",
                StringComparison.Ordinal
            );
    }
}