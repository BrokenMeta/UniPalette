using NUnit.Framework;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

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
            using var session = new PaletteSession(source, selection, new PaletteItem[] { context });
            var browser = session.Browser;
            source.Requesting = requested =>
            {
                Assert.That(selection.Confirmed, Is.SameAs(requested));
                Assert.That(session.ConfirmingContext, Is.True);
            };

            Assert.That(session.OpenCommands(context), Is.EqualTo(succeeds));
            Assert.That(selection.Confirmed, Is.SameAs(context));
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
            private readonly bool succeeds;
            internal ContextItem Confirmed;
            internal TestSelection(bool succeeds) { this.succeeds = succeeds; }
            public bool TryConfirm(ContextItem context, out string error)
            {
                Confirmed = context;
                error = succeeds ? "" : "Confirmation failed";
                return succeeds;
            }
            public bool MatchesSelection(PaletteItem context) => succeeds && ReferenceEquals(context, Confirmed);
        }
    }
}
