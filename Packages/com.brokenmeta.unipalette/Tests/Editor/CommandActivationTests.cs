using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BrokenMeta.UniPalette.Editor;
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
            using var state = new PaletteState(new[] { Assets() }, Build());
            state.Select(0); state.OpenContextCommands();
            var before = Selection.objects;
            state.SetQuery("Assets/Open"); state.Select(0);
            Assert.That(state.BrowseSelected(), Is.False);
            Assert.That(state.Message, Is.Empty);
            Assert.That(state.OpenContextCommands(), Is.False);
            Assert.That(state.ActivateSelected(), Is.False);
            Assert.That(state.Message, Does.Contain("Native execution is unavailable").And.Contain("nothing executed"));
            Assert.That(Selection.objects, Is.EqualTo(before));
        }
    }
}
