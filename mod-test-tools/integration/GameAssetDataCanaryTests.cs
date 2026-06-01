using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class GameAssetDataCanaryTests
    {
        [Fact]
        public async Task KnownChartMetadataAndAssetPathsResolveAfterBoot()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                JsonElement properties = await WaitForAssetDataAsync(run.Client, TimeSpan.FromSeconds(60));
                Assert.True(properties.GetProperty("CollectionCount").GetInt32() > 0, "SongStorage should expose at least one collection.");
                Assert.True(properties.GetProperty("NonEmptyCollectionCount").GetInt32() > 0, "SongStorage should expose at least one non-empty collection.");
                Assert.True(properties.GetProperty("TotalChartCount").GetInt64() > 0, "SongStorage.TotalChartCount should be non-zero.");
                Assert.True(properties.GetProperty("EnumeratedSongCount").GetInt32() > 0, "SongStorage collections should enumerate songs.");
                Assert.True(properties.GetProperty("KnownSongFound").GetBoolean(), "Expected MAJTITLE metadata record in SongStorage.");
                Assert.Equal("MAJTITLE", properties.GetProperty("KnownSongTitle").GetString());
                Assert.Equal("bbben", properties.GetProperty("KnownSongArtist").GetString());
                Assert.True(properties.GetProperty("KnownSongHashPresent").GetBoolean(), "Known song metadata should have a hash.");
                Assert.True(properties.GetProperty("KnownSongLevelCount").GetInt32() >= 5, "Known song metadata should expose expected chart levels.");
                Assert.True(properties.GetProperty("LocalMaidataExists").GetBoolean(), "Local MAJTITLE maidata.txt should exist.");
                Assert.True(properties.GetProperty("LocalTrackExists").GetBoolean(), "Local MAJTITLE track.mp3 should exist.");
                Assert.True(properties.GetProperty("LocalCoverExists").GetBoolean(), "Local MAJTITLE bg.png should exist.");
                Assert.True(properties.GetProperty("BuiltInMaidataExists").GetBoolean(), "Built-in MAJTITLE maidata.txt should exist.");
                Assert.True(properties.GetProperty("BuiltInTrackExists").GetBoolean(), "Built-in MAJTITLE track.opus should exist.");
                Assert.True(properties.GetProperty("DefaultSkinTapExists").GetBoolean(), "Default skin tap.png should exist.");
                Assert.True(properties.GetProperty("LocalMaidataContainsTitle").GetBoolean(), "Local MAJTITLE maidata.txt should contain title metadata.");
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

                harness.CollectLogs("asset-data");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "asset-data")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }

        private static async Task<JsonElement> WaitForAssetDataAsync(TestClient client, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            JsonElement latest = default;
            while (DateTimeOffset.UtcNow < deadline)
            {
                latest = await ReadAssetDataAsync(client);
                if (latest.GetProperty("KnownSongFound").GetBoolean())
                {
                    return latest;
                }

                await Task.Delay(2000);
            }

            return latest;
        }

        private static async Task<JsonElement> ReadAssetDataAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    string gameRoot = Environment.CurrentDirectory;
    string dataRoot = UnityEngine.Application.dataPath;
    string localMaidataPath = System.IO.Path.Combine(gameRoot, ""MaiCharts"", ""Original"", ""MAJTITLE"", ""maidata.txt"");
    string localTrackPath = System.IO.Path.Combine(gameRoot, ""MaiCharts"", ""Original"", ""MAJTITLE"", ""track.mp3"");
    string localCoverPath = System.IO.Path.Combine(gameRoot, ""MaiCharts"", ""Original"", ""MAJTITLE"", ""bg.png"");
    string builtInMaidataPath = System.IO.Path.Combine(dataRoot, ""StreamingAssets"", ""MaiCharts"", ""Original"", ""MAJTITLE"", ""maidata.txt"");
    string builtInTrackPath = System.IO.Path.Combine(dataRoot, ""StreamingAssets"", ""MaiCharts"", ""Original"", ""MAJTITLE"", ""track.opus"");
    string defaultSkinPath = System.IO.Path.Combine(gameRoot, ""Skins"", ""default"", ""TapSkins"", ""tap.png"");

    var collections = MajdataPlay.SongStorage.Collections ?? Array.Empty<MajdataPlay.Collections.SongCollection>();
    var allSongs = collections.SelectMany(collection => collection.ToArray()).ToArray();
    var majtitle = allSongs.FirstOrDefault(song => song.Title == ""MAJTITLE"");
    string localMaidataText = System.IO.File.Exists(localMaidataPath)
        ? System.IO.File.ReadAllText(localMaidataPath)
        : string.Empty;

    return new {
        CollectionCount = collections.Length,
        NonEmptyCollectionCount = collections.Count(collection => collection.Count > 0),
        TotalChartCount = MajdataPlay.SongStorage.TotalChartCount,
        EnumeratedSongCount = allSongs.Length,
        KnownSongFound = majtitle != null,
        KnownSongTitle = majtitle == null ? string.Empty : majtitle.Title,
        KnownSongArtist = majtitle == null ? string.Empty : majtitle.Artist,
        KnownSongHashPresent = majtitle != null && !string.IsNullOrWhiteSpace(majtitle.Hash),
        KnownSongLevelCount = majtitle == null ? 0 : majtitle.Levels.Length,
        LocalMaidataExists = System.IO.File.Exists(localMaidataPath),
        LocalTrackExists = System.IO.File.Exists(localTrackPath),
        LocalCoverExists = System.IO.File.Exists(localCoverPath),
        BuiltInMaidataExists = System.IO.File.Exists(builtInMaidataPath),
        BuiltInTrackExists = System.IO.File.Exists(builtInTrackPath),
        DefaultSkinTapExists = System.IO.File.Exists(defaultSkinPath),
        LocalMaidataContainsTitle = localMaidataText.Contains(""&title=MAJTITLE"")
    };
})()");

            return IntegrationAssertions.EvalResultProperties(response);
        }
    }
}
