using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class TestHookShutdownRegressionTests
    {
        [Fact]
        public async Task ShutdownEndpointReportsFallbackPidAndStopsRuntimeProcesses()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            bool shutdownRequested = false;
            try
            {
                run = await harness.LaunchAsync(
                    TimeSpan.FromSeconds(90),
                    new TestHookLaunchOptions { ReplEnabled = true });
                int readinessPid = run.Ready.Pid;
                Assert.True(readinessPid > 0, "Readiness PID should be positive before shutdown.");
                await WaitForProcessByNameAsync("ModTestReplClient", TimeSpan.FromSeconds(15));

                BridgeResponse shutdownResponse = await run.Client.ShutdownAsync();
                shutdownRequested = true;
                Assert.Equal(200, shutdownResponse.StatusCode);
                JsonElement shutdown = JsonDocument.Parse(shutdownResponse.Content).RootElement.Clone();
                Assert.True(shutdown.GetProperty("ok").GetBoolean(), shutdownResponse.Content);
                Assert.Equal("shutdown", shutdown.GetProperty("phase").GetString());
                Assert.True(shutdown.GetProperty("accepted").GetBoolean(), shutdownResponse.Content);
                Assert.Equal(readinessPid, shutdown.GetProperty("fallbackPid").GetInt32());

                await harness.WaitForReadinessPidExitAsync(run.Ready, TimeSpan.FromSeconds(20));
                await WaitForNoProcessByNameAsync("ModTestReplClient", TimeSpan.FromSeconds(15));
                Assert.Empty(Process.GetProcessesByName("MajdataPlay"));
                Assert.Empty(Process.GetProcessesByName("ModTestReplClient"));
            }
            finally
            {
                if (run != null)
                {
                    if (!shutdownRequested)
                    {
                        await run.ShutdownOrKillAsync();
                    }

                    run.Dispose();
                }

                await harness.StopRuntimeProcessesAsync(TimeSpan.FromSeconds(5));
                harness.CollectLogs("test-hook-shutdown");
            }
        }

        private static async Task WaitForProcessByNameAsync(string processName, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                {
                    return;
                }

                await Task.Delay(250);
            }

            throw new TimeoutException("Timed out waiting for process " + processName + " to start.");
        }

        private static async Task WaitForNoProcessByNameAsync(string processName, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (Process.GetProcessesByName(processName).Length == 0)
                {
                    return;
                }

                await Task.Delay(250);
            }

            throw new TimeoutException("Timed out waiting for process " + processName + " to exit.");
        }
    }
}
