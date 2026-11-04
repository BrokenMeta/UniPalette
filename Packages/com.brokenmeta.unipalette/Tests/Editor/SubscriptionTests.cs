using System;
using System.Collections.Generic;
using BrokenMeta.UniPalette.Editor.Lifetime;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("Fast")]
    [Category("Subscriptions")]
    public class SubscriptionTests
    {
        [Test]
        public void SubscriptionUnsubscribesOnceEvenWhenDisposedReentrantly()
        {
            var calls = 0;
            Subscription subscription = null;
            subscription = new Subscription(() =>
            {
                calls++;
                subscription.Dispose();
            });

            subscription.Dispose();
            subscription.Dispose();

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void FailedRegistrationReleasesItsPartialSubscription()
        {
            var attached = false;

            Assert.Throws<InvalidOperationException>(() => Subscription.Create(
                () =>
                {
                    attached = true;
                    throw new InvalidOperationException();
                },
                () => attached = false
            ));

            Assert.That(attached, Is.False);
        }

        [Test]
        public void ScopeReleasesAllSubscriptionsDespiteAnUnsubscribeFailure()
        {
            var scope = new SubscriptionScope();
            var calls = new List<string>();
            var first = new Subscription(() => calls.Add("first"));
            Assert.That(first.AddTo(scope), Is.SameAs(first));
            new Subscription(() =>
            {
                scope.Dispose();
                throw new InvalidOperationException("Expected unsubscribe failure");
            }).AddTo(scope);
            new Subscription(() => calls.Add("last")).AddTo(scope);

            var failure = Assert.Throws<AggregateException>(scope.Dispose);
            scope.Dispose();
            new Subscription(() => calls.Add("late")).AddTo(scope);

            Assert.That(failure.InnerExceptions.Count, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(new[] { "last", "first", "late" }));
        }

        [Test]
        public void DuplicateCallbacksHaveIndependentSubscriptionsAndKeepRegistrationOrder()
        {
            using var signal = new Signal();
            var calls = new List<string>();
            Action shared = () => calls.Add("shared");
            using var first = signal.Subscribe(shared);
            using var middle = signal.Subscribe(() => calls.Add("middle"));
            using var last = signal.Subscribe(shared);

            first.Dispose();
            first.Dispose();
            signal.Publish();

            Assert.That(calls, Is.EqualTo(new[] { "middle", "shared" }));
            Assert.That(signal.SubscriberCount, Is.EqualTo(2));
        }

        [Test]
        public void DisposedListenersAreSkippedAndNewListenersWaitForTheNextNotification()
        {
            using var signal = new Signal<int>();
            var calls = new List<int>();
            IDisposable later = null;
            IDisposable added = null;
            using var first = signal.Subscribe(value =>
            {
                calls.Add(value);
                later.Dispose();
                added ??= signal.Subscribe(next => calls.Add(next * 10));
            });
            later = signal.Subscribe(_ => Assert.Fail("Disposed listener must not run"));

            signal.Publish(1);
            Assert.That(calls, Is.EqualTo(new[] { 1 }));
            signal.Publish(2);
            Assert.That(calls, Is.EqualTo(new[] { 1, 2, 20 }));

            added.Dispose();
            later.Dispose();
        }

        [Test]
        public void DisposingPublisherDuringNotificationStopsRemainingListeners()
        {
            using var signal = new Signal<int>();
            var calls = 0;
            using var first = signal.Subscribe(_ =>
            {
                calls++;
                signal.Dispose();
            });
            using var second = signal.Subscribe(_ => Assert.Fail("Publisher was disposed"));

            signal.Publish(1);
            signal.Publish(2);
            signal.Dispose();

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(signal.SubscriberCount, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => signal.Subscribe(_ => { }));
        }
    }
}
