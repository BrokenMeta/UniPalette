using System;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("CommandDiscovery")]
    [Category("Fast")]
    public class CommandSourceTests : CommandTestFixture
    {
        private static PaletteItem Empty() =>
            new MenuCategoryItem("Commands", "", Array.Empty<PaletteItem>());

        [Test]
        public void SynchronousCompletionReadsConfirmedSelectionAndPushesOneLevel()
        {
            using var source = new TestCommandSource(Empty(), true);

            using var session = new PaletteSession(
                source,
                new UnityContextSelection(),
                new TestContextSource(new[] { Assets(), })
            );

            var confirmationObserved = false;
            using var confirmation = session.SubscribeContextConfirmed(() => confirmationObserved = true);

            source._requesting = context =>
            {
                Assert.That(session.ConfirmingContext, Is.True);
                Assert.That(confirmationObserved, Is.True, "Acknowledge Selection before discovering commands");

                Assert.That(
                    new UnityContextSelection().MatchesSelection(context),
                    Is.True,
                    "Confirm before requesting a command tree"
                );

                source.Complete(Build());
            };

            session.Select(0);
            var notifications = 0;
            using var subscription = session.SubscribeChanged(change =>
            {
                Assert.That(change, Is.EqualTo(PaletteChange.Screen));
                Assert.That(session.ConfirmingContext, Is.False);
                Assert.That(session.SelectedIndex, Is.Zero);
                notifications++;
            });
            Assert.That(session.OpenContextCommands(), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(source.Requests, Is.EqualTo(1), "Synchronous notification must not recursively reload");
            Assert.That(session.Results, Is.Not.Empty);
            Assert.That(session.SelectedIndex, Is.Zero);
            subscription.Dispose();
            Assert.That(session.GoBack(), Is.True);
            Assert.That(session.CanGoBack, Is.False, "Only one transition was recorded");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DelayedCompletionUpdatesResultsAndRespectsTypedQuery(bool typeQuery)
        {
            using var source = new TestCommandSource(Empty(), true);

            using var session = new PaletteSession(
                source,
                new UnityContextSelection(),
                new TestContextSource(new[] { Assets(), })
            );

            session.Select(0);
            session.OpenContextCommands();
            Assert.That(session.CommandsLoading, Is.True);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));

            if (typeQuery)
                session.SetQuery("Create/Folder");

            var notifications = 0;
            using var subscription = session.SubscribeChanged(_ => notifications++);
            source.Complete(Build());
            Assert.That(session.CommandsLoading, Is.False);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(session.Results, Is.Not.Empty);
            Assert.That(session.SelectedIndex, Is.EqualTo(typeQuery ? -1 : 0));
            Assert.That(session.Query.Text, Is.EqualTo(typeQuery ? "Create/Folder" : ""));
            Assert.That(session.Results[0].Path, Is.EqualTo(typeQuery ? "Assets/Create/Folder" : "Assets"));
        }

        [Test]
        public void CompletionAfterBackDoesNotReplaceContextResults()
        {
            using var source = new TestCommandSource(Empty(), true);

            using var session = new PaletteSession(
                source,
                new UnityContextSelection(),
                new TestContextSource(new[] { Assets(), })
            );

            var browser = session.Browser;
            session.Select(0);
            session.OpenContextCommands();
            session.GoBack();
            session.SetQuery("Project");
            session.Select(0);
            var requests = source.Requests;
            var notifications = 0;
            using var subscription = session.SubscribeChanged(_ => notifications++);
            source.Complete(Build());
            Assert.That(session.Browser, Is.SameAs(browser));
            Assert.That(session.Query.Text, Is.EqualTo("Project"));
            Assert.That(session.SelectedIndex, Is.Zero);
            Assert.That(session.Results[0].Path, Is.EqualTo("Assets"));
            Assert.That(source.Requests, Is.EqualTo(requests));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void DisposingSessionUnsubscribesAndLeavesSourceToItsOwner()
        {
            using var source = new TestCommandSource(Empty(), true);

            using var session = new PaletteSession(
                source,
                new UnityContextSelection(),
                new TestContextSource(new[] { Assets(), })
            );

            session.Select(0);
            session.OpenContextCommands();
            Assert.That(source.Subscribers, Is.EqualTo(1));
            session.Dispose();
            session.Dispose();
            Assert.That(source.Subscribers, Is.Zero);
            Assert.That(source.DisposeCalls, Is.Zero);
            source.Complete(Build());
            Assert.That(session.Results, Is.Empty, "A borrowed source cannot update disposed state");
        }

        [Test]
        public void LoadingErrorIsExposedWithoutInventingResults()
        {
            using var source = new TestCommandSource(Empty(), true);

            using var session = new PaletteSession(
                source,
                new UnityContextSelection(),
                new TestContextSource(new[] { Assets(), })
            );

            session.Select(0);
            session.OpenContextCommands();
            source.Complete(Empty(), "Test source unavailable");
            Assert.That(session.CommandsLoading, Is.False);
            Assert.That(session.CommandNotice, Is.EqualTo("Test source unavailable"));
            Assert.That(session.Results, Is.Empty);
            Assert.That(session.SelectedIndex, Is.EqualTo(-1));
        }
    }
}