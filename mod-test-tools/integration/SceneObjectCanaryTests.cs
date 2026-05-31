using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class SceneObjectCanaryTests
    {
        [Fact]
        public async Task BootedSceneHasBroadUnityAndMajdataObjectInvariants()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                BridgeResponse response = await run.Client.EvalIsolatedAsync(@"new Func<object>(() => {
    Func<Type, int> countActiveComponents = type => {
        if (type == null) {
            return 0;
        }

        return UnityEngine.Resources.FindObjectsOfTypeAll(type)
            .OfType<UnityEngine.Component>()
            .Count(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);
    };

    Type eventSystemType = Type.GetType(""UnityEngine.EventSystems.EventSystem, UnityEngine.UI"", false);
    Type gameManagerType = Type.GetType(""MajdataPlay.GameManager, Assembly-CSharp"", false);
    Type titleManagerType = Type.GetType(""MajdataPlay.Scenes.Title.TitleManager, Assembly-CSharp"", false);
    Type listManagerType = Type.GetType(""MajdataPlay.Scenes.List.ListManager, Assembly-CSharp"", false);
    Type majComponentType = Type.GetType(""MajdataPlay.MajComponent, Assembly-CSharp"", false);

    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    var rootObjects = activeScene.GetRootGameObjects();
    int activeCameraCount = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Camera))
        .OfType<UnityEngine.Camera>()
        .Count(camera => camera != null && camera.isActiveAndEnabled);
    int activeAudioListenerCount = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(UnityEngine.AudioListener))
        .OfType<UnityEngine.AudioListener>()
        .Count(listener => listener != null && listener.isActiveAndEnabled);
    int activeCanvasCount = countActiveComponents(typeof(UnityEngine.Canvas));
    int activeEventSystemCount = countActiveComponents(eventSystemType);
    int activeGameManagerCount = countActiveComponents(gameManagerType);
    int activeTitleManagerCount = countActiveComponents(titleManagerType);
    int activeListManagerCount = countActiveComponents(listManagerType);
    int activeMajComponentCount = countActiveComponents(majComponentType);

    return new {
        ActiveSceneName = activeScene.name,
        ActiveRootObjectCount = rootObjects.Count(root => root != null && root.activeInHierarchy),
        ActiveCameraCount = activeCameraCount,
        MainCameraPresent = UnityEngine.Camera.main != null,
        ActiveAudioListenerCount = activeAudioListenerCount,
        ActiveCanvasCount = activeCanvasCount,
        ActiveEventSystemCount = activeEventSystemCount,
        ActiveGameManagerCount = activeGameManagerCount,
        ActiveTitleManagerCount = activeTitleManagerCount,
        ActiveListManagerCount = activeListManagerCount,
        ActiveMajComponentCount = activeMajComponentCount
    };
})()");

                JsonElement properties = IntegrationAssertions.EvalResultProperties(response);
                string sceneName = properties.GetProperty("ActiveSceneName").GetString();
                int activeRootObjectCount = properties.GetProperty("ActiveRootObjectCount").GetInt32();
                int activeCameraCount = properties.GetProperty("ActiveCameraCount").GetInt32();
                int activeAudioListenerCount = properties.GetProperty("ActiveAudioListenerCount").GetInt32();
                int activeCanvasCount = properties.GetProperty("ActiveCanvasCount").GetInt32();
                int activeEventSystemCount = properties.GetProperty("ActiveEventSystemCount").GetInt32();
                int activeGameManagerCount = properties.GetProperty("ActiveGameManagerCount").GetInt32();
                int activeTitleManagerCount = properties.GetProperty("ActiveTitleManagerCount").GetInt32();
                int activeListManagerCount = properties.GetProperty("ActiveListManagerCount").GetInt32();
                int activeMajComponentCount = properties.GetProperty("ActiveMajComponentCount").GetInt32();

                Assert.False(string.IsNullOrWhiteSpace(sceneName));
                Assert.True(activeRootObjectCount > 0, "Active scene should have active root objects.");
                Assert.True(activeCameraCount > 0, "Booted scene should have at least one active camera.");
                Assert.True(activeAudioListenerCount > 0, "Booted scene should have at least one active audio listener.");
                Assert.True(activeCanvasCount > 0 || activeEventSystemCount > 0, "Booted idle scene should have active UI canvas or event-system objects.");
                Assert.True(activeMajComponentCount > 0, "Booted scene should have active MajdataPlay components.");
                Assert.True(
                    activeGameManagerCount > 0 || activeTitleManagerCount > 0 || activeListManagerCount > 0,
                    "Booted scene should expose at least one stable MajdataPlay root/controller component.");
            }
            catch (Exception ex)
            {
                scenarioFailure = ex;
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("scene-objects");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "scene-objects")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }
    }
}
