using System;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Synchronous discovery. The group is presentation data, not an execution route.
    internal sealed class SceneViewContextActions : ICommandSource
    {
        private readonly Signal _changed = new();

        private readonly SceneViewActionCatalog _catalog;

        private bool _disposed;

        public bool IsLoading => false;

        public string Error { get; private set; } = "";

        internal SceneViewContextActions(SceneViewActionCatalog catalog) =>
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        public IDisposable SubscribeChanged(Action callback) =>
            _changed.Subscribe(callback);

        public PaletteItem ForContext(PaletteItem context)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SceneViewContextActions));

            Error = "";
            PaletteItem[] children = Array.Empty<PaletteItem>();

            if (context is SceneObjectContext sceneObject)
            {
                if (_catalog.TryGetItems(sceneObject, out var items, out var error))
                {
                    var actions = items
                        .Cast<PaletteItem>()
                        .ToArray();
                    children = new PaletteItem[] { new ContextActionCategoryItem(_catalog.GroupTitle, actions), };
                }
                else
                {
                    Error = error;
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
