using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class ChartParseCanaryTests
    {
        [Fact]
        public async Task KnownGoodChartParsesThroughGameSongDetailPath()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                JsonElement properties = await WaitForParsedChartAsync(run.Client, TimeSpan.FromSeconds(75));

                Assert.True(properties.GetProperty("KnownSongFound").GetBoolean(), "Expected MAJTITLE metadata record in SongStorage.");
                Assert.Equal("MAJTITLE", properties.GetProperty("KnownSongTitle").GetString());
                Assert.True(properties.GetProperty("Parsed").GetBoolean(), "Expected MAJTITLE to parse through ISongDetail.GetMaidataAsync.");
                Assert.True(properties.GetProperty("ChartCount").GetInt32() >= 5, "Expected MAJTITLE to expose multiple chart levels.");
                Assert.InRange(properties.GetProperty("SelectedChartIndex").GetInt32(), 0, 4);
                Assert.True(properties.GetProperty("TimingPointCount").GetInt32() > 0, "Parsed chart should have timing points.");
                Assert.True(properties.GetProperty("NoteCount").GetInt32() > 0, "Parsed chart should have notes.");
                Assert.True(properties.GetProperty("BpmTimingCount").GetInt32() > 0, "Parsed chart should have BPM/timing data.");
                Assert.True(properties.GetProperty("FirstBpm").GetDouble() > 0, "Parsed chart first BPM should be positive.");
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

                harness.CollectLogs("chart-parse");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "chart-parse")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }

        private static async Task<JsonElement> WaitForParsedChartAsync(TestClient client, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            JsonElement latest = default;
            while (DateTimeOffset.UtcNow < deadline)
            {
                latest = await ReadParsedChartAsync(client);
                if (latest.GetProperty("Parsed").GetBoolean())
                {
                    return latest;
                }

                await Task.Delay(2000);
            }

            return latest;
        }

        private static async Task<JsonElement> ReadParsedChartAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    var collections = MajdataPlay.SongStorage.Collections ?? Array.Empty<MajdataPlay.Collections.SongCollection>();
    var allSongs = collections.SelectMany(collection => collection.ToArray()).ToArray();
    var song = allSongs.FirstOrDefault(candidate => candidate.Title == ""MAJTITLE"");
    if (song == null) {
        return new {
            KnownSongFound = false,
            KnownSongTitle = string.Empty,
            Parsed = false,
            ChartCount = 0,
            SelectedChartIndex = -1,
            TimingPointCount = 0,
            NoteCount = 0,
            BpmTimingCount = 0,
            FirstBpm = 0.0
        };
    }

    var maidata = song.GetMaidataAsync(true).AsTask().GetAwaiter().GetResult();
    int chartCount = maidata.Charts.Length;
    int selectedIndex = -1;
    int timingPointCount = 0;
    int noteCount = 0;
    int bpmTimingCount = 0;
    double firstBpm = 0.0;

    for (int i = 0; i < maidata.Charts.Length; i++) {
        var chart = MajSimai.SimaiParser.ParseChartAsync(maidata.Charts[i].Fumen).GetAwaiter().GetResult();
        if (chart == null || chart.NoteTimings == null || chart.NoteTimings.Length == 0) {
            continue;
        }

        selectedIndex = i;
        timingPointCount = chart.NoteTimings.Length;
        for (int timingIndex = 0; timingIndex < chart.NoteTimings.Length; timingIndex++) {
            var timing = chart.NoteTimings[timingIndex];
            noteCount += timing.Notes == null ? 0 : timing.Notes.Length;
            if (timing.Bpm > 0) {
                bpmTimingCount++;
            }
        }
        firstBpm = chart.NoteTimings[0].Bpm;
        break;
    }

    return new {
        KnownSongFound = true,
        KnownSongTitle = song.Title,
        Parsed = selectedIndex >= 0 && timingPointCount > 0 && noteCount > 0 && bpmTimingCount > 0,
        ChartCount = chartCount,
        SelectedChartIndex = selectedIndex,
        TimingPointCount = timingPointCount,
        NoteCount = noteCount,
        BpmTimingCount = bpmTimingCount,
        FirstBpm = firstBpm
    };
})()");

            return IntegrationAssertions.EvalResultProperties(response);
        }
    }
}
