using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class EvalErrorShapeRegressionTests
    {
        [Fact]
        public async Task EvalReportsStructuredCompilationExecutionAndTimeoutErrors()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));

                BridgeResponse compilationResponse = await run.Client.EvalIsolatedAsync("this is not valid csharp");
                JsonElement compilation = AssertFailure(compilationResponse, 400, "compilation", "invalid C# syntax");
                Assert.True(compilation.TryGetProperty("errors", out JsonElement errors), "Compilation failure should include an errors array:\n" + compilationResponse.Content);
                Assert.True(errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0, "Compilation errors should be a non-empty array:\n" + compilationResponse.Content);
                Assert.False(string.IsNullOrWhiteSpace(errors[0].GetProperty("message").GetString()));

                BridgeResponse executionResponse = await run.Client.EvalIsolatedAsync(@"return System.Threading.Tasks.Task.FromException<object>(
    new System.InvalidOperationException(""issue05 runtime boom""));");
                JsonElement execution = AssertFailure(executionResponse, 500, "execution", "runtime exception");
                JsonElement executionError = execution.GetProperty("error");
                Assert.Contains("InvalidOperationException", executionError.GetProperty("type").GetString());
                Assert.Contains("issue05 runtime boom", executionError.GetProperty("message").GetString());

                BridgeResponse timeoutResponse = await run.Client.EvalIsolatedAsync(new EvalRequest
                {
                    Code = @"new Func<object>(() => {
    System.Threading.Thread.Sleep(2000);
    return null;
})()",
                    TimeoutMs = 50,
                    MaxDepth = 6,
                    MaxResponseBytes = 262144
                });
                JsonElement timeout = AssertFailure(timeoutResponse, 408, "execution", "timeout");
                JsonElement timeoutError = timeout.GetProperty("error");
                Assert.Contains("TimeoutException", timeoutError.GetProperty("type").GetString());
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("eval-error-shape");
            }
        }

        private static JsonElement AssertFailure(BridgeResponse response, int expectedStatusCode, string expectedPhase, string scenario)
        {
            Assert.Equal(expectedStatusCode, response.StatusCode);
            JsonElement root = JsonDocument.Parse(response.Content).RootElement.Clone();
            Assert.False(root.GetProperty("ok").GetBoolean(), "Expected ok:false for " + scenario + ":\n" + response.Content);
            Assert.Equal(expectedPhase, root.GetProperty("phase").GetString());
            return root;
        }
    }
}
