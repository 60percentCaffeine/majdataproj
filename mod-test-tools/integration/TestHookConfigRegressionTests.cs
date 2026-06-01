using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class TestHookConfigRegressionTests
    {
        [Fact]
        public async Task HarnessCanLaunchWithTemporaryConfigFileOverrideAndRestoreIt()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            string configPath = Path.Combine(harness.GameRoot, "UserData", "TestHookMod", "config.json");
            bool configExisted = File.Exists(configPath);
            string originalConfig = configExisted ? File.ReadAllText(configPath) : null;
            HarnessRun run = null;
            try
            {
                run = await harness.LaunchAsync(
                    TimeSpan.FromSeconds(90),
                    new TestHookLaunchOptions
                    {
                        BridgeHost = "127.0.0.1",
                        BridgePort = 17444,
                        ReplEnabled = false
                    });

                AssertConfigRestored(configPath, configExisted, originalConfig);

                Assert.Equal("127.0.0.1", run.Ready.Host);
                Assert.Equal(17444, run.Ready.Port);
                JsonElement readiness = JsonDocument.Parse(File.ReadAllText(harness.ReadyPath)).RootElement.Clone();
                Assert.False(readiness.GetProperty("replEnabled").GetBoolean());

                BridgeResponse healthResponse = await run.Client.HealthAsync();
                Assert.Equal(200, healthResponse.StatusCode);
                JsonElement health = JsonDocument.Parse(healthResponse.Content).RootElement.Clone();
                Assert.True(health.GetProperty("ok").GetBoolean(), healthResponse.Content);
                Assert.Equal("127.0.0.1", health.GetProperty("host").GetString());
                Assert.Equal(17444, health.GetProperty("port").GetInt32());
                Assert.False(health.GetProperty("replEnabled").GetBoolean());
                Assert.Empty(Process.GetProcessesByName("ModTestReplClient"));
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                RestoreConfig(configPath, configExisted, originalConfig);
                harness.CollectLogs("test-hook-config");
            }
        }

        private static void AssertConfigRestored(string configPath, bool existed, string originalConfig)
        {
            if (existed)
            {
                Assert.True(File.Exists(configPath), "Existing TestHookMod config file should be restored after launch.");
                Assert.Equal(originalConfig, File.ReadAllText(configPath));
            }
            else
            {
                Assert.False(File.Exists(configPath), "Temporary TestHookMod config file should be removed after launch.");
            }
        }

        private static void RestoreConfig(string configPath, bool existed, string originalConfig)
        {
            if (existed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                File.WriteAllText(configPath, originalConfig);
            }
            else if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
    }
}
