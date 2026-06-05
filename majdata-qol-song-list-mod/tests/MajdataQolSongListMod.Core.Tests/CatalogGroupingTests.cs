using System;
using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class CatalogGroupingTests
    {
        [Theory]
        [InlineData(null, "Other")]
        [InlineData("", "Other")]
        [InlineData("   ", "Other")]
        [InlineData("wat", "Other")]
        [InlineData("-1", "Other")]
        [InlineData("13", "13")]
        [InlineData("13.0", "13")]
        [InlineData("13+", "13+")]
        [InlineData("13.1", "13+")]
        [InlineData("13.999", "13+")]
        [InlineData("14", "14")]
        [InlineData("14.0", "14")]
        [InlineData("14.1", "14+")]
        [InlineData(" 15+ ", "15+")]
        public void LevelBucketizerNormalizesKnownAndUnknownValues(string value, string expected)
        {
            Assert.Equal(expected, LevelBucketizer.Bucketize(value));
        }

        [Theory]
        [InlineData(DifficultyCountFilter.No, true)]
        [InlineData(DifficultyCountFilter.MoreThan1, true)]
        [InlineData(DifficultyCountFilter.MoreThan2, true)]
        [InlineData(DifficultyCountFilter.MoreThan3, false)]
        public void DifficultyCountFiltersUseNonemptyLevels(DifficultyCountFilter filter, bool expected)
        {
            CatalogRow row = Row(
                "multi",
                "Multi",
                "Artist",
                "SS",
                new CatalogLevel(0, "Easy", "3"),
                new CatalogLevel(1, "Basic", ""),
                new CatalogLevel(2, "Advance", "7"),
                new CatalogLevel(3, "Expert", "11"));

            Assert.Equal(3, DifficultyCount.CountUsableDifficulties(row));
            Assert.Equal(expected, DifficultyCount.Passes(row, filter));
        }

        [Fact]
        public void DefaultGroupingPassesThroughExistingFolderCollections()
        {
            CatalogRow allRow = Row("all", "All Row", "Artist", null, new CatalogLevel(0, "Easy", "1"));
            CatalogCollection[] existing =
            {
                new CatalogCollection("All", new[] { allRow }),
                new CatalogCollection("MyFavorites", new CatalogRow[0]),
                new CatalogCollection("JPORTAL", new[] { allRow })
            };

            CatalogNavigator navigator = new CatalogNavigator(CatalogIndex.Build(new[]
            {
                Input("ignored", "Ignored", "Artist", null, "Folder", new CatalogLevel(0, "Easy", "2"))
            }));

            var collections = navigator.BuildCollections(CatalogGroupingRequest.DefaultFolders(existing));

            Assert.Same(existing, collections);
            Assert.Equal(new[] { "All", "MyFavorites", "JPORTAL" }, collections.Select(collection => collection.Name).ToArray());
        }

        [Fact]
        public void DifficultyGroupingIncludesRowsInEveryUsableDifficultyAndOtherForNoLevels()
        {
            CatalogRow multi = Row(
                "multi",
                "Multi",
                "Artist",
                null,
                new CatalogLevel(0, "Easy", "3"),
                new CatalogLevel(3, "Expert", "11"),
                new CatalogLevel(4, "Master", ""));
            CatalogRow noLevels = Row("none", "No Levels", "Artist", null, new CatalogLevel(0, "Easy", ""));

            var collections = Navigator(multi, noLevels).BuildCollections(Request(MapListGroupingMode.DifficultyBracket, 0));

            Assert.Contains(collections.Single(collection => collection.Name == "Easy").Rows, row => row.Hash == "multi");
            Assert.Contains(collections.Single(collection => collection.Name == "Expert").Rows, row => row.Hash == "multi");
            Assert.DoesNotContain(collections.Single(collection => collection.Name == "Easy").Rows, row => row.Hash == "none");
            Assert.Contains(collections.Single(collection => collection.Name == "Other").Rows, row => row.Hash == "none");
        }

        [Fact]
        public void LevelGroupingUsesSelectedDifficultyBucket()
        {
            CatalogRow thirteen = Row("a", "Thirteen", "Artist", null, new CatalogLevel(3, "Expert", "13.0"));
            CatalogRow thirteenPlus = Row("b", "Thirteen Plus", "Artist", null, new CatalogLevel(3, "Expert", "13.1"));
            CatalogRow other = Row("c", "Other Level", "Artist", null, new CatalogLevel(2, "Advance", "9"));

            var collections = Navigator(thirteen, thirteenPlus, other).BuildCollections(Request(MapListGroupingMode.DifficultyLevel, 3));

            Assert.Contains(collections.Single(collection => collection.Name == "13").Rows, row => row.Hash == "a");
            Assert.Contains(collections.Single(collection => collection.Name == "13+").Rows, row => row.Hash == "b");
            Assert.Contains(collections.Single(collection => collection.Name == "Other").Rows, row => row.Hash == "c");
        }

        [Fact]
        public void ArtistGroupingUsesUnknownArtistForBlankValues()
        {
            CatalogRow known = Row("known", "Known", "bbben", null, new CatalogLevel(0, "Easy", "1"));
            CatalogRow unknown = Row("unknown", "Unknown", " ", null, new CatalogLevel(0, "Easy", "1"));

            var collections = Navigator(known, unknown).BuildCollections(Request(MapListGroupingMode.Artist, 0));

            Assert.Contains(collections.Single(collection => collection.Name == "bbben").Rows, row => row.Hash == "known");
            Assert.Contains(collections.Single(collection => collection.Name == "Unknown Artist").Rows, row => row.Hash == "unknown");
        }

        [Fact]
        public void TitleGroupingUsesAlphabeticDigitAndOtherBuckets()
        {
            CatalogRow alpha = Row("alpha", "Alpha Song", "Artist", null, new CatalogLevel(0, "Easy", "1"));
            CatalogRow digit = Row("digit", "7th Song", "Artist", null, new CatalogLevel(0, "Easy", "1"));
            CatalogRow other = Row("other", "", "Artist", null, new CatalogLevel(0, "Easy", "1"));

            var collections = Navigator(alpha, digit, other).BuildCollections(Request(MapListGroupingMode.Title, 0));

            Assert.Contains(collections.Single(collection => collection.Name == "A").Rows, row => row.Hash == "alpha");
            Assert.Contains(collections.Single(collection => collection.Name == "#").Rows, row => row.Hash == "digit");
            Assert.Contains(collections.Single(collection => collection.Name == "Other").Rows, row => row.Hash == "other");
        }

        [Fact]
        public void RankGroupingUsesNoPlayForMissingScore()
        {
            CatalogRow played = Row("played", "Played", "Artist", "SSS", new CatalogLevel(0, "Easy", "1"));
            CatalogRow noPlay = Row("nop", "No Play", "Artist", null, new CatalogLevel(0, "Easy", "1"));

            var collections = Navigator(played, noPlay).BuildCollections(Request(MapListGroupingMode.Rank, 0));

            Assert.Contains(collections.Single(collection => collection.Name == "SSS").Rows, row => row.Hash == "played");
            Assert.Contains(collections.Single(collection => collection.Name == "No Play").Rows, row => row.Hash == "nop");
        }

        [Fact]
        public void NonDefaultGroupingAppliesDifficultyFilter()
        {
            CatalogRow oneDiff = Row("one", "One", "Artist", null, new CatalogLevel(0, "Easy", "1"));
            CatalogRow twoDiffs = Row("two", "Two", "Artist", null, new CatalogLevel(0, "Easy", "1"), new CatalogLevel(1, "Basic", "2"));

            var collections = Navigator(oneDiff, twoDiffs).BuildCollections(
                new CatalogGroupingRequest(MapListGroupingMode.Artist, DifficultyCountFilter.MoreThan1, 0, null));

            CatalogCollection artist = Assert.Single(collections);
            Assert.DoesNotContain(artist.Rows, row => row.Hash == "one");
            Assert.Contains(artist.Rows, row => row.Hash == "two");
        }

        private static CatalogGroupingRequest Request(MapListGroupingMode mode, int selectedDifficultyIndex)
        {
            return new CatalogGroupingRequest(mode, DifficultyCountFilter.No, selectedDifficultyIndex, null);
        }

        private static CatalogNavigator Navigator(params CatalogRow[] rows)
        {
            return new CatalogNavigator(CatalogIndex.Build(rows.Select(row => CatalogInput.Local(
                row.Hash,
                row.Title,
                row.Artist,
                row.LocalFolder,
                row.Levels,
                row.Designers,
                row.Timestamp,
                row.Score,
                row.HydrationState))));
        }

        private static CatalogRow Row(string hash, string title, string artist, string rank, params CatalogLevel[] levels)
        {
            return CatalogIndex.Build(new[]
            {
                Input(hash, title, artist, rank, "Folder", levels)
            }).Rows.Single();
        }

        private static CatalogInput Input(string hash, string title, string artist, string rank, string folder, params CatalogLevel[] levels)
        {
            return CatalogInput.Local(
                hash,
                title,
                artist,
                folder,
                levels,
                new[] { "Designer" },
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                string.IsNullOrWhiteSpace(rank) ? ScoreFacet.Empty() : new ScoreFacet(rank, 1, false, false, 1000),
                HydrationState.Fresh);
        }
    }
}
