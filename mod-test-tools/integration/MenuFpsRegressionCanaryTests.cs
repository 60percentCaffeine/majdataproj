using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class MenuFpsRegressionCanaryTests
    {
        private static readonly TimeSpan SampleWindow = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task StableTitleMenuFrameTimingStaysWithinConservativeThresholds()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                MenuStatePair stableState = await WaitForStableTitleStateAsync(run.Client, TimeSpan.FromSeconds(75));
                Assert.Equal("Title", stableState.Second.CurrentScene);
                await Task.Delay(TimeSpan.FromSeconds(2));

                await ResetFrameTimingProbeAsync(run.Client);
                await Task.Delay(SampleWindow);
                FrameTimingMetrics metrics = await StopAndReadFrameTimingProbeAsync(run.Client);

                Assert.True(metrics.SampleCount >= 120, "Menu FPS sample count is too low:\n" + metrics.Describe());
                Assert.True(metrics.EndFrame > metrics.StartFrame, "Frame count did not advance during menu FPS sampling:\n" + metrics.Describe());
                Assert.True(metrics.SamplingWindowSeconds >= 4.0, "Menu FPS sampling window was shorter than expected:\n" + metrics.Describe());
                Assert.True(metrics.AverageFps >= 30.0, "Menu average FPS regressed below the conservative 30 FPS floor:\n" + metrics.Describe());
                Assert.True(metrics.P95FrameTimeMs <= 50.0, "Menu p95 frame time regressed beyond 50 ms:\n" + metrics.Describe());
                Assert.True(metrics.DroppedFrameRatio <= 0.10, "Menu dropped-frame ratio above 10% using a 33.3 ms frame budget:\n" + metrics.Describe());
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

                harness.CollectLogs("menu-fps");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "menu-fps")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }

        private static async Task<MenuStatePair> WaitForStableTitleStateAsync(TestClient client, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            MenuState previous = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                MenuState current = await ReadMenuStateAsync(client);
                if (previous != null && IsStableTitleState(previous, current))
                {
                    return new MenuStatePair(previous, current);
                }

                previous = current;
                await Task.Delay(1000);
            }

            throw new TimeoutException("Timed out waiting for a stable Title menu state.");
        }

        private static async Task<MenuState> ReadMenuStateAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    Type sceneSwitcherType = Type.GetType(""MajdataPlay.SceneSwitcher, Assembly-CSharp"", false);
    object currentScene = sceneSwitcherType == null ? null : sceneSwitcherType.GetProperty(""CurrentScene"", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
    return new {
        ActiveSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        ActiveSceneLoaded = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded,
        CurrentScene = currentScene == null ? string.Empty : currentScene.ToString(),
        FrameCount = UnityEngine.Time.frameCount,
        RealtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup,
        IsPlaying = UnityEngine.Application.isPlaying,
        SongStorageReady = MajdataPlay.SongStorage.Collections != null && MajdataPlay.SongStorage.Collections.Length > 0 && !MajdataPlay.SongStorage.IsEmpty
    };
})()");
            JsonElement properties = IntegrationAssertions.EvalResultProperties(response);
            return new MenuState
            {
                ActiveSceneName = properties.GetProperty("ActiveSceneName").GetString(),
                ActiveSceneLoaded = properties.GetProperty("ActiveSceneLoaded").GetBoolean(),
                CurrentScene = properties.GetProperty("CurrentScene").GetString(),
                FrameCount = properties.GetProperty("FrameCount").GetInt32(),
                RealtimeSinceStartup = properties.GetProperty("RealtimeSinceStartup").GetDouble(),
                IsPlaying = properties.GetProperty("IsPlaying").GetBoolean(),
                SongStorageReady = properties.GetProperty("SongStorageReady").GetBoolean()
            };
        }

        private static bool IsStableTitleState(MenuState first, MenuState second)
        {
            return first.ActiveSceneLoaded
                && second.ActiveSceneLoaded
                && first.IsPlaying
                && second.IsPlaying
                && first.CurrentScene == "Title"
                && second.CurrentScene == "Title"
                && first.SongStorageReady
                && second.SongStorageReady
                && first.ActiveSceneName == second.ActiveSceneName
                && second.FrameCount > first.FrameCount
                && second.RealtimeSinceStartup > first.RealtimeSinceStartup;
        }

        private static async Task ResetFrameTimingProbeAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    Type probeType = Type.GetType(""TestHookMod.FrameTimingProbe, TestHookMod"", true);
    probeType.GetMethod(""Reset"", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    return new { Started = true };
})()");
            IntegrationAssertions.EvalResultProperties(response);
        }

        private static async Task<FrameTimingMetrics> StopAndReadFrameTimingProbeAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    Type probeType = Type.GetType(""TestHookMod.FrameTimingProbe, TestHookMod"", true);
    probeType.GetMethod(""Stop"", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    return probeType.GetMethod(""Read"", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
})()");
            JsonElement properties = IntegrationAssertions.EvalResultProperties(response);
            return new FrameTimingMetrics
            {
                SampleCount = properties.GetProperty("SampleCount").GetInt32(),
                StartFrame = properties.GetProperty("StartFrame").GetInt32(),
                EndFrame = properties.GetProperty("EndFrame").GetInt32(),
                SamplingWindowSeconds = properties.GetProperty("SamplingWindowSeconds").GetDouble(),
                AverageFps = properties.GetProperty("AverageFps").GetDouble(),
                P95FrameTimeMs = properties.GetProperty("P95FrameTimeMs").GetDouble(),
                MaxFrameTimeMs = properties.GetProperty("MaxFrameTimeMs").GetDouble(),
                DroppedFrameRatio = properties.GetProperty("DroppedFrameRatio").GetDouble(),
                TargetFrameRate = properties.GetProperty("TargetFrameRate").GetInt32(),
                VSyncCount = properties.GetProperty("VSyncCount").GetInt32()
            };
        }

        private sealed class MenuStatePair
        {
            public MenuStatePair(MenuState first, MenuState second)
            {
                First = first;
                Second = second;
            }

            public MenuState First { get; }
            public MenuState Second { get; }
        }

        private sealed class MenuState
        {
            public string ActiveSceneName { get; set; }
            public bool ActiveSceneLoaded { get; set; }
            public string CurrentScene { get; set; }
            public int FrameCount { get; set; }
            public double RealtimeSinceStartup { get; set; }
            public bool IsPlaying { get; set; }
            public bool SongStorageReady { get; set; }
        }

        private sealed class FrameTimingMetrics
        {
            public int SampleCount { get; set; }
            public int StartFrame { get; set; }
            public int EndFrame { get; set; }
            public double SamplingWindowSeconds { get; set; }
            public double AverageFps { get; set; }
            public double P95FrameTimeMs { get; set; }
            public double MaxFrameTimeMs { get; set; }
            public double DroppedFrameRatio { get; set; }
            public int TargetFrameRate { get; set; }
            public int VSyncCount { get; set; }

            public string Describe()
            {
                return "averageFps=" + AverageFps.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + ", p95FrameTimeMs=" + P95FrameTimeMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + ", maxFrameTimeMs=" + MaxFrameTimeMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + ", frameCount=" + SampleCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ", startFrame=" + StartFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ", endFrame=" + EndFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ", samplingWindowSeconds=" + SamplingWindowSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + ", droppedFrameRatio=" + DroppedFrameRatio.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                    + ", targetFrameRate=" + TargetFrameRate.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ", vSyncCount=" + VSyncCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
