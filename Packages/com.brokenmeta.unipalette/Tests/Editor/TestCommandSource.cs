using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Tests
{
    // Fixed data by default; completion is controlled explicitly for loading/lifetime regressions.
    internal sealed class TestCommandSource : ICommandSource
    {
        internal Action<PaletteItem> _requesting;
        private PaletteItem _root;

        internal TestCommandSource(PaletteItem root, bool loading = false)
        {
            _root = root;
            IsLoading = loading;
        }

        internal int Requests { get; private set; }

        internal int DisposeCalls { get; private set; }

        internal int Subscribers => Changed?.GetInvocationList().Length ?? 0;

        public event Action Changed;

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
            Changed = null;
        }

        internal void Complete(PaletteItem next, string error = "")
        {
            _root = next;
            Error = error;
            IsLoading = false;
            Changed?.Invoke();
        }
    }
}