using System;

namespace BrokenMeta.UniPalette.Editor.Lifetime
{
    internal static class DisposableExtensions
    {
        internal static IDisposable AddTo(this IDisposable subscription, SubscriptionScope scope)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            scope.Add(subscription);
            return subscription;
        }
    }
}
