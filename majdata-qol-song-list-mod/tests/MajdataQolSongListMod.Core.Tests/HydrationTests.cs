using System;
using System.IO;
using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class HydrationTests : IDisposable
    {
        private readonly string _tempRoot;

        public HydrationTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "MajdataQolSongListModTests", Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Fact]
        public void CacheDataIsStoredUnderModDirectoryAndSurvivesRestart()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);
            HydrationStore first = new HydrationStore(_tempRoot);

            first.Write(new HydrationCacheValue("hash-a", HydrationDataKind.Bpm, "120", now));
            HydrationStore second = new HydrationStore(_tempRoot);
            HydrationStoreReadResult read = second.Read("hash-a", HydrationDataKind.Bpm, now.AddMinutes(1), TimeSpan.FromHours(1));

            Assert.Equal(Path.Combine(_tempRoot, HydrationStore.ModCacheDirectoryName), first.CacheDirectory);
            Assert.True(Directory.Exists(first.CacheDirectory));
            Assert.NotNull(read.Value);
            Assert.Equal("120", read.Value.Payload);
            Assert.False(read.ShouldRefresh);
        }

        [Fact]
        public void FreshValuesAreUsedWithoutRefreshAndStaleValuesQueueRefresh()
        {
            DateTimeOffset fetchedAt = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);
            HydrationStore store = new HydrationStore(_tempRoot);
            store.Write(new HydrationCacheValue("hash-a", HydrationDataKind.InteractionStats, "plays=10", fetchedAt));

            HydrationStoreReadResult fresh = store.Read("hash-a", HydrationDataKind.InteractionStats, fetchedAt.AddHours(1), TimeSpan.FromHours(24));
            HydrationStoreReadResult stale = store.Read("hash-a", HydrationDataKind.InteractionStats, fetchedAt.AddHours(25), TimeSpan.FromHours(24));
            HydrationStoreReadResult missing = store.Read("missing", HydrationDataKind.InteractionStats, fetchedAt, TimeSpan.FromHours(24));

            Assert.NotNull(fresh.Value);
            Assert.False(fresh.ShouldRefresh);
            Assert.NotNull(stale.Value);
            Assert.True(stale.ShouldRefresh);
            Assert.Null(missing.Value);
            Assert.True(missing.ShouldRefresh);
        }

        [Fact]
        public void HydrationPriorityOrdersMissingBeforeStale()
        {
            CatalogRow missing = LocalRow("missing", "Missing");
            CatalogRow stale = LocalRow("stale", "Stale");

            HydrationWorkItem[] ordered = Order(
                new HydrationWorkItem(stale, HydrationDataKind.Bpm, missing: false, stale: true),
                new HydrationWorkItem(missing, HydrationDataKind.Bpm, missing: true, stale: false));

            Assert.Equal(new[] { "missing", "stale" }, ordered.Select(item => item.Row.Hash).ToArray());
        }

        [Fact]
        public void OfflineRowsQueueAheadOfOnlineRows()
        {
            CatalogRow local = LocalRow("local", "Local");
            CatalogRow online = OnlineRow("online", "Online");

            HydrationWorkItem[] ordered = Order(
                new HydrationWorkItem(online, HydrationDataKind.Bpm, missing: true, stale: false),
                new HydrationWorkItem(local, HydrationDataKind.Bpm, missing: true, stale: false));

            Assert.Equal(new[] { "local", "online" }, ordered.Select(item => item.Row.Hash).ToArray());
        }

        [Fact]
        public void SpecialOnlineRowsQueueAheadOfNormalOnlineRows()
        {
            CatalogRow normal = OnlineRow("normal", "Normal");
            CatalogRow special = OnlineRow("special", "Special");

            HydrationWorkItem[] ordered = new HydrationScheduler().OrderWork(
                new[]
                {
                    new HydrationWorkItem(normal, HydrationDataKind.Bpm, missing: true, stale: false),
                    new HydrationWorkItem(special, HydrationDataKind.Bpm, missing: true, stale: false)
                },
                new BrowsingContext(null, null, new[] { "special" }),
                HydrationSceneState.List).ToArray();

            Assert.Equal(new[] { "special", "normal" }, ordered.Select(item => item.Row.Hash).ToArray());
        }

        [Fact]
        public void VisibleAndNearbyRowsQueueAheadOfNonVisibleRows()
        {
            CatalogRow hidden = OnlineRow("hidden", "Hidden");
            CatalogRow nearby = OnlineRow("nearby", "Nearby");
            CatalogRow visible = OnlineRow("visible", "Visible");

            HydrationWorkItem[] ordered = new HydrationScheduler().OrderWork(
                new[]
                {
                    new HydrationWorkItem(hidden, HydrationDataKind.Bpm, missing: true, stale: false),
                    new HydrationWorkItem(nearby, HydrationDataKind.Bpm, missing: true, stale: false),
                    new HydrationWorkItem(visible, HydrationDataKind.Bpm, missing: true, stale: false)
                },
                new BrowsingContext(new[] { "visible" }, new[] { "nearby" }, null),
                HydrationSceneState.List).ToArray();

            Assert.Equal(new[] { "visible", "nearby", "hidden" }, ordered.Select(item => item.Row.Hash).ToArray());
        }

        [Theory]
        [InlineData(HydrationSceneState.Title, true)]
        [InlineData(HydrationSceneState.Menu, true)]
        [InlineData(HydrationSceneState.List, true)]
        [InlineData(HydrationSceneState.Setting, true)]
        [InlineData(HydrationSceneState.Login, true)]
        [InlineData(HydrationSceneState.Gameplay, false)]
        [InlineData(HydrationSceneState.Practice, false)]
        public void GameplayAndPracticeBlockHydration(HydrationSceneState sceneState, bool expected)
        {
            HydrationScheduler scheduler = new HydrationScheduler();
            HydrationWorkItem item = new HydrationWorkItem(LocalRow("row", "Row"), HydrationDataKind.Bpm, missing: true, stale: false);

            Assert.Equal(expected, scheduler.AllowsHydration(sceneState));
            Assert.Equal(expected ? 1 : 0, scheduler.OrderWork(new[] { item }, null, sceneState).Count);
        }

        [Fact]
        public void NetworkFailureIsRecoverableAndDoesNotDeleteStaleData()
        {
            DateTimeOffset fetchedAt = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);
            HydrationStore store = new HydrationStore(_tempRoot);
            store.Write(new HydrationCacheValue("hash-a", HydrationDataKind.InteractionStats, "plays=7", fetchedAt));

            store.RecordFailure("hash-a", HydrationDataKind.InteractionStats, "network failed");
            HydrationStoreReadResult read = store.Read("hash-a", HydrationDataKind.InteractionStats, fetchedAt.AddHours(25), TimeSpan.FromHours(24));

            Assert.NotNull(read.Value);
            Assert.Equal("plays=7", read.Value.Payload);
            Assert.True(read.ShouldRefresh);
            Assert.Contains("network failed", File.ReadAllText(Path.Combine(store.CacheDirectory, "hash-a.InteractionStats.failure.txt")));
        }

        private static HydrationWorkItem[] Order(params HydrationWorkItem[] items)
        {
            return new HydrationScheduler().OrderWork(items, null, HydrationSceneState.List).ToArray();
        }

        private static CatalogRow LocalRow(string hash, string title)
        {
            return CatalogIndex.Build(new[]
            {
                CatalogInput.Local(
                    hash,
                    title,
                    "Artist",
                    "Folder",
                    new[] { new CatalogLevel(0, "Easy", "1") },
                    new[] { "Designer" },
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    ScoreFacet.Empty(),
                    HydrationState.Fresh)
            }).Rows.Single();
        }

        private static CatalogRow OnlineRow(string hash, string title)
        {
            return CatalogIndex.Build(new[]
            {
                CatalogInput.Online(
                    hash,
                    "online-" + hash,
                    title,
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
    }
}
