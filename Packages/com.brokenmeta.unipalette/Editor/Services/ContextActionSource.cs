using System;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Synchronous discovery. The group is presentation data, not an execution route.
    internal sealed class ContextActionSource<TContext> : ICommandSource where TContext : ContextItem
    {
        private readonly Signal _changed = new();

        private readonly ContextActionCatalog<TContext> _catalog;

        private readonly bool _includeEmptyGroup;

        private bool _disposed;

        public bool IsLoading => false;

        public string Error { get; private set; } = "";

        internal ContextActionSource(ContextActionCatalog<TContext> catalog, bool includeEmptyGroup)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _includeEmptyGroup = includeEmptyGroup;
        }

        public IDisposable SubscribeChanged(Action callback) =>
            _changed.Subscribe(callback);

        public PaletteItem ForContext(PaletteItem context)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ContextActionSource<TContext>));

            Error = "";
            var children = Array.Empty<PaletteItem>();

            if (context is TContext target)
            {
                if (!_catalog.TryGetItems(target, out var items, out var error))
                {
                    Error = error;
                }
                else if (_includeEmptyGroup || items.Count > 0)
                {
                    var group = new ContextActionCategoryItem(_catalog.GroupTitle, items.ToArray<PaletteItem>());
                    children = new PaletteItem[] { group, };
                }
            }

            return new MenuCategoryItem("Commands", "", children);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _changed.Dispose();
        }
    }
}