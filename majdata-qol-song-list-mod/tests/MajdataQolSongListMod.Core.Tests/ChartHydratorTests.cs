using System;
using System.IO;
using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class ChartHydratorTests : IDisposable
    {
        private readonly string _tempRoot;

        public ChartHydratorTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "MajdataQolSongListModHydratorTests", Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Fact]
        public void LocalMaidataParsingCalculatesFixedBpm()
        {
            ChartDataHydrator hydrator = new ChartDataHydrator(new FakeFetcher(""), "https://majdata.example");

            BpmFacet bpm = hydrator.CalculateBpmFromMaidata("&title=Song\n&inote_1=(120){4}1,2,3,4,");

            Assert.True(bpm.HasKnownValue);
            Assert.Equal(120m, bpm.Minimum);
            Assert.Equal(120m, bpm.Maximum);
            Assert.Equal("120BPM", ChartDataHydrator.FormatBpm(bpm));
        }

        [Fact]
        public void LocalMaidataParsingCalculatesBpmRange()
        {
            ChartDataHydrator hydrator = new ChartDataHydrator(new FakeFetcher(""), "https://majdata.example");

            BpmFacet bpm = hydrator.CalculateBpmFromMaidata("&inote_1=(120.5){4}1,2,(240){4}3,4,");

            Assert.Equal(120.5m, bpm.Minimum);
            Assert.Equal(240m, bpm.Maximum);
            Assert.Equal("120.5-240BPM", ChartDataHydrator.FormatBpm(bpm));
        }

        [Fact]
        public void MalformedMaidataProducesUnknownBpm()
        {
            ChartDataHydrator hydrator = new ChartDataHydrator(new FakeFetcher(""), "https://majdata.example");

            BpmFacet bpm = hydrator.CalculateBpmFromMaidata("&inote_1=(wat){4}1,2,");

            Assert.False(bpm.HasKnownValue);
            Assert.Equal(HydrationState.Unknown, bpm.State);
            Assert.Equal("unknown BPM", ChartDataHydrator.FormatBpm(bpm));
        }

        [Fact]
        public void BpmFormatSupportsPendingAndUnknown()
        {
            Assert.Equal("BPM pending", ChartDataHydrator.FormatBpm(BpmFacet.Pending()));
            Assert.Equal("unknown BPM", ChartDataHydrator.FormatBpm(BpmFacet.Unknown()));
            Assert.Equal("unknown BPM", ChartDataHydrator.FormatBpm(null));
        }

        [Fact]
        public void OnlineBpmHydrationFetchesOnlyWhenQueued()
        {
            FakeFetcher fetcher = new FakeFetcher("(180){4}1,2,");
            ChartDataHydrator hydrator = new ChartDataHydrator(fetcher, "https://majdata.example");
            CatalogRow row = OnlineRow("hash-a", "online-a");

            MajdataNetResult<BpmFacet> notQueued = hydrator.HydrateOnlineBpm(row, queued: false);
            MajdataNetResult<BpmFacet> queued = hydrator.HydrateOnlineBpm(row, queued: true);

            Assert.True(notQueued.Success);
            Assert.Equal(HydrationState.Pending, notQueued.Value.State);
            Assert.Equal(1, fetcher.CallCount);
            Assert.Equal("https://majdata.example/api/maichart/online-a/chart", fetcher.LastUrl);
            Assert.True(queued.Value.HasKnownValue);
            Assert.Equal(180m, queued.Value.Minimum);
        }

        [Fact]
        public void OnlineInteractionStatsParseAndCacheWithFreshness()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);
            HydrationStore store = new HydrationStore(_tempRoot);
            OnlineStatsHydrator hydrator = new OnlineStatsHydrator(new FakeFetcher("{\"playCount\":12,\"likeCount\":3,\"commentCount\":4}"), "https://majdata.example", store);
            CatalogRow row = OnlineRow("hash-stats", "online-stats");

            MajdataNetResult<InteractionFacet> result = hydrator.HydrateOnlineStats(row, now);
            HydrationStoreReadResult fresh = hydrator.ReadCachedStats(row, now.AddHours(1));
            HydrationStoreReadResult stale = hydrator.ReadCachedStats(row, now.AddHours(25));

            Assert.True(result.Success);
            Assert.Equal(12, result.Value.OnlinePlayCount);
            Assert.Equal(3, result.Value.LikeCount);
            Assert.Equal(4, result.Value.CommentCount);
            Assert.NotNull(fresh.Value);
            Assert.False(fresh.ShouldRefresh);
            Assert.NotNull(stale.Value);
            Assert.True(stale.ShouldRefresh);
        }

        [Fact]
        public void OnlineStatsFailureIsRecoverableAndDoesNotDeleteStaleStats()
        {
            DateTimeOffset old = new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset now = old.AddHours(25);
            HydrationStore store = new HydrationStore(_tempRoot);
            CatalogRow row = OnlineRow("hash-fail", "online-fail");
            store.Write(new HydrationCacheValue(row.Hash, HydrationDataKind.InteractionStats, "play=7", old));
            OnlineStatsHydrator hydrator = new OnlineStatsHydrator(new FakeFetcher(new InvalidOperationException("network failed")), "https://majdata.example", store);

            MajdataNetResult<InteractionFacet> result = hydrator.HydrateOnlineStats(row, now);
            HydrationStoreReadResult stale = hydrator.ReadCachedStats(row, now);

            Assert.False(result.Success);
            Assert.Contains("network failed", result.Error);
            Assert.NotNull(stale.Value);
            Assert.Equal("play=7", stale.Value.Payload);
            Assert.True(stale.ShouldRefresh);
        }

        [Fact]
        public void InteractionStatsMissingPlayCountDegradesAsUnknown()
        {
            OnlineStatsHydrator hydrator = new OnlineStatsHydrator(new FakeFetcher(""), "https://majdata.example", new HydrationStore(_tempRoot));

            InteractionFacet facet = hydrator.ParseInteractionStats("{\"likeCount\":1}", new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero));

            Assert.False(facet.OnlinePlayCount.HasValue);
            Assert.Equal(1, facet.LikeCount);
        }

        private static CatalogRow OnlineRow(string hash, string onlineId)
        {
            return CatalogIndex.Build(new[]
            {
                CatalogInput.Online(
                    hash,
                    onlineId,
                    "Online Song",
                    "Artist",
                    "Uploader",
                    new[] { new CatalogLevel(0, "Easy", "1") },
                    new[] { "Designer" },
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    InteractionFacet.Empty(),
                    null,
                    HydrationState.Unknown)
            }).Rows.Single();
        }

        private sealed class FakeFetcher : ITextFetcher
        {
            private readonly string _body;
            private readonly Exception _exception;

            public FakeFetcher(string body)
            {
                _body = body;
            }

            public FakeFetcher(Exception exception)
            {
                _exception = exception;
            }

            public int CallCount { get; private set; }

            public string LastUrl { get; private set; }

            public string GetString(string url)
            {
                CallCount++;
                LastUrl = url;
                if (_exception != null)
                {
                    throw _exception;
                }

                return _body;
            }
        }
    }
}
