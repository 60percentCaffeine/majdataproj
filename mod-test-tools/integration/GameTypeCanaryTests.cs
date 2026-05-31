using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class GameTypeCanaryTests
    {
        [Fact]
        public async Task CoreAssembliesAndRepresentativeGameTypesAreLoadable()
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
    string[] requiredAssemblies = new[] {
        ""Assembly-CSharp"",
        ""MajSimai"",
        ""UnityEngine.CoreModule"",
        ""Unity.InputSystem"",
        ""ManagedBass""
    };
    string[] requiredTypes = new[] {
        ""MajdataPlay.GameManager, Assembly-CSharp"",
        ""MajdataPlay.IO.InputManager, Assembly-CSharp"",
        ""MajdataPlay.IO.AudioManager, Assembly-CSharp"",
        ""MajdataPlay.Settings.ChartSetting, Assembly-CSharp"",
        ""MajSimai.SimaiParser, MajSimai"",
        ""MajdataPlay.Scenes.Game.GamePlayManager, Assembly-CSharp"",
        ""MajdataPlay.Scenes.Game.NoteLoader, Assembly-CSharp"",
        ""MajdataPlay.Scenes.Game.Notes.Controllers.NoteManager, Assembly-CSharp"",
        ""MajdataPlay.Scenes.Game.Buffers.NoteInfo, Assembly-CSharp""
    };
    var loadedAssemblyNames = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
    var missingAssemblies = requiredAssemblies.Where(name => !loadedAssemblyNames.Contains(name)).ToArray();
    var missingTypes = requiredTypes.Where(name => Type.GetType(name, false) == null).ToArray();
    return new {
        CoreGameAssemblyLoaded = loadedAssemblyNames.Contains(""Assembly-CSharp""),
        RequiredAssemblyCount = requiredAssemblies.Length,
        MissingAssemblies = string.Join(""\n"", missingAssemblies),
        RequiredTypeCount = requiredTypes.Length,
        ResolvedTypeCount = requiredTypes.Length - missingTypes.Length,
        MissingTypes = string.Join(""\n"", missingTypes)
    };
})()");

                JsonElement properties = IntegrationAssertions.EvalResultProperties(response);
                Assert.True(properties.GetProperty("CoreGameAssemblyLoaded").GetBoolean(), "Assembly-CSharp is not loaded.");
                Assert.Equal("", properties.GetProperty("MissingAssemblies").GetString());
                Assert.Equal("", properties.GetProperty("MissingTypes").GetString());
                Assert.Equal(
                    properties.GetProperty("RequiredTypeCount").GetInt32(),
                    properties.GetProperty("ResolvedTypeCount").GetInt32());
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

                harness.CollectLogs("game-types");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "game-types")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }
    }
}
