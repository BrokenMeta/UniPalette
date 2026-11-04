using System;

namespace BrokenMeta.UniPalette.Editor.Lifetime
{
    internal sealed class Signal : IDisposable
    {
        private readonly Signal<ValueTuple> _signal = new();

        internal int SubscriberCount => _signal.SubscriberCount;

        internal IDisposable Subscribe(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return _signal.Subscribe(_ => callback());
        }

        internal void Publish() =>
            _signal.Publish(default);

        public void Dispose() =>
            _signal.Dispose();
    }
}
