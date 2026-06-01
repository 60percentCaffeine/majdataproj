using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class EnterExitFlowCanaryTests
    {
        [Fact]
        public async Task GameCanNavigateFromTitleToListAndBackToTitle()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                JsonElement initial = await WaitForSceneStateAsync(
                    run.Client,
                    TimeSpan.FromSeconds(75),
                    state => state.GetProperty("CurrentScene").GetString() == "Title"
                        && state.GetProperty("SongStorageReady").GetBoolean()
                        && state.GetProperty("ActiveTitleManagerCount").GetInt32() > 0);

                Assert.Equal("Title", initial.GetProperty("CurrentScene").GetString());
                Assert.Equal("Title", initial.GetProperty("ActiveSceneName").GetString());
                Assert.True(initial.GetProperty("FrameCount").GetInt32() > 0);
                Assert.True(initial.GetProperty("ActiveSceneLoaded").GetBoolean());
                Assert.True(initial.GetProperty("ActiveSceneRootCount").GetInt32() > 0);
                Assert.True(initial.GetProperty("ActiveSceneSwitcherCount").GetInt32() > 0, "SceneSwitcher should be available to drive the flow.");

                JsonElement triggerEntry = await SwitchSceneAsync(run.Client, "List", autoFadeOut: true);
                Assert.Equal("Title", triggerEntry.GetProperty("CurrentScene").GetString());
                Assert.True(triggerEntry.GetProperty("SwitchRequested").GetBoolean(), "List scene switch should be requested from Title.");

                JsonElement listState = await WaitForSceneStateAsync(
                    run.Client,
                    TimeSpan.FromSeconds(30),
                    state => state.GetProperty("CurrentScene").GetString() == "List"
                        && state.GetProperty("ActiveListManagerCount").GetInt32() > 0);

                Assert.Equal("List", listState.GetProperty("CurrentScene").GetString());
                Assert.Equal("List", listState.GetProperty("ActiveSceneName").GetString());
                Assert.True(listState.GetProperty("FrameCount").GetInt32() > initial.GetProperty("FrameCount").GetInt32());
                Assert.True(listState.GetProperty("ActiveListManagerCount").GetInt32() > 0, "ListManager should be active after entry.");
                Assert.NotEqual(initial.GetProperty("ActiveSceneHandle").GetInt32(), listState.GetProperty("ActiveSceneHandle").GetInt32());

                JsonElement triggerExit = await SwitchSceneAsync(run.Client, "Title", autoFadeOut: true);
                Assert.Equal("List", triggerExit.GetProperty("CurrentScene").GetString());
                Assert.True(triggerExit.GetProperty("SwitchRequested").GetBoolean(), "Title scene switch should be requested from List.");

                JsonElement returned = await WaitForSceneStateAsync(
                    run.Client,
                    TimeSpan.FromSeconds(30),
                    state => state.GetProperty("CurrentScene").GetString() == "Title"
                        && state.GetProperty("ActiveTitleManagerCount").GetInt32() > 0);

                Assert.Equal("Title", returned.GetProperty("CurrentScene").GetString());
                Assert.Equal("Title", returned.GetProperty("ActiveSceneName").GetString());
                Assert.True(returned.GetProperty("FrameCount").GetInt32() > listState.GetProperty("FrameCount").GetInt32());
                Assert.True(returned.GetProperty("ActiveTitleManagerCount").GetInt32() > 0, "TitleManager should be active after returning.");
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

                harness.CollectLogs("enter-exit-flow");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "enter-exit-flow")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }

        private static async Task<JsonElement> WaitForSceneStateAsync(TestClient client, TimeSpan timeout, Func<JsonElement, bool> predicate)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            JsonElement latest = default;
            while (DateTimeOffset.UtcNow < deadline)
            {
                latest = await ReadSceneStateAsync(client);
                if (predicate(latest))
                {
                    return latest;
                }

                await Task.Delay(1000);
            }

            throw new TimeoutException("Timed out waiting for scene state. Last state: " + latest);
        }

        private static async Task<JsonElement> ReadSceneStateAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(SceneStateEval);
            return IntegrationAssertions.EvalResultProperties(response);
        }

        private static async Task<JsonElement> SwitchSceneAsync(TestClient client, string sceneName, bool autoFadeOut)
        {
            string escapedSceneName = sceneName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    const System.Reflection.BindingFlags StaticFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    const System.Reflection.BindingFlags InstanceFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    int countActiveComponents(Type type) {
        if (type == null) {
            return 0;
        }

        return UnityEngine.Resources.FindObjectsOfTypeAll(type)
            .OfType<UnityEngine.Component>()
            .Count(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);
    }

    Type sceneSwitcherType = Type.GetType(""MajdataPlay.SceneSwitcher, Assembly-CSharp"", true);
    Type titleManagerType = Type.GetType(""MajdataPlay.Scenes.Title.TitleManager, Assembly-CSharp"", false);
    Type listManagerType = Type.GetType(""MajdataPlay.Scenes.List.ListManager, Assembly-CSharp"", false);
    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    object switcher = UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);

    bool switchRequested = false;
    if (switcher != null) {
        sceneSwitcherType.GetMethod(""SwitchScene"", InstanceFlags).Invoke(switcher, new object[] { """ + escapedSceneName + @""", " + (autoFadeOut ? "true" : "false") + @" });
        switchRequested = true;
    }

    return new {
        SwitchRequested = switchRequested,
        ActiveSceneName = activeScene.name,
        ActiveSceneHandle = activeScene.handle,
        ActiveSceneLoaded = activeScene.isLoaded,
        ActiveSceneRootCount = activeScene.GetRootGameObjects().Count(root => root != null && root.activeInHierarchy),
        CurrentScene = sceneSwitcherType.GetProperty(""CurrentScene"", StaticFlags).GetValue(null).ToString(),
        LastScene = sceneSwitcherType.GetProperty(""LastScene"", StaticFlags).GetValue(null).ToString(),
        FrameCount = UnityEngine.Time.frameCount,
        ActiveSceneSwitcherCount = countActiveComponents(sceneSwitcherType),
        ActiveTitleManagerCount = countActiveComponents(titleManagerType),
        ActiveListManagerCount = countActiveComponents(listManagerType),
        SongStorageReady = MajdataPlay.SongStorage.Collections != null && MajdataPlay.SongStorage.Collections.Length > 0 && !MajdataPlay.SongStorage.IsEmpty
    };
})()");

            return IntegrationAssertions.EvalResultProperties(response);
        }

        private const string SceneStateEval = @"new Func<object>(() => {
    const System.Reflection.BindingFlags StaticFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

    int countActiveComponents(Type type) {
        if (type == null) {
            return 0;
        }

        return UnityEngine.Resources.FindObjectsOfTypeAll(type)
            .OfType<UnityEngine.Component>()
            .Count(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);
    }

    Type sceneSwitcherType = Type.GetType(""MajdataPlay.SceneSwitcher, Assembly-CSharp"", true);
    Type titleManagerType = Type.GetType(""MajdataPlay.Scenes.Title.TitleManager, Assembly-CSharp"", false);
    Type listManagerType = Type.GetType(""MajdataPlay.Scenes.List.ListManager, Assembly-CSharp"", false);
    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    return new {
        ActiveSceneName = activeScene.name,
        ActiveSceneHandle = activeScene.handle,
        ActiveSceneLoaded = activeScene.isLoaded,
        ActiveSceneRootCount = activeScene.GetRootGameObjects().Count(root => root != null && root.activeInHierarchy),
        CurrentScene = sceneSwitcherType.GetProperty(""CurrentScene"", StaticFlags).GetValue(null).ToString(),
        LastScene = sceneSwitcherType.GetProperty(""LastScene"", StaticFlags).GetValue(null).ToString(),
        FrameCount = UnityEngine.Time.frameCount,
        ActiveSceneSwitcherCount = countActiveComponents(sceneSwitcherType),
        ActiveTitleManagerCount = countActiveComponents(titleManagerType),
        ActiveListManagerCount = countActiveComponents(listManagerType),
        SongStorageReady = MajdataPlay.SongStorage.Collections != null && MajdataPlay.SongStorage.Collections.Length > 0 && !MajdataPlay.SongStorage.IsEmpty
    };
})()";
    }
}
