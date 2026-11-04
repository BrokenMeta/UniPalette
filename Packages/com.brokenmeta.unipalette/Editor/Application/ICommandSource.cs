using System;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Editor.Application
{
    // The creator owns disposal. Consumers unsubscribe when they no longer need updates.
    // ForContext may start loading and synchronously notify subscribers; confirm context before calling it.
    internal interface ICommandSource : IDisposable
    {
        bool IsLoading { get; }

        string Error { get; }
        IDisposable SubscribeChanged(Action callback);

        PaletteItem ForContext(PaletteItem context);
    }
}