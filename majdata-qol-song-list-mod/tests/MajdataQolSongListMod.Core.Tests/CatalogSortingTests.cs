using System;
using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class CatalogSortingTests
    {
        [Fact]
        public void SortOptionSetMatchesUiProposalAndOmitsUnsupportedLabels()
        {
            string[] labels = MapListOptionCatalog.GetSortModes().Select(option => option.Label).ToArray();

            Assert.Equal(
                new[]
                {
                    "Default",
                    "Date Added",
                    "Difficulty",
                    "Note Designer",
                    "Title",
                    "Rank",
                    "Artist",
                    "Play Count",
                    "BPM",
                    "AP/FC Rank",
                    "DX Score"
                },
                labels);
            Assert.DoesNotContain("Sync", labels);
        }

        [Fact]
        public void ArtistTitleAndReleaseSortsAreDeterministic()
        {
            CatalogRow beta = Row("b", "Beta", "zeta", "Designer B", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            CatalogRow alpha2 = Row("c", "Alpha", "beta", "Designer C", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
            CatalogRow alpha1 = Row("a", "Alpha", "alpha", "Designer A", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

            AssertOrder(new[] { alpha1, alpha2, beta }, MapListSortMode.Artist, "a", "c", "b");
            AssertOrder(new[] { beta, alpha2, alpha1 }, MapListSortMode.Title, "a", "c", "b");
            AssertOrder(new[] { beta, alpha2, alpha1 }, MapListSortMode.DateAdded, "c", "a", "b");
            AssertOrder(new[] { beta, alpha2, alpha1 }, MapListSortMode.NoteDesigner, "a", "b", "c");
        }

        [Fact]
        public void LocalPlayCountSortsImmediatelyBeforeOnlineAndUnknown()
        {
            CatalogRow localTen = Row("local10", "Local Ten", playCount: 10);
            CatalogRow onlineFive = OnlineRow("online5", "Online Five", onlinePlayCount: 5);
            CatalogRow localZero = Row("local0", "Local Zero", playCount: 0);
            CatalogRow unknown = OnlineRow("unknown", "Unknown", onlinePlayCount: null);

            AssertOrder(
                new[] { unknown, localZero, onlineFive, localTen },
                new CatalogSortRequest(CatalogSortMode.PlayCount, 0),
                "local10",
                "online5",
                "local0",
                "unknown");
        }

        [Fact]
        public void UnknownOnlinePlayCountIsDistinctFromConfirmedZero()
        {
            CatalogRow confirmedZero = OnlineRow("zero", "Confirmed Zero", onlinePlayCount: 0);
            CatalogRow unknown = OnlineRow("unknown", "Unknown", onlinePlayCount: null);

            Assert.True(confirmedZero.Interaction.OnlinePlayCount.HasValue);
            Assert.False(unknown.Interaction.OnlinePlayCount.HasValue);
            AssertOrder(new[] { unknown, confirmedZero }, new CatalogSortRequest(CatalogSortMode.PlayCount, 0), "zero", "unknown");
        }

        [Fact]
        public void BpmSortUsesKnownValuesAndPlacesPendingUnknownAfterKnown()
        {
            CatalogRow fast = Row("fast", "Fast", bpm: BpmFacet.Known(240m));
            CatalogRow slow = Row("slow", "Slow", bpm: BpmFacet.Known(120m));
            CatalogRow pending = Row("pending", "Pending", bpm: BpmFacet.Pending());
            CatalogRow unknown = Row("unknown", "Unknown", bpm: BpmFacet.Unknown());

            AssertOrder(new[] { unknown, fast, pending, slow }, MapListSortMode.Bpm, "slow", "fast", "pending", "unknown");
        }

        [Fact]
        public void RankApFcAndDxScoreDegradeCleanlyWhenMissing()
        {
            CatalogRow ap = Row("ap", "AP", rank: "SSS", allPerfect: true, dxScore: 990000);
            CatalogRow fc = Row("fc", "FC", rank: "SS", fullCombo: true, dxScore: 980000);
            CatalogRow none = Row("none", "None", rank: null, dxScore: null);
            CatalogRow zeroDx = Row("zero", "Zero", rank: "S", dxScore: 0);

            AssertOrder(new[] { none, zeroDx, fc, ap }, MapListSortMode.Rank, "ap", "fc", "zero", "none");
            AssertOrder(new[] { none, fc, ap }, MapListSortMode.ApFcRank, "ap", "fc", "none");
            AssertOrder(new[] { none, zeroDx, fc, ap }, new CatalogSortRequest(CatalogSortMode.DxScore, 0), "ap", "fc", "zero", "none");
        }

        [Fact]
        public void StaleKnownValuesStillSortBeforeUnknown()
        {
            CatalogRow staleOnline = OnlineRow("stale", "Stale", onlinePlayCount: 3, hydrationState: HydrationState.Stale);
            CatalogRow unknown = OnlineRow("unknown", "Unknown", onlinePlayCount: null, hydrationState: HydrationState.Unknown);

            Assert.Equal(HydrationState.Stale, staleOnline.HydrationState);
            AssertOrder(new[] { unknown, staleOnline }, new CatalogSortRequest(CatalogSortMode.PlayCount, 0), "stale", "unknown");
        }

        private static void AssertOrder(CatalogRow[] rows, MapListSortMode sortMode, params string[] expectedHashes)
        {
            AssertOrder(rows, CatalogSortRequest.FromMapListSort(sortMode, 0), expectedHashes);
        }

        private static void AssertOrder(CatalogRow[] rows, CatalogSortRequest request, params string[] expectedHashes)
        {
            CatalogSorter sorter = new CatalogSorter();
            Assert.Equal(expectedHashes, sorter.Sort(rows, request).Select(row => row.Hash).ToArray());
        }

        private static CatalogRow Row(
            string hash,
            string title,
            string artist = "Artist",
            string designer = "Designer",
            DateTimeOffset? timestamp = null,
            int? playCount = null,
            string rank = null,
            bool fullCombo = false,
            bool allPerfect = false,
            int? dxScore = null,
            BpmFacet bpm = null)
        {
            return CatalogIndex.Build(new[]
            {
                CatalogInput.Local(
                    hash,
                    title,
                    artist,
                    "Folder",
                    new[] { new CatalogLevel(0, "Easy", "7") },
                    new[] { designer },
                    timestamp,
                    new ScoreFacet(rank, playCount, fullCombo, allPerfect, dxScore),
                    HydrationState.Fresh,
                    bpm)
            }).Rows.Single();
        }

        private static CatalogRow OnlineRow(string hash, string title, int? onlinePlayCount, HydrationState hydrationState = HydrationState.Fresh)
        {
            return CatalogIndex.Build(new[]
            {
                CatalogInput.Online(
                    hash,
                    "online-" + hash,
                    title,
                    "Artist",
                    "Uploader",
                    new[] { new CatalogLevel(0, "Easy", "7") },
                    new[] { "Designer" },
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    new InteractionFacet(onlinePlayCount, null, null, DateTimeOffset.UtcNow),
                    null,
                    hydrationState)
            }).Rows.Single();
        }
    }
}
