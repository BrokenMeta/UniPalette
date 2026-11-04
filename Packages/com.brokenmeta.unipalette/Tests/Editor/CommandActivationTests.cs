using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("ExecutionPreparation")]
    [Category("Fast")]
    public class CommandActivationTests : CommandTestFixture
    {
        [Test]
        public void BrowserForwardsMenuLeafWithoutDecidingNativeEligibility()
        {
            var context = Assets();
            var command = new MenuCommandItem("Create Empty", "GameObject/Create Empty");
            var category = new MenuCategoryItem("Commands", "", new PaletteItem[] { command });
            using var session = PaletteTestData.CreateSession();
            var browser = new CommandBrowser(context, category);
            var requests = 0;
            session.ExecuteCommand += (requestedCommand, requestedContext) =>
            {
                requests++;
                Assert.That(requestedCommand, Is.SameAs(command));
                Assert.That(requestedContext, Is.SameAs(context));
            };

            Assert.That(browser.Activate(session, command), Is.False);
            Assert.That(requests, Is.EqualTo(1), "The executor owns relevance and preparation checks");
            Assert.That(session.Message, Is.Empty);
            Assert.That(browser.Activate(session, category), Is.True);
            var emptyCategory = new MenuCategoryItem("Empty", "Assets/Empty", Array.Empty<PaletteItem>());
            Assert.That(browser.Activate(session, emptyCategory), Is.False);
            Assert.That(requests, Is.EqualTo(1), "Categories must never be dispatched");
        }

        [Test]
        public void LeafWithoutExecutorKeepsConfirmedSelectionAndDoesNotDispatch()
        {
            using var session = PaletteTestData.CreateSession(new[] { Assets() }, Build());
            session.Select(0); session.OpenContextCommands();
            var before = Selection.objects;
            session.SetQuery("Assets/Open"); session.Select(0);
            Assert.That(session.BrowseSelected(), Is.False);
            Assert.That(session.Message, Is.Empty);
            Assert.That(session.OpenContextCommands(), Is.False);
            Assert.That(session.ActivateSelected(), Is.False);
            Assert.That(session.Message, Does.Contain("Native execution is unavailable").And.Contain("nothing executed"));
            Assert.That(Selection.objects, Is.EqualTo(before));
        }
    }
}
