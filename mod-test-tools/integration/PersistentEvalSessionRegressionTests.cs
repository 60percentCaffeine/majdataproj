using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class PersistentEvalSessionRegressionTests
    {
        [Fact]
        public async Task EvalSessionStateIsPersistentIsolatedAndResettable()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));

                JsonElement created = await ExpectOkAsync(
                    await run.Client.EvalAsync("int issue02Value = 41;"),
                    "creating persistent /eval session variable");
                Assert.Equal("execution", created.GetProperty("phase").GetString());

                JsonElement visible = await ExpectOkAsync(
                    await run.Client.EvalAsync("issue02Value + 1"),
                    "reading persistent /eval session variable");
                Assert.Equal(42, visible.GetProperty("result").GetInt32());

                JsonElement isolatedFailure = await ExpectFailureAsync(
                    await run.Client.EvalIsolatedAsync("issue02Value"),
                    "verifying /eval-isolated cannot see persistent /eval state");
                Assert.Equal("compilation", isolatedFailure.GetProperty("phase").GetString());

                JsonElement reset = await ExpectOkAsync(
                    await run.Client.ResetSessionAsync(),
                    "resetting persistent /eval session");
                Assert.Equal("reset", reset.GetProperty("phase").GetString());
                Assert.True(reset.GetProperty("sessionVersion").GetInt32() > 0, "Reset should advance the persistent session version.");

                JsonElement resetFailure = await ExpectFailureAsync(
                    await run.Client.EvalAsync("issue02Value"),
                    "verifying /reset-session cleared persistent /eval state");
                Assert.Equal("compilation", resetFailure.GetProperty("phase").GetString());
            }
            finally
            {
                if (run != null)
                {
                    await Record.ExceptionAsync(() => run.Client.ResetSessionAsync());
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("persistent-eval-session");
            }
        }

        private static Task<JsonElement> ExpectOkAsync(BridgeResponse response, string transition)
        {
            Assert.Equal(200, response.StatusCode);
            JsonElement root = JsonDocument.Parse(response.Content).RootElement.Clone();
            Assert.True(root.GetProperty("ok").GetBoolean(), "Expected success while " + transition + ":\n" + response.Content);
            return Task.FromResult(root);
        }

        private static Task<JsonElement> ExpectFailureAsync(BridgeResponse response, string transition)
        {
            Assert.True(response.StatusCode >= 400, "Expected failure while " + transition + ":\n" + response.Content);
            JsonElement root = JsonDocument.Parse(response.Content).RootElement.Clone();
            Assert.False(root.GetProperty("ok").GetBoolean(), "Expected ok:false while " + transition + ":\n" + response.Content);
            return Task.FromResult(root);
        }
    }
}
