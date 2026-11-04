using BrokenMeta.UniPalette.Editor.Application;
using BrokenMeta.UniPalette.Editor.Model;
using BrokenMeta.UniPalette.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BrokenMeta.UniPalette.Tests
{
    public abstract class CommandTestFixture
    {
        private protected static readonly string[] s_paths =
        {
            "Assets/Create/Material",
            "Assets/Create/Folder",
            "Assets/CreateMore/Other",
            "Assets/Open",
            "GameObject/Create Empty",
            "GameObject/3D Object/Cube",
            "Component/Physics/Box Collider",
            "Window/General/Inspector",
        };

        private protected Object[] _originalSelection;

        private protected static PaletteItem Assets() =>
            new AssetsRootContext();

        private protected static PaletteItem SceneRoot() =>
            new SceneRootContext();

        private protected static PaletteItem Go() =>
            new SceneObjectContext(null);

        private protected static PaletteItem Build(string[] paths = null, PaletteItem context = null)
        {
            return MenuCommands.Build(
                paths
                ?? s_paths,
                context
                ?? Assets(),
                false,
                _ => true
            );
        }

        private protected static void Choose(PaletteSession session, string path)
        {
            var index = session.FindResultIndex(item => item.Path == path);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), path);
            session.Select(index);
        }

        [SetUp]
        public void SetUp()
        {
            _originalSelection = Selection.objects;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = _originalSelection;
        }
    }
}