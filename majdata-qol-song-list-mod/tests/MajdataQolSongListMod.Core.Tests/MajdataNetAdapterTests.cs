using System;
using System.Collections.Generic;
using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class MajdataNetAdapterTests
    {
        private const string RepresentativeChartListJson =
            "[" +
            "{" +
            "\"id\":\"1\"," +
            "\"title\":\"Hana no Tou\"," +
            "\"artist\":\"Sayuri\"," +
            "\"designer\":\"FTOWER\"," +
            "\"description\":\"sample\"," +
            "\"levels\":[null,null,null,null,\"13+\",null,null]," +
            "\"timestamp\":\"2025-03-05T14:45:24.4705018+08:00\"," +
            "\"hash\":\"naksqJTTV0fMM//FRcdO0g==\"," +
            "\"uploader\":\"uploader-a\"," +
            "\"tags\":[\"anime\"]," +
            "\"publicTags\":[\"public\"]" +
            "}," +
            "{" +
            "\"id\":\"2\"," +
            "\"title\":\"Second Song\"," +
            "\"artist\":\"Second Artist\"," +
            "\"designers\":[\"Designer A\",\"Designer B\"]," +
            "\"levels\":[\"4\",\"7\",\"10\",\"12\",\"13\",null,\"?\"]," +
            "\"timestamp\":\"not a timestamp\"," +
            "\"hash\":\"hash-two\"" +
            "}" +
            "]";

        [Fact]
        public void ChartListJsonConvertsIntoOnlineCatalogInputs()
        {
            MajdataNetAdapter adapter = new MajdataNetAdapter("https://majdata.example", new FakeFetcher(RepresentativeChartListJson));

            CatalogInput[] rows = adapter.ConvertChartListJson(RepresentativeChartListJson).ToArray();

            Assert.Equal(2, rows.Length);
            CatalogInput first = rows[0];
            Assert.Equal(CatalogSource.Online, first.Source);
            Assert.Equal("1", first.OnlineId);
            Assert.Equal("Hana no Tou", first.Title);
            Assert.Equal("Sayuri", first.Artist);
            Assert.Equal("uploader-a", first.Uploader);
            Assert.Equal("naksqJTTV0fMM//FRcdO0g==", first.Hash);
            Assert.Equal("FTOWER", Assert.Single(first.Designers));
            Assert.Equal(7, first.Levels.Count);
            Assert.Equal("Master", first.Levels[4].DifficultyName);
            Assert.Equal("13+", first.Levels[4].Value);
            Assert.True(first.Timestamp.HasValue);

            CatalogInput second = rows[1];
            Assert.Equal(new[] { "Designer A", "Designer B" }, second.Designers.ToArray());
            Assert.Null(second.Timestamp);
            Assert.Equal("UTAGE", second.Levels[6].DifficultyName);
        }

        [Fact]
        public void FetchPublicChartListUsesUnauthenticatedListEndpoint()
        {
            FakeFetcher fetcher = new FakeFetcher(RepresentativeChartListJson);
            MajdataNetAdapter adapter = new MajdataNetAdapter("https://majdata.example/", fetcher);

            MajdataNetResult<IReadOnlyList<CatalogInput>> result = adapter.FetchPublicChartList();

            Assert.True(result.Success);
            Assert.Equal("https://majdata.example/api/maichart/list", fetcher.LastUrl);
            Assert.Equal(2, result.Value.Count);
        }

        [Fact]
        public void NetworkFailureIsRecoverable()
        {
            MajdataNetAdapter adapter = new MajdataNetAdapter("https://majdata.example", new FakeFetcher(new InvalidOperationException("network down")));

            MajdataNetResult<IReadOnlyList<CatalogInput>> result = adapter.FetchPublicChartList();

            Assert.False(result.Success);
            Assert.Contains("network down", result.Error);
        }

        [Fact]
        public void RandomRecommendationsAreBuiltFromPublicChartList()
        {
            RandomRecommendationService service = new RandomRecommendationService(
                new MajdataNetAdapter("https://majdata.example", new FakeFetcher(RepresentativeChartListJson)));

            MajdataNetResult<RandomRecommendationBatch> result = service.BuildRecommendations(
                new RandomRecommendationRequest(seed: 10, batchSize: 2, useLocalFallback: false),
                null);

            Assert.True(result.Success);
            Assert.False(result.Value.FromLocalFallback);
            Assert.Equal(2, result.Value.Rows.Count);
            Assert.All(result.Value.Rows, row => Assert.True(row.HasOnlineMetadata));
        }

        [Fact]
        public void RecommendationShuffleIsDeterministicForSeedAndVariesAcrossSeeds()
        {
            string json =
                "[" +
                Chart("1", "A", "hash-a") + "," +
                Chart("2", "B", "hash-b") + "," +
                Chart("3", "C", "hash-c") + "," +
                Chart("4", "D", "hash-d") +
                "]";
            RandomRecommendationService firstService = new RandomRecommendationService(new MajdataNetAdapter("https://majdata.example", new FakeFetcher(json)));
            RandomRecommendationService secondService = new RandomRecommendationService(new MajdataNetAdapter("https://majdata.example", new FakeFetcher(json)));
            RandomRecommendationService thirdService = new RandomRecommendationService(new MajdataNetAdapter("https://majdata.example", new FakeFetcher(json)));

            string[] first = firstService.BuildRecommendations(new RandomRecommendationRequest(123, 4, false), null).Value.Rows.Select(row => row.Hash).ToArray();
            string[] sameSeed = secondService.BuildRecommendations(new RandomRecommendationRequest(123, 4, false), null).Value.Rows.Select(row => row.Hash).ToArray();
            string[] differentSeed = thirdService.BuildRecommendations(new RandomRecommendationRequest(456, 4, false), null).Value.Rows.Select(row => row.Hash).ToArray();

            Assert.Equal(first, sameSeed);
            Assert.NotEqual(first, differentSeed);
        }

        [Fact]
        public void ConfiguredLocalFallbackProducesRecommendationsWhenFetchFails()
        {
            RandomRecommendationService service = new RandomRecommendationService(
                new MajdataNetAdapter("https://majdata.example", new FakeFetcher(new InvalidOperationException("offline"))));
            CatalogRow[] localRows =
            {
                LocalRow("local-a", "Local A"),
                LocalRow("local-b", "Local B")
            };

            MajdataNetResult<RandomRecommendationBatch> result = service.BuildRecommendations(
                new RandomRecommendationRequest(seed: 7, batchSize: 1, useLocalFallback: true),
                localRows);

            Assert.True(result.Success);
            Assert.True(result.Value.FromLocalFallback);
            Assert.Single(result.Value.Rows);
            Assert.Contains(result.Value.Rows[0], localRows);
        }

        [Fact]
        public void FailureWithoutFallbackReturnsRecoverableResult()
        {
            RandomRecommendationService service = new RandomRecommendationService(
                new MajdataNetAdapter("https://majdata.example", new FakeFetcher(new InvalidOperationException("offline"))));

            MajdataNetResult<RandomRecommendationBatch> result = service.BuildRecommendations(
                new RandomRecommendationRequest(seed: 7, batchSize: 1, useLocalFallback: false),
                new[] { LocalRow("local-a", "Local A") });

            Assert.False(result.Success);
            Assert.Contains("offline", result.Error);
        }

        private static string Chart(string id, string title, string hash)
        {
            return "{\"id\":\"" + id + "\",\"title\":\"" + title + "\",\"artist\":\"Artist\",\"designer\":\"Designer\",\"levels\":[\"1\"],\"timestamp\":\"2026-01-01T00:00:00Z\",\"hash\":\"" + hash + "\"}";
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

            public string LastUrl { get; private set; }

            public string GetString(string url)
            {
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
