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
using BrokenMeta.UniPalette.Editor.Services;

namespace BrokenMeta.UniPalette.Tests
{
    public abstract class CommandTestFixture
    {
        private protected UnityEngine.Object[] originalSelection;

        [SetUp] public void SetUp() => originalSelection = Selection.objects;

        [TearDown] public void TearDown() => Selection.objects = originalSelection;

        private protected static readonly string[] Paths = {
            "Assets/Create/Material", "Assets/Create/Folder", "Assets/CreateMore/Other", "Assets/Open",
            "GameObject/Create Empty", "GameObject/3D Object/Cube", "Component/Physics/Box Collider",
            "Window/General/Inspector"
        };

        private protected static PaletteItem Assets() => new AssetsRootContext();

        private protected static PaletteItem SceneRoot() => new SceneRootContext();

        private protected static PaletteItem Go() => new FixtureItem("Player", "Scene Root/Player");

        private protected static PaletteItem Build(string[] paths = null, PaletteItem context = null) =>
            MenuCommands.Build(paths ?? Paths, context ?? Assets(), false, _ => true);

        private protected static void Choose(PaletteSession session, string path)
        {
            var index = session.Results.FindIndex(item => item.Path == path);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), path);
            session.Select(index);
        }
    }
}
