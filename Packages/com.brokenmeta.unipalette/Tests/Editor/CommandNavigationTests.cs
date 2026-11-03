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
    [Category("Navigation")]
    [Category("Fast")]
    public class CommandNavigationTests : CommandTestFixture
    {

        [Test]
        public void EmptyQueryShowsChildrenAndRecursiveQueryStaysInsideCategory()
        {
            using var state = new PaletteState(new[] { Assets() }, Build());
            state.Select(0); state.OpenContextCommands();
            Assert.That(state.Results.Select(item => item.Path), Is.EqualTo(new[] { "Assets" }));
            Choose(state, "Assets"); state.ActivateSelected();
            Choose(state, "Assets/Create"); state.BrowseSelected();
            Assert.That(state.Results.Select(item => item.Path), Is.EqualTo(new[] { "Assets/Create/Folder", "Assets/Create/Material" }));
            state.SetQuery("Other");
            Assert.That(state.Results, Is.Empty, "CreateMore is not a descendant of Create");
            state.SetQuery("CREATE/material");
            Assert.That(state.Results.Single().Path, Is.EqualTo("Assets/Create/Material"));
            Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            state.GoBack();
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo("Assets/Create"));
        }

        [Test]
        public void RepeatedQueryAndDeepCategoryReturnHaveNoDuplicates()
        {
            var root = Build(new[] { "Assets/Create/Shapes/Cube", "Assets/Create/Shapes/Cube", "Assets/Open" });
            using var state = new PaletteState(new[] { Assets() }, root);
            state.Select(0); state.OpenContextCommands();
            state.SetQuery("Shapes");
            Choose(state, "Assets/Create/Shapes"); state.ActivateSelected();
            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Results.Single().Path, Is.EqualTo("Assets/Create/Shapes/Cube"));
            state.GoBack();
            Assert.That(state.Results[state.SelectedIndex].Path, Is.EqualTo("Assets"), "Restore visible ancestor at menu root");
            for (var i = 0; i < 3; ++i)
            {
                state.SetQuery("Cube");
                Assert.That(state.Results.Single().Path, Is.EqualTo("Assets/Create/Shapes/Cube"));
                state.SetQuery("");
                Assert.That(state.SelectedIndex, Is.EqualTo(-1));
            }
        }
    }
}
