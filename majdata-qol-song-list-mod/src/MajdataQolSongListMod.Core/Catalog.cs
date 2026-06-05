using System;
using System.Collections.Generic;
using System.Linq;

namespace MajdataQolSongListMod.Core
{
    [Flags]
    public enum CatalogSource
    {
        None = 0,
        Local = 1,
        Online = 2
    }

    public enum PreferredPlaybackSource
    {
        None,
        Local,
        Online
    }

    public enum HydrationState
    {
        Unknown,
        Pending,
        Fresh,
        Stale,
        Failed
    }

    public sealed class BpmFacet
    {
        public BpmFacet(decimal? minimum, decimal? maximum, HydrationState state)
        {
            Minimum = minimum;
            Maximum = maximum;
            State = state;
        }

        public decimal? Minimum { get; private set; }

        public decimal? Maximum { get; private set; }

        public HydrationState State { get; private set; }

        public bool HasKnownValue
        {
            get { return Minimum.HasValue && Maximum.HasValue; }
        }

        public decimal? SortValue
        {
            get { return HasKnownValue ? Minimum.Value : (decimal?)null; }
        }

        public static BpmFacet Unknown()
        {
            return new BpmFacet(null, null, HydrationState.Unknown);
        }

        public static BpmFacet Pending()
        {
            return new BpmFacet(null, null, HydrationState.Pending);
        }

        public static BpmFacet Known(decimal bpm)
        {
            return new BpmFacet(bpm, bpm, HydrationState.Fresh);
        }

        public static BpmFacet KnownRange(decimal minimum, decimal maximum)
        {
            return new BpmFacet(minimum, maximum, HydrationState.Fresh);
        }
    }

    public sealed class CatalogLevel
    {
        public CatalogLevel(int difficultyIndex, string difficultyName, string value)
        {
            DifficultyIndex = difficultyIndex;
            DifficultyName = difficultyName;
            Value = value;
        }

        public int DifficultyIndex { get; private set; }

        public string DifficultyName { get; private set; }

        public string Value { get; private set; }

        public bool HasUsableValue
        {
            get { return !string.IsNullOrWhiteSpace(Value); }
        }
    }

    public sealed class ScoreFacet
    {
        public ScoreFacet(string rank, int? localPlayCount, bool hasFullCombo, bool hasAllPerfect, int? dxScore)
        {
            Rank = rank;
            LocalPlayCount = localPlayCount;
            HasFullCombo = hasFullCombo;
            HasAllPerfect = hasAllPerfect;
            DxScore = dxScore;
        }

        public string Rank { get; private set; }

        public int? LocalPlayCount { get; private set; }

        public bool HasFullCombo { get; private set; }

        public bool HasAllPerfect { get; private set; }

        public int? DxScore { get; private set; }

        public static ScoreFacet Empty()
        {
            return new ScoreFacet(null, null, false, false, null);
        }
    }

    public sealed class InteractionFacet
    {
        public InteractionFacet(int? onlinePlayCount, int? likeCount, int? commentCount, DateTimeOffset? fetchedAt)
        {
            OnlinePlayCount = onlinePlayCount;
            LikeCount = likeCount;
            CommentCount = commentCount;
            FetchedAt = fetchedAt;
        }

        public int? OnlinePlayCount { get; private set; }

        public int? LikeCount { get; private set; }

        public int? CommentCount { get; private set; }

        public DateTimeOffset? FetchedAt { get; private set; }

        public static InteractionFacet Empty()
        {
            return new InteractionFacet(null, null, null, null);
        }
    }

    public sealed class CollectionMembership
    {
        public CollectionMembership(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; private set; }

        public string Name { get; private set; }
    }

    public sealed class CatalogInput
    {
        private CatalogInput(
            CatalogSource source,
            string hash,
            string title,
            string artist,
            string uploader,
            IEnumerable<string> designers,
            IEnumerable<CatalogLevel> levels,
            DateTimeOffset? timestamp,
            string onlineId,
            string localFolder,
            ScoreFacet score,
            InteractionFacet interaction,
            IEnumerable<CollectionMembership> collectionMemberships,
            BpmFacet bpm,
            HydrationState hydrationState)
        {
            Source = source;
            Hash = hash;
            Title = title;
            Artist = artist;
            Uploader = uploader;
            Designers = (designers ?? Enumerable.Empty<string>()).ToArray();
            Levels = (levels ?? Enumerable.Empty<CatalogLevel>()).ToArray();
            Timestamp = timestamp;
            OnlineId = onlineId;
            LocalFolder = localFolder;
            Score = score ?? ScoreFacet.Empty();
            Interaction = interaction ?? InteractionFacet.Empty();
            CollectionMemberships = (collectionMemberships ?? Enumerable.Empty<CollectionMembership>()).ToArray();
            Bpm = bpm ?? BpmFacet.Unknown();
            HydrationState = hydrationState;
        }

        public CatalogSource Source { get; private set; }

        public string Hash { get; private set; }

        public string Title { get; private set; }

        public string Artist { get; private set; }

        public string Uploader { get; private set; }

        public IReadOnlyList<string> Designers { get; private set; }

        public IReadOnlyList<CatalogLevel> Levels { get; private set; }

        public DateTimeOffset? Timestamp { get; private set; }

        public string OnlineId { get; private set; }

        public string LocalFolder { get; private set; }

        public ScoreFacet Score { get; private set; }

        public InteractionFacet Interaction { get; private set; }

        public IReadOnlyList<CollectionMembership> CollectionMemberships { get; private set; }

        public BpmFacet Bpm { get; private set; }

        public HydrationState HydrationState { get; private set; }

        public static CatalogInput Local(
            string hash,
            string title,
            string artist,
            string localFolder,
            IEnumerable<CatalogLevel> levels,
            IEnumerable<string> designers,
            DateTimeOffset? timestamp,
            ScoreFacet score,
            HydrationState hydrationState,
            BpmFacet bpm = null)
        {
            return new CatalogInput(
                CatalogSource.Local,
                hash,
                title,
                artist,
                null,
                designers,
                levels,
                timestamp,
                null,
                localFolder,
                score,
                null,
                null,
                bpm,
                hydrationState);
        }

        public static CatalogInput Online(
            string hash,
            string onlineId,
            string title,
            string artist,
            string uploader,
            IEnumerable<CatalogLevel> levels,
            IEnumerable<string> designers,
            DateTimeOffset? timestamp,
            InteractionFacet interaction,
            IEnumerable<CollectionMembership> collectionMemberships,
            HydrationState hydrationState,
            BpmFacet bpm = null)
        {
            return new CatalogInput(
                CatalogSource.Online,
                hash,
                title,
                artist,
                uploader,
                designers,
                levels,
                timestamp,
                onlineId,
                null,
                null,
                interaction,
                collectionMemberships,
                bpm,
                hydrationState);
        }
    }

    public sealed class CatalogRow
    {
        internal CatalogRow(
            string hash,
            string title,
            string artist,
            string uploader,
            IEnumerable<string> designers,
            IEnumerable<CatalogLevel> levels,
            DateTimeOffset? timestamp,
            CatalogSource source,
            PreferredPlaybackSource preferredPlaybackSource,
            string onlineId,
            string localFolder,
            ScoreFacet score,
            InteractionFacet interaction,
            IEnumerable<CollectionMembership> collectionMemberships,
            BpmFacet bpm,
            HydrationState hydrationState)
        {
            Hash = hash;
            Title = title;
            Artist = artist;
            Uploader = uploader;
            Designers = (designers ?? Enumerable.Empty<string>()).ToArray();
            Levels = (levels ?? Enumerable.Empty<CatalogLevel>()).ToArray();
            Timestamp = timestamp;
            Source = source;
            PreferredPlaybackSource = preferredPlaybackSource;
            OnlineId = onlineId;
            LocalFolder = localFolder;
            Score = score ?? ScoreFacet.Empty();
            Interaction = interaction ?? InteractionFacet.Empty();
            CollectionMemberships = (collectionMemberships ?? Enumerable.Empty<CollectionMembership>()).ToArray();
            Bpm = bpm ?? BpmFacet.Unknown();
            HydrationState = hydrationState;
        }

        public string Hash { get; private set; }

        public string Title { get; private set; }

        public string Artist { get; private set; }

        public string Uploader { get; private set; }

        public IReadOnlyList<string> Designers { get; private set; }

        public IReadOnlyList<CatalogLevel> Levels { get; private set; }

        public DateTimeOffset? Timestamp { get; private set; }

        public CatalogSource Source { get; private set; }

        public PreferredPlaybackSource PreferredPlaybackSource { get; private set; }

        public string OnlineId { get; private set; }

        public string LocalFolder { get; private set; }

        public ScoreFacet Score { get; private set; }

        public InteractionFacet Interaction { get; private set; }

        public IReadOnlyList<CollectionMembership> CollectionMemberships { get; private set; }

        public BpmFacet Bpm { get; private set; }

        public HydrationState HydrationState { get; private set; }

        public bool HasLocalAsset
        {
            get { return (Source & CatalogSource.Local) == CatalogSource.Local; }
        }

        public bool HasOnlineMetadata
        {
            get { return (Source & CatalogSource.Online) == CatalogSource.Online; }
        }
    }

    public sealed class CatalogIndex
    {
        private readonly IReadOnlyList<CatalogRow> _rows;

        private CatalogIndex(IEnumerable<CatalogRow> rows)
        {
            _rows = rows.OrderBy(row => row.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Hash ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<CatalogRow> Rows
        {
            get { return _rows; }
        }

        public static CatalogIndex Build(IEnumerable<CatalogInput> inputs)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException("inputs");
            }

            Dictionary<string, CatalogInputAccumulator> byHash = new Dictionary<string, CatalogInputAccumulator>(StringComparer.OrdinalIgnoreCase);
            foreach (CatalogInput input in inputs)
            {
                if (input == null)
                {
                    continue;
                }

                string normalizedHash = NormalizeHash(input.Hash);
                CatalogInputAccumulator accumulator;
                if (!byHash.TryGetValue(normalizedHash, out accumulator))
                {
                    accumulator = new CatalogInputAccumulator(normalizedHash);
                    byHash.Add(normalizedHash, accumulator);
                }

                accumulator.Add(input);
            }

            return new CatalogIndex(byHash.Values.Select(value => value.ToRow()));
        }

        private static string NormalizeHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return string.Empty;
            }

            return hash.Trim();
        }

        private sealed class CatalogInputAccumulator
        {
            private readonly string _hash;
            private CatalogInput _local;
            private CatalogInput _online;
            private readonly List<CatalogInput> _inputs = new List<CatalogInput>();

            public CatalogInputAccumulator(string hash)
            {
                _hash = hash;
            }

            public void Add(CatalogInput input)
            {
                _inputs.Add(input);
                if ((input.Source & CatalogSource.Local) == CatalogSource.Local && _local == null)
                {
                    _local = input;
                }

                if ((input.Source & CatalogSource.Online) == CatalogSource.Online && _online == null)
                {
                    _online = input;
                }
            }

            public CatalogRow ToRow()
            {
                CatalogInput display = PreferTextInput();
                CatalogSource source = CatalogSource.None;
                foreach (CatalogInput input in _inputs)
                {
                    source |= input.Source;
                }

                PreferredPlaybackSource preferredPlaybackSource = (source & CatalogSource.Local) == CatalogSource.Local
                    ? PreferredPlaybackSource.Local
                    : ((source & CatalogSource.Online) == CatalogSource.Online ? PreferredPlaybackSource.Online : PreferredPlaybackSource.None);

                return new CatalogRow(
                    _hash,
                    FirstNonBlank(_online != null ? _online.Title : null, _local != null ? _local.Title : null, display.Title),
                    FirstNonBlank(_online != null ? _online.Artist : null, _local != null ? _local.Artist : null, display.Artist),
                    FirstNonBlank(_online != null ? _online.Uploader : null, display.Uploader),
                    MergeStrings(_online != null ? _online.Designers : null, _local != null ? _local.Designers : null, display.Designers),
                    PreferLevels(_local, _online, display),
                    FirstValue(_online != null ? _online.Timestamp : null, _local != null ? _local.Timestamp : null, display.Timestamp),
                    source,
                    preferredPlaybackSource,
                    FirstNonBlank(_online != null ? _online.OnlineId : null, display.OnlineId),
                    FirstNonBlank(_local != null ? _local.LocalFolder : null, display.LocalFolder),
                    _local != null ? _local.Score : display.Score,
                    _online != null ? _online.Interaction : display.Interaction,
                    MergeMemberships(),
                    FirstBpm(_local, _online, display),
                    MostAdvancedHydrationState());
            }

            private CatalogInput PreferTextInput()
            {
                if (_online != null)
                {
                    return _online;
                }

                if (_local != null)
                {
                    return _local;
                }

                return _inputs[0];
            }

            private HydrationState MostAdvancedHydrationState()
            {
                HydrationState result = HydrationState.Unknown;
                foreach (CatalogInput input in _inputs)
                {
                    if (Rank(input.HydrationState) > Rank(result))
                    {
                        result = input.HydrationState;
                    }
                }

                return result;
            }

            private static int Rank(HydrationState state)
            {
                switch (state)
                {
                    case HydrationState.Fresh:
                        return 4;
                    case HydrationState.Stale:
                        return 3;
                    case HydrationState.Pending:
                        return 2;
                    case HydrationState.Failed:
                        return 1;
                    default:
                        return 0;
                }
            }

            private IReadOnlyList<CollectionMembership> MergeMemberships()
            {
                Dictionary<string, CollectionMembership> memberships = new Dictionary<string, CollectionMembership>(StringComparer.OrdinalIgnoreCase);
                foreach (CatalogInput input in _inputs)
                {
                    foreach (CollectionMembership membership in input.CollectionMemberships)
                    {
                        string key = FirstNonBlank(membership.Id, membership.Name);
                        if (!memberships.ContainsKey(key))
                        {
                            memberships.Add(key, membership);
                        }
                    }
                }

                return memberships.Values.ToArray();
            }

            private static IReadOnlyList<CatalogLevel> PreferLevels(CatalogInput local, CatalogInput online, CatalogInput fallback)
            {
                if (local != null && local.Levels.Count > 0)
                {
                    return local.Levels;
                }

                if (online != null && online.Levels.Count > 0)
                {
                    return online.Levels;
                }

                return fallback.Levels;
            }

            private static BpmFacet FirstBpm(CatalogInput local, CatalogInput online, CatalogInput fallback)
            {
                if (local != null && local.Bpm.HasKnownValue)
                {
                    return local.Bpm;
                }

                if (online != null && online.Bpm.HasKnownValue)
                {
                    return online.Bpm;
                }

                if (local != null && local.Bpm.State != HydrationState.Unknown)
                {
                    return local.Bpm;
                }

                if (online != null && online.Bpm.State != HydrationState.Unknown)
                {
                    return online.Bpm;
                }

                return fallback.Bpm;
            }

            private static DateTimeOffset? FirstValue(params DateTimeOffset?[] values)
            {
                foreach (DateTimeOffset? value in values)
                {
                    if (value.HasValue)
                    {
                        return value;
                    }
                }

                return null;
            }

            private static string FirstNonBlank(params string[] values)
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return null;
            }

            private static IReadOnlyList<string> MergeStrings(params IEnumerable<string>[] sources)
            {
                List<string> result = new List<string>();
                foreach (IEnumerable<string> source in sources)
                {
                    if (source == null)
                    {
                        continue;
                    }

                    foreach (string value in source)
                    {
                        if (!string.IsNullOrWhiteSpace(value) && !result.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
                        {
                            result.Add(value);
                        }
                    }
                }

                return result.ToArray();
            }
        }
    }
}
