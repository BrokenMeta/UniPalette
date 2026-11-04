using System;
using System.Collections.Generic;

namespace BrokenMeta.UniPalette.Editor.Lifetime
{
    // Synchronous Editor-main-thread notifications; a disposed listener is skipped even in a snapshot.
    internal sealed class Signal<T> : IDisposable
    {
        private sealed class Listener : IDisposable
        {
            private Action<T> _callback;

            private Action<Listener> _remove;

            internal Listener(Action<T> callback, Action<Listener> remove)
            {
                _callback = callback;
                _remove = remove;
            }

            internal void Invoke(T value) =>
                _callback?.Invoke(value);

            public void Dispose()
            {
                var remove = _remove;
                _remove = null;
                _callback = null;
                remove?.Invoke(this);
            }
        }

        private readonly List<Listener> _listeners = new();

        private bool _disposed;

        internal int SubscriberCount => _listeners.Count;

        private void Remove(Listener listener) =>
            _listeners.Remove(listener);

        internal IDisposable Subscribe(Action<T> callback)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Signal<T>));

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var listener = new Listener(callback, Remove);
            _listeners.Add(listener);
            return listener;
        }

        internal void Publish(T value)
        {
            if (_disposed)
                return;

            foreach (var listener in _listeners.ToArray())
                listener.Invoke(value);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var listener in _listeners.ToArray())
                listener.Dispose();
        }
    }
}
