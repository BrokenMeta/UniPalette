using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BrokenMeta.UniPalette.Tests
{
    [Category("ExecutionPreparation")]
    [Category("Fast")]
    public class CommandActivationTests : CommandTestFixture
    {

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
