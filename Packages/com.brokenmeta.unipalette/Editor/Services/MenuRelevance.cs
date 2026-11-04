using System;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Family relevance only; Unity menu state and execution readiness are checked separately.
    internal sealed class MenuRelevance : IContextVisitor<bool>
    {
        private readonly string _path;

        internal MenuRelevance(string path) =>
            _path = path;

        internal bool Matches(PaletteItem context) =>
            !string.IsNullOrEmpty(_path)
            && context is ContextItem nativeContext
            && nativeContext.Accept(this);

        public bool Visit(SceneObjectContext context) =>
            _path == "Edit/Duplicate"
            || _path.StartsWith("GameObject/", StringComparison.Ordinal)
            || _path.StartsWith("Component/", StringComparison.Ordinal);

        public bool Visit(AssetContext context) =>
            _path == "Edit/Duplicate" || _path.StartsWith("Assets/", StringComparison.Ordinal);

        public bool Visit(SceneRootContext context) =>
            _path.StartsWith("GameObject/", StringComparison.Ordinal);

        public bool Visit(AssetsRootContext context) =>
            _path.StartsWith("Assets/", StringComparison.Ordinal);
    }
}
