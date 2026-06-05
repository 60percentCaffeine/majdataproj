using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace MajdataQolSongListMod.Core
{
    public sealed class ChartDataHydrator
    {
        private static readonly Regex BpmRegex = new Regex(@"\(\s*(?<bpm>[0-9]+(?:\.[0-9]+)?)\s*\)", RegexOptions.Compiled);
        private readonly ITextFetcher _textFetcher;
        private readonly string _baseUrl;

        public ChartDataHydrator(ITextFetcher textFetcher, string baseUrl)
        {
            _textFetcher = textFetcher;
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://majdata.net" : baseUrl.TrimEnd('/');
        }

        public BpmFacet CalculateBpmFromMaidata(string maidata)
        {
            decimal[] bpms = ExtractBpmValues(maidata).ToArray();
            if (bpms.Length == 0)
            {
                return BpmFacet.Unknown();
            }

            return BpmFacet.KnownRange(bpms.Min(), bpms.Max());
        }

        public MajdataNetResult<BpmFacet> HydrateOnlineBpm(CatalogRow row, bool queued)
        {
            if (!queued)
            {
                return MajdataNetResult<BpmFacet>.Ok(BpmFacet.Pending());
            }

            if (row == null || string.IsNullOrWhiteSpace(row.OnlineId))
            {
                return MajdataNetResult<BpmFacet>.RecoverableFailure("Online chart id is missing.");
            }

            try
            {
                string maidata = _textFetcher.GetString(_baseUrl + "/api/maichart/" + row.OnlineId + "/chart");
                return MajdataNetResult<BpmFacet>.Ok(CalculateBpmFromMaidata(maidata));
            }
            catch (Exception ex)
            {
                return MajdataNetResult<BpmFacet>.RecoverableFailure(ex.Message);
            }
        }

        public static string FormatBpm(BpmFacet bpm)
        {
            if (bpm == null || bpm.State == HydrationState.Unknown)
            {
                return "unknown BPM";
            }

            if (bpm.State == HydrationState.Pending)
            {
                return "BPM pending";
            }

            if (!bpm.HasKnownValue)
            {
                return "unknown BPM";
            }

            if (bpm.Minimum.Value == bpm.Maximum.Value)
            {
                return FormatDecimal(bpm.Minimum.Value) + "BPM";
            }

            return FormatDecimal(bpm.Minimum.Value) + "-" + FormatDecimal(bpm.Maximum.Value) + "BPM";
        }

        private static IEnumerable<decimal> ExtractBpmValues(string maidata)
        {
            if (string.IsNullOrWhiteSpace(maidata))
            {
                yield break;
            }

            MatchCollection matches = BpmRegex.Matches(maidata);
            foreach (Match match in matches)
            {
                decimal bpm;
                if (decimal.TryParse(match.Groups["bpm"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out bpm) && bpm > 0m)
                {
                    yield return bpm;
                }
            }
        }

        private static string FormatDecimal(decimal value)
        {
            return value % 1m == 0m
                ? ((int)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    public sealed class OnlineStatsHydrator
    {
        public static readonly TimeSpan InteractionFreshness = TimeSpan.FromHours(24);

        private readonly ITextFetcher _textFetcher;
        private readonly string _baseUrl;
        private readonly HydrationStore _store;

        public OnlineStatsHydrator(ITextFetcher textFetcher, string baseUrl, HydrationStore store)
        {
            _textFetcher = textFetcher;
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://majdata.net" : baseUrl.TrimEnd('/');
            _store = store;
        }

        public HydrationStoreReadResult ReadCachedStats(CatalogRow row, DateTimeOffset now)
        {
            if (row == null)
            {
                throw new ArgumentNullException("row");
            }

            return _store.Read(row.Hash, HydrationDataKind.InteractionStats, now, InteractionFreshness);
        }

        public MajdataNetResult<InteractionFacet> HydrateOnlineStats(CatalogRow row, DateTimeOffset now)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.OnlineId))
            {
                return MajdataNetResult<InteractionFacet>.RecoverableFailure("Online chart id is missing.");
            }

            try
            {
                string json = _textFetcher.GetString(_baseUrl + "/api/maichart/" + row.OnlineId + "/interact");
                InteractionFacet facet = ParseInteractionStats(json, now);
                _store.Write(new HydrationCacheValue(row.Hash, HydrationDataKind.InteractionStats, SerializeInteraction(facet), now));
                return MajdataNetResult<InteractionFacet>.Ok(facet);
            }
            catch (Exception ex)
            {
                _store.RecordFailure(row.Hash, HydrationDataKind.InteractionStats, ex.Message);
                return MajdataNetResult<InteractionFacet>.RecoverableFailure(ex.Message);
            }
        }

        public InteractionFacet ParseInteractionStats(string json, DateTimeOffset fetchedAt)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return InteractionFacet.Empty();
            }

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(InteractionStatsDto));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                InteractionStatsDto dto = (InteractionStatsDto)serializer.ReadObject(stream);
                return new InteractionFacet(dto.PlayCount ?? dto.Play ?? dto.PlayedCount, dto.LikeCount ?? dto.Likes, dto.CommentCount ?? dto.Comments, fetchedAt);
            }
        }

        private static string SerializeInteraction(InteractionFacet facet)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "play={0};like={1};comment={2}",
                facet.OnlinePlayCount.HasValue ? facet.OnlinePlayCount.Value.ToString(CultureInfo.InvariantCulture) : "",
                facet.LikeCount.HasValue ? facet.LikeCount.Value.ToString(CultureInfo.InvariantCulture) : "",
                facet.CommentCount.HasValue ? facet.CommentCount.Value.ToString(CultureInfo.InvariantCulture) : "");
        }
    }

    [DataContract]
    internal sealed class InteractionStatsDto
    {
        [DataMember(Name = "playCount")]
        public int? PlayCount { get; set; }

        [DataMember(Name = "play")]
        public int? Play { get; set; }

        [DataMember(Name = "playedCount")]
        public int? PlayedCount { get; set; }

        [DataMember(Name = "likeCount")]
        public int? LikeCount { get; set; }

        [DataMember(Name = "likes")]
        public int? Likes { get; set; }

        [DataMember(Name = "commentCount")]
        public int? CommentCount { get; set; }

        [DataMember(Name = "comments")]
        public int? Comments { get; set; }
    }
}
