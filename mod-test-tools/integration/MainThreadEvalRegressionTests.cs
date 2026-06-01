using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class MainThreadEvalRegressionTests
    {
        [Fact]
        public async Task EvalCanReadUnitySceneStateOnMainThreadAndGameContinuesAdvancing()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));

                JsonElement first = await ReadUnityMainThreadStateAsync(run.Client);
                Assert.True(first.GetProperty("IsPlaying").GetBoolean(), "Unity application should be playing during main-thread eval probe.");
                Assert.True(first.GetProperty("ActiveSceneLoaded").GetBoolean(), "Active Unity scene should be loaded when read from eval.");
                Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("ActiveSceneName").GetString()));
                Assert.True(first.GetProperty("RootCount").GetInt32() > 0, "Active Unity scene should expose root GameObjects from eval.");
                Assert.True(first.GetProperty("CameraCount").GetInt32() > 0, "Main-thread eval should be able to inspect Unity camera objects.");

                await Task.Delay(1500);
                JsonElement second = await ReadUnityMainThreadStateAsync(run.Client);
                Assert.True(second.GetProperty("ActiveSceneLoaded").GetBoolean(), "Active Unity scene should still be loaded after the main-thread eval probe.");
                Assert.False(string.IsNullOrWhiteSpace(second.GetProperty("ActiveSceneName").GetString()));
                Assert.True(
                    second.GetProperty("FrameCount").GetInt32() > first.GetProperty("FrameCount").GetInt32(),
                    "Unity frame count should continue advancing after the main-thread eval probe.");
                Assert.True(
                    second.GetProperty("RealtimeSinceStartup").GetDouble() > first.GetProperty("RealtimeSinceStartup").GetDouble(),
                    "Unity realtime should continue advancing after the main-thread eval probe.");
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("main-thread-eval");
            }
        }

        private static async Task<JsonElement> ReadUnityMainThreadStateAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    return new {
        ActiveSceneName = activeScene.name,
        ActiveSceneLoaded = activeScene.isLoaded,
        RootCount = activeScene.GetRootGameObjects().Length,
        CameraCount = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Camera))
            .OfType<UnityEngine.Camera>()
            .Count(camera => camera != null && camera.gameObject != null && camera.gameObject.activeInHierarchy),
        FrameCount = UnityEngine.Time.frameCount,
        RealtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup,
        IsPlaying = UnityEngine.Application.isPlaying
    };
})()");

            return IntegrationAssertions.EvalResultProperties(response);
        }
    }
}
