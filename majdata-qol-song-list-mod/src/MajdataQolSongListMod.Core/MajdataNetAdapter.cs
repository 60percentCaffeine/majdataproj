using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MajdataQolSongListMod.Core
{
    public interface ITextFetcher
    {
        string GetString(string url);
    }

    public sealed class HttpTextFetcher : ITextFetcher
    {
        public string GetString(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                return client.GetStringAsync(url).GetAwaiter().GetResult();
            }
        }
    }

    public sealed class MajdataNetResult<T>
    {
        private MajdataNetResult(bool success, T value, string error)
        {
            Success = success;
            Value = value;
            Error = error;
        }

        public bool Success { get; private set; }

        public T Value { get; private set; }

        public string Error { get; private set; }

        public static MajdataNetResult<T> Ok(T value)
        {
            return new MajdataNetResult<T>(true, value, null);
        }

        public static MajdataNetResult<T> RecoverableFailure(string error)
        {
            return new MajdataNetResult<T>(false, default(T), string.IsNullOrWhiteSpace(error) ? "Unknown MajdataNet error." : error);
        }
    }

    public sealed class MajdataNetAdapter
    {
        private static readonly string[] DifficultyNames =
        {
            "Easy",
            "Basic",
            "Advance",
            "Expert",
            "Master",
            "ReMaster",
            "UTAGE"
        };

        private readonly string _baseUrl;
        private readonly ITextFetcher _textFetcher;

        public MajdataNetAdapter(string baseUrl, ITextFetcher textFetcher)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://majdata.net" : baseUrl.TrimEnd('/');
            _textFetcher = textFetcher ?? new HttpTextFetcher();
        }

        public MajdataNetResult<IReadOnlyList<CatalogInput>> FetchPublicChartList()
        {
            try
            {
                string json = _textFetcher.GetString(_baseUrl + "/api/maichart/list");
                return MajdataNetResult<IReadOnlyList<CatalogInput>>.Ok(ConvertChartListJson(json));
            }
            catch (Exception ex)
            {
                return MajdataNetResult<IReadOnlyList<CatalogInput>>.RecoverableFailure(ex.Message);
            }
        }

        public IReadOnlyList<CatalogInput> ConvertChartListJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CatalogInput[0];
            }

            MajdataNetChartListItem[] items = DeserializeChartList(json);
            return items.Select(ConvertChartListItem).ToArray();
        }

        private static MajdataNetChartListItem[] DeserializeChartList(string json)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(MajdataNetChartListItem[]));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (MajdataNetChartListItem[])serializer.ReadObject(stream);
            }
        }

        private static CatalogInput ConvertChartListItem(MajdataNetChartListItem item)
        {
            return CatalogInput.Online(
                item.Hash,
                item.Id,
                item.Title,
                item.Artist,
                item.Uploader,
                ConvertLevels(item.Levels),
                ConvertDesigners(item),
                ParseTimestamp(item.Timestamp),
                InteractionFacet.Empty(),
                null,
                HydrationState.Unknown);
        }

        private static IReadOnlyList<CatalogLevel> ConvertLevels(string[] levels)
        {
            if (levels == null)
            {
                return new CatalogLevel[0];
            }

            List<CatalogLevel> result = new List<CatalogLevel>();
            for (int i = 0; i < levels.Length; i++)
            {
                string difficultyName = i < DifficultyNames.Length ? DifficultyNames[i] : "Difficulty " + i.ToString();
                result.Add(new CatalogLevel(i, difficultyName, levels[i]));
            }

            return result.ToArray();
        }

        private static IReadOnlyList<string> ConvertDesigners(MajdataNetChartListItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Designer))
            {
                return new[] { item.Designer };
            }

            if (item.Designers != null)
            {
                return item.Designers.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            }

            return new string[0];
        }

        private static DateTimeOffset? ParseTimestamp(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp))
            {
                return null;
            }

            DateTimeOffset parsed;
            if (DateTimeOffset.TryParse(timestamp, out parsed))
            {
                return parsed;
            }

            return null;
        }
    }

    public sealed class RandomRecommendationRequest
    {
        public RandomRecommendationRequest(int seed, int batchSize, bool useLocalFallback)
        {
            Seed = seed;
            BatchSize = batchSize;
            UseLocalFallback = useLocalFallback;
        }

        public int Seed { get; private set; }

        public int BatchSize { get; private set; }

        public bool UseLocalFallback { get; private set; }
    }

    public sealed class RandomRecommendationBatch
    {
        public RandomRecommendationBatch(IEnumerable<CatalogRow> rows, bool fromLocalFallback)
        {
            Rows = (rows ?? Enumerable.Empty<CatalogRow>()).ToArray();
            FromLocalFallback = fromLocalFallback;
        }

        public IReadOnlyList<CatalogRow> Rows { get; private set; }

        public bool FromLocalFallback { get; private set; }
    }

    public sealed class RandomRecommendationService
    {
        private readonly MajdataNetAdapter _adapter;

        public RandomRecommendationService(MajdataNetAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException("adapter");
            }

            _adapter = adapter;
        }

        public MajdataNetResult<RandomRecommendationBatch> BuildRecommendations(RandomRecommendationRequest request, IEnumerable<CatalogRow> localFallbackRows)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            MajdataNetResult<IReadOnlyList<CatalogInput>> onlineRows = _adapter.FetchPublicChartList();
            if (onlineRows.Success)
            {
                IReadOnlyList<CatalogRow> rows = CatalogIndex.Build(onlineRows.Value).Rows;
                return MajdataNetResult<RandomRecommendationBatch>.Ok(new RandomRecommendationBatch(Pick(rows, request), false));
            }

            if (request.UseLocalFallback)
            {
                IReadOnlyList<CatalogRow> fallbackRows = (localFallbackRows ?? Enumerable.Empty<CatalogRow>()).ToArray();
                return MajdataNetResult<RandomRecommendationBatch>.Ok(new RandomRecommendationBatch(Pick(fallbackRows, request), true));
            }

            return MajdataNetResult<RandomRecommendationBatch>.RecoverableFailure(onlineRows.Error);
        }

        private static IReadOnlyList<CatalogRow> Pick(IReadOnlyList<CatalogRow> rows, RandomRecommendationRequest request)
        {
            if (rows == null || rows.Count == 0 || request.BatchSize <= 0)
            {
                return new CatalogRow[0];
            }

            List<CatalogRow> shuffled = rows.ToList();
            Random random = new Random(request.Seed);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                CatalogRow current = shuffled[i];
                shuffled[i] = shuffled[swapIndex];
                shuffled[swapIndex] = current;
            }

            return shuffled.Take(request.BatchSize).ToArray();
        }
    }

    [DataContract]
    internal sealed class MajdataNetChartListItem
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "artist")]
        public string Artist { get; set; }

        [DataMember(Name = "designer")]
        public string Designer { get; set; }

        [DataMember(Name = "designers")]
        public string[] Designers { get; set; }

        [DataMember(Name = "uploader")]
        public string Uploader { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "levels")]
        public string[] Levels { get; set; }

        [DataMember(Name = "timestamp")]
        public string Timestamp { get; set; }

        [DataMember(Name = "hash")]
        public string Hash { get; set; }

        [DataMember(Name = "tags")]
        public string[] Tags { get; set; }

        [DataMember(Name = "publicTags")]
        public string[] PublicTags { get; set; }
    }
}
