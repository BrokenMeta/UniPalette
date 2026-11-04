using NUnit.Framework;

namespace BrokenMeta.UniPalette.Tests
{
    [SetUpFixture]
    public sealed class TestSceneSession
    {
        [OneTimeSetUp]
        public void Begin()
        {
            SharedTestScene.Current = new SharedTestScene();
        }

        [OneTimeTearDown]
        public void End()
        {
            SharedTestScene.Current?.Dispose();
        }
    }
}