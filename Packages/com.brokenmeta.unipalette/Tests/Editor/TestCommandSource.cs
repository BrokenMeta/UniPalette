using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Tests
{
    // Fixed data by default; completion is controlled explicitly for loading/lifetime regressions.
    internal sealed class TestCommandSource : ICommandSource
    {
        private PaletteItem root;
        public event Action Changed;
        public bool IsLoading { get; private set; }
        public string Error { get; private set; } = "";
        internal Action<PaletteItem> Requesting;
        internal int Requests { get; private set; }
        internal int DisposeCalls { get; private set; }
        internal int Subscribers => Changed?.GetInvocationList().Length ?? 0;

        internal TestCommandSource(PaletteItem root, bool loading = false)
        { this.root = root; IsLoading = loading; }

        public PaletteItem ForContext(PaletteItem context)
        {
            Requests++;
            Requesting?.Invoke(context);
            return root;
        }

        internal void Complete(PaletteItem next, string error = "")
        {
            root = next;
            Error = error;
            IsLoading = false;
            Changed?.Invoke();
        }

        public void Dispose()
        {
            DisposeCalls++;
            Requesting = null;
            Changed = null;
        }
    }
}
