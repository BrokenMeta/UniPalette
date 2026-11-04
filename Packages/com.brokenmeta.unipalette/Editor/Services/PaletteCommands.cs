using System;
using System.Linq;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Services
{
    // Owns its sources and their notification handles. Source order determines row order.
    internal sealed class PaletteCommands : ICommandSource
    {
        private readonly Signal _changed = new();

        private readonly SubscriptionScope _lifetime = new();

        private readonly ICommandSource[] _sources;

        private bool _disposed;

        public bool IsLoading => _sources.Any(source => source.IsLoading);

        public string Error => string.Join(
            Environment.NewLine,
            _sources
                .Select(source => source.Error)
                .Where(error => !string.IsNullOrEmpty(error))
                .Distinct()
        );

        internal PaletteCommands(params ICommandSource[] sources)
        {
            if (sources == null || sources.Any(source => source == null))
                throw new ArgumentException("Command sources must not be null.", nameof(sources));

            _sources = sources.ToArray();

            foreach (var source in _sources)
                source.AddTo(_lifetime);

            try
            {
                foreach (var source in _sources)
                    source.SubscribeChanged(_changed.Publish).AddTo(_lifetime);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public IDisposable SubscribeChanged(Action callback) =>
            _changed.Subscribe(callback);

        public PaletteItem ForContext(PaletteItem context)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PaletteCommands));

            var roots = _sources
                .Select(source => source.ForContext(context))
                .ToArray();
            var children = roots
                .SelectMany(root => root.Children)
                .ToArray();
            return new MenuCategoryItem("Commands", "", children);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _changed.Dispose();
            _lifetime.Dispose();
        }
    }
}
