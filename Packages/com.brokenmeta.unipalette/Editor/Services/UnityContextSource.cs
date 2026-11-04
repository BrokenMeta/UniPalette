using System;
using System.Collections.Generic;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Resolve live contexts on each request rather than caching the Editor Selection.
    internal sealed class UnityContextSource : IContextSource
    {
        private sealed class ChildrenVisitor : IContextVisitor<IEnumerable<PaletteItem>>
        {
            private readonly bool _recursive;

            internal ChildrenVisitor(bool recursive) =>
                _recursive = recursive;

            public IEnumerable<PaletteItem> Visit(SceneObjectContext context) =>
                context.IsValid
                    ? UnityContexts.SceneChildren(context.SceneObject.transform, _recursive)
                    : Array.Empty<PaletteItem>();

            public IEnumerable<PaletteItem> Visit(AssetContext context) =>
                context.IsValid
                    ? UnityContexts.AssetChildren(context.AssetPath, _recursive)
                    : Array.Empty<PaletteItem>();

            public IEnumerable<PaletteItem> Visit(SceneRootContext context) =>
                UnityContexts.SceneRoots(_recursive);

            public IEnumerable<PaletteItem> Visit(AssetsRootContext context) =>
                UnityContexts.AssetChildren("Assets", _recursive);
        }

        private sealed class BrowseChildrenVisitor : IContextVisitor<bool>
        {
            public bool Visit(SceneObjectContext context) =>
                context.IsValid && context.SceneObject.transform.childCount > 0;

            public bool Visit(AssetContext context) =>
                context.CanBrowse
                && context.IsValid
                && UnityContexts.AssetChildren(context.AssetPath, false)
                    .Any();

            // Source areas remain navigable even when empty.
            public bool Visit(SceneRootContext context) =>
                true;

            public bool Visit(AssetsRootContext context) =>
                true;
        }

        private readonly ChildrenVisitor _directChildren = new(false);

        private readonly ChildrenVisitor _descendants = new(true);

        private readonly BrowseChildrenVisitor _browseChildren = new();

        private IEnumerable<PaletteItem> TreeChildren(PaletteItem context, bool recursive)
        {
            foreach (var child in context.Children)
            {
                yield return child;

                if (!recursive)
                    continue;

                foreach (var descendant in GetItems(child, true))
                    yield return descendant;
            }
        }

        public IEnumerable<PaletteItem> GetItems(PaletteItem context, bool recursive)
        {
            if (context == null)
                return UnityContexts.Roots();

            if (context is ContextItem nativeContext)
                return nativeContext.Accept(recursive ? _descendants : _directChildren);

            return TreeChildren(context, recursive);
        }

        public bool HasBrowseChildren(PaletteItem context) =>
            context is ContextItem nativeContext
                ? nativeContext.Accept(_browseChildren)
                : context != null && context.CanBrowse;
    }
}
