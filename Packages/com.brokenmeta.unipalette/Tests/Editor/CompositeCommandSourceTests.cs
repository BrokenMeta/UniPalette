using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("CommandSources")]
    [Category("Fast")]
    public class CompositeCommandSourceTests
    {
        private static PaletteItem Root(PaletteItem item) =>
            new MenuCategoryItem("Commands", "", new[] { item, });

        [Test]
        public void CompositePreservesSourceOrderAndIndependentState()
        {
            var firstItem = new FixtureItem("Same name", "Same path");
            var secondItem = new FixtureItem("Same name", "Same path");
            var first = new TestCommandSource(Root(firstItem));
            var second = new TestCommandSource(Root(secondItem), true);
            using ICommandSource source = new PaletteCommands(first, second);
            var context = new AssetsRootContext();
            Assert.That(source.ForContext(context).Children, Is.EqualTo(new[] { firstItem, secondItem, }));
            Assert.That(source.IsLoading, Is.True);
            first.Complete(Root(firstItem), "First source error");
            second.Complete(Root(secondItem), "Second source error");
            Assert.That(source.IsLoading, Is.False);
            Assert.That(source.Error, Does.Contain("First source error").And.Contain("Second source error"));
            Assert.That(first.Requests, Is.EqualTo(1));
            Assert.That(second.Requests, Is.EqualTo(1));
        }

        [Test]
        public void CompositeForwardsEverySourceAndOwnsTheirLifetime()
        {
            var first = new TestCommandSource(Root(new FixtureItem("First", "First")));
            var second = new TestCommandSource(Root(new FixtureItem("Second", "Second")));
            var source = new PaletteCommands(first, second);
            var notifications = 0;
            var subscription = source.SubscribeChanged(() => notifications++);
            first.Complete(Root(new FixtureItem("First update", "First update")));
            second.Complete(Root(new FixtureItem("Second update", "Second update")));
            Assert.That(notifications, Is.EqualTo(2));
            subscription.Dispose();
            second.Complete(Root(new FixtureItem("Another update", "Another update")));
            Assert.That(notifications, Is.EqualTo(2));
            source.Dispose();
            source.Dispose();
            Assert.That(first.Subscribers, Is.Zero);
            Assert.That(second.Subscribers, Is.Zero);
            Assert.That(first.DisposeCalls, Is.EqualTo(1));
            Assert.That(second.DisposeCalls, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => source.SubscribeChanged(() => { }));
            Assert.Throws<ObjectDisposedException>(() => source.ForContext(new AssetsRootContext()));
        }
    }
}
