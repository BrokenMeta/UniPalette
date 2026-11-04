using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Lifetime;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Tests
{
    // Fixed data by default; completion is controlled explicitly for loading/lifetime regressions.
    internal sealed class TestCommandSource : ICommandSource
    {
        private readonly Signal _changed = new();

        internal Action<PaletteItem> _requesting;
        private PaletteItem _root;

        internal TestCommandSource(PaletteItem root, bool loading = false)
        {
            _root = root;
            IsLoading = loading;
        }

        internal int Requests { get; private set; }

        internal int DisposeCalls { get; private set; }

        internal int Subscribers => _changed.SubscriberCount;

        public IDisposable SubscribeChanged(Action callback) =>
            _changed.Subscribe(callback);

        public bool IsLoading { get; private set; }

        public string Error { get; private set; } = "";

        public PaletteItem ForContext(PaletteItem context)
        {
            Requests++;
            _requesting?.Invoke(context);

            return _root;
        }

        public void Dispose()
        {
            DisposeCalls++;
            _requesting = null;
            _changed.Dispose();
        }

        internal void Complete(PaletteItem next, string error = "")
        {
            _root = next;
            Error = error;
            IsLoading = false;
            _changed.Publish();
        }
    }
}