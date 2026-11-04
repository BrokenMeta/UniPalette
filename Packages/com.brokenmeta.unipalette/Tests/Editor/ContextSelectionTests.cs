using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("Fast")]
    [Category("ContextConfirmation")]
    public class ContextSelectionTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void SessionConfirmsThroughDependencyBeforeRequestingCommands(bool succeeds)
        {
            var context = new AssetsRootContext();
            var selection = new TestSelection(succeeds);
            using var source = new TestCommandSource(PaletteTestData.Commands());

            using var session = new PaletteSession(
                source,
                selection,
                new TestContextSource(
                    new PaletteItem[] { context, }
                )
            );

            var browser = session.Browser;

            source._requesting = requested =>
            {
                Assert.That(selection._confirmed, Is.SameAs(requested));
                Assert.That(session.ConfirmingContext, Is.True);
            };

            Assert.That(session.OpenCommands(context), Is.EqualTo(succeeds));
            Assert.That(selection._confirmed, Is.SameAs(context));
            Assert.That(source.Requests, Is.EqualTo(succeeds ? 1 : 0));
            Assert.That(session.ConfirmingContext, Is.False);
            Assert.That(session.CanGoBack, Is.EqualTo(succeeds));

            if (!succeeds)
            {
                Assert.That(session.Browser, Is.SameAs(browser));
                Assert.That(session.Message, Is.EqualTo("Confirmation failed"));
            }
        }

        private sealed class TestSelection : IContextSelection
        {
            private readonly bool _succeeds;

            internal ContextItem _confirmed;

            internal TestSelection(bool succeeds) => _succeeds = succeeds;

            public bool TryConfirm(ContextItem context, out string error)
            {
                _confirmed = context;
                error = _succeeds ? "" : "Confirmation failed";

                return _succeeds;
            }

            public bool MatchesSelection(PaletteItem context) => _succeeds && ReferenceEquals(context, _confirmed);
        }
    }
}