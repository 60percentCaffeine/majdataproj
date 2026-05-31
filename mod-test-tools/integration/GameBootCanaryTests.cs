using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class GameBootCanaryTests
    {
        [Fact]
        public async Task GameBootReachesStableRunningUnityState()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                BootStatePair stableState = await WaitForStableBootStateAsync(run.Client, TimeSpan.FromSeconds(30));

                Assert.False(string.IsNullOrWhiteSpace(stableState.Second.ActiveSceneName));
                Assert.True(stableState.Second.ActiveSceneLoaded, "Active scene should be loaded.");
                Assert.True(stableState.Second.FrameCount > stableState.First.FrameCount, "Frame count should advance between stable boot samples.");
                Assert.Equal(stableState.First.ActiveSceneName, stableState.Second.ActiveSceneName);
                Assert.True(stableState.Second.TimeScaleCanWrite, "UnityEngine.Time.timeScale should be present and writable.");
                Assert.True(stableState.Second.RealtimeSinceStartup > stableState.First.RealtimeSinceStartup);
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

                harness.CollectLogs("boot-stable-state");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "boot-stable-state")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }

        private static async Task<BootStatePair> WaitForStableBootStateAsync(TestClient client, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            BootState previous = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                BootState current = await ReadBootStateAsync(client);
                if (previous != null && IsStableRunningState(previous, current))
                {
                    return new BootStatePair(previous, current);
                }

                previous = current;
                await Task.Delay(1000);
            }

            throw new TimeoutException("Timed out waiting for a stable running Unity boot state.");
        }

        private static async Task<BootState> ReadBootStateAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new {
    ActiveSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
    ActiveSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle,
    ActiveSceneLoaded = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded,
    FrameCount = UnityEngine.Time.frameCount,
    TimeScaleCanWrite = typeof(UnityEngine.Time).GetProperty(""timeScale"", BindingFlags.Public | BindingFlags.Static) != null && typeof(UnityEngine.Time).GetProperty(""timeScale"", BindingFlags.Public | BindingFlags.Static).CanWrite,
    RealtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup,
    IsPlaying = UnityEngine.Application.isPlaying
}");
            JsonElement properties = IntegrationAssertions.EvalResultProperties(response);
            return new BootState
            {
                ActiveSceneName = properties.GetProperty("ActiveSceneName").GetString(),
                ActiveSceneHandle = properties.GetProperty("ActiveSceneHandle").GetInt32(),
                ActiveSceneLoaded = properties.GetProperty("ActiveSceneLoaded").GetBoolean(),
                FrameCount = properties.GetProperty("FrameCount").GetInt32(),
                TimeScaleCanWrite = properties.GetProperty("TimeScaleCanWrite").GetBoolean(),
                RealtimeSinceStartup = properties.GetProperty("RealtimeSinceStartup").GetDouble(),
                IsPlaying = properties.GetProperty("IsPlaying").GetBoolean()
            };
        }

        private static bool IsStableRunningState(BootState first, BootState second)
        {
            return first.ActiveSceneLoaded
                && second.ActiveSceneLoaded
                && first.IsPlaying
                && second.IsPlaying
                && !string.IsNullOrWhiteSpace(first.ActiveSceneName)
                && first.ActiveSceneName == second.ActiveSceneName
                && second.FrameCount > first.FrameCount
                && second.TimeScaleCanWrite
                && second.RealtimeSinceStartup > first.RealtimeSinceStartup;
        }

        private sealed class BootStatePair
        {
            public BootStatePair(BootState first, BootState second)
            {
                First = first;
                Second = second;
            }

            public BootState First { get; }
            public BootState Second { get; }
        }

        private sealed class BootState
        {
            public string ActiveSceneName { get; set; }
            public int ActiveSceneHandle { get; set; }
            public bool ActiveSceneLoaded { get; set; }
            public int FrameCount { get; set; }
            public bool TimeScaleCanWrite { get; set; }
            public double RealtimeSinceStartup { get; set; }
            public bool IsPlaying { get; set; }
        }
    }
}
