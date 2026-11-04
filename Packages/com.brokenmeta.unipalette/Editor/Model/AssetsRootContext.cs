using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Editor.Model
{
    internal sealed class AssetsRootContext : ContextItem
    {
        internal override string Title => "Project";
        internal override string Path => "Assets";
        internal override string ContextTitle => Path;
        internal override bool CanBrowse => true;

        internal override T Accept<T>(IContextVisitor<T> visitor) =>
            visitor.Visit(this);

        internal override bool SameIdentity(PaletteItem other) =>
            other is AssetsRootContext;

        internal override IEnumerable<PaletteItem> GetChildren(bool recursive) =>
            UnityContexts.AssetChildren("Assets", recursive);

        internal override bool IsMenuRelevant(string path) =>
            path.StartsWith("Assets/", StringComparison.Ordinal);
    }
}