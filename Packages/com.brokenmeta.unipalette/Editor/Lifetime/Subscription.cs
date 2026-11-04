using System;

namespace BrokenMeta.UniPalette.Editor.Lifetime
{
    internal sealed class Subscription : IDisposable
    {
        private Action _unsubscribe;

        internal Subscription(Action unsubscribe) =>
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));

        internal static IDisposable Create(Action subscribe, Action unsubscribe)
        {
            if (subscribe == null)
                throw new ArgumentNullException(nameof(subscribe));

            var subscription = new Subscription(unsubscribe);

            try
            {
                subscribe();
                return subscription;
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            var unsubscribe = _unsubscribe;
            _unsubscribe = null;
            unsubscribe?.Invoke();
        }
    }
}
