using System;
using System.Collections.Generic;

namespace BrokenMeta.UniPalette.Editor.Lifetime
{
    internal sealed class SubscriptionScope : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();

        private bool _disposed;

        internal void Add(IDisposable subscription)
        {
            if (subscription == null)
                throw new ArgumentNullException(nameof(subscription));

            if (_disposed)
                subscription.Dispose();
            else
                _subscriptions.Add(subscription);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            var subscriptions = _subscriptions.ToArray();
            _subscriptions.Clear();
            List<Exception> errors = null;

            for (var index = subscriptions.Length - 1; index >= 0; index--)
            {
                try
                {
                    subscriptions[index].Dispose();
                }
                catch (Exception exception)
                {
                    errors ??= new List<Exception>();
                    errors.Add(exception);
                }
            }

            if (errors != null)
                throw new AggregateException(errors);
        }
    }
}
