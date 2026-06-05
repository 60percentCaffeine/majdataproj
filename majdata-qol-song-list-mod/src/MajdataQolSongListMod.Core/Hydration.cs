using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MajdataQolSongListMod.Core
{
    public enum HydrationDataKind
    {
        Bpm,
        InteractionStats
    }

    public enum HydrationSceneState
    {
        Title,
        Menu,
        List,
        Setting,
        Login,
        Gameplay,
        Practice
    }

    public sealed class HydrationCacheValue
    {
        public HydrationCacheValue(string key, HydrationDataKind kind, string payload, DateTimeOffset fetchedAt)
        {
            Key = key;
            Kind = kind;
            Payload = payload;
            FetchedAt = fetchedAt;
        }

        public string Key { get; private set; }

        public HydrationDataKind Kind { get; private set; }

        public string Payload { get; private set; }

        public DateTimeOffset FetchedAt { get; private set; }

        public bool IsFresh(DateTimeOffset now, TimeSpan freshness)
        {
            return now - FetchedAt <= freshness;
        }
    }

    public sealed class HydrationStoreReadResult
    {
        public HydrationStoreReadResult(HydrationCacheValue value, bool shouldRefresh)
        {
            Value = value;
            ShouldRefresh = shouldRefresh;
        }

        public HydrationCacheValue Value { get; private set; }

        public bool ShouldRefresh { get; private set; }
    }

    public sealed class HydrationStore
    {
        public const string ModCacheDirectoryName = "MajdataQolSongListMod";

        private readonly string _cacheDirectory;

        public HydrationStore(string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                throw new ArgumentException("Cache root is required.", "cacheRoot");
            }

            _cacheDirectory = Path.Combine(cacheRoot, ModCacheDirectoryName);
            Directory.CreateDirectory(_cacheDirectory);
        }

        public string CacheDirectory
        {
            get { return _cacheDirectory; }
        }

        public void Write(HydrationCacheValue value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            string text = string.Join(
                Environment.NewLine,
                new[]
                {
                    value.Key ?? string.Empty,
                    value.Kind.ToString(),
                    value.FetchedAt.ToString("O", CultureInfo.InvariantCulture),
                    value.Payload ?? string.Empty
                });
            File.WriteAllText(PathFor(value.Key, value.Kind), text);
        }

        public HydrationStoreReadResult Read(string key, HydrationDataKind kind, DateTimeOffset now, TimeSpan freshness)
        {
            string path = PathFor(key, kind);
            if (!File.Exists(path))
            {
                return new HydrationStoreReadResult(null, true);
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 4)
            {
                return new HydrationStoreReadResult(null, true);
            }

            DateTimeOffset fetchedAt;
            if (!DateTimeOffset.TryParse(lines[2], null, DateTimeStyles.RoundtripKind, out fetchedAt))
            {
                return new HydrationStoreReadResult(null, true);
            }

            HydrationCacheValue value = new HydrationCacheValue(lines[0], kind, lines[3], fetchedAt);
            return new HydrationStoreReadResult(value, !value.IsFresh(now, freshness));
        }

        public void RecordFailure(string key, HydrationDataKind kind, string message)
        {
            string path = Path.Combine(_cacheDirectory, Sanitize(key) + "." + kind + ".failure.txt");
            File.WriteAllText(path, message ?? string.Empty);
        }

        private string PathFor(string key, HydrationDataKind kind)
        {
            return Path.Combine(_cacheDirectory, Sanitize(key) + "." + kind + ".cache");
        }

        private static string Sanitize(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "blank" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                text = text.Replace(c, '_');
            }

            return text.Replace('/', '_').Replace('\\', '_');
        }
    }

    public sealed class BrowsingContext
    {
        public BrowsingContext(IEnumerable<string> visibleHashes, IEnumerable<string> nearbyHashes, IEnumerable<string> specialOnlineHashes)
        {
            VisibleHashes = new HashSet<string>(visibleHashes ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            NearbyHashes = new HashSet<string>(nearbyHashes ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            SpecialOnlineHashes = new HashSet<string>(specialOnlineHashes ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public ISet<string> VisibleHashes { get; private set; }

        public ISet<string> NearbyHashes { get; private set; }

        public ISet<string> SpecialOnlineHashes { get; private set; }
    }

    public sealed class HydrationWorkItem
    {
        public HydrationWorkItem(CatalogRow row, HydrationDataKind kind, bool missing, bool stale)
        {
            Row = row;
            Kind = kind;
            Missing = missing;
            Stale = stale;
        }

        public CatalogRow Row { get; private set; }

        public HydrationDataKind Kind { get; private set; }

        public bool Missing { get; private set; }

        public bool Stale { get; private set; }
    }

    public sealed class HydrationScheduler
    {
        public bool AllowsHydration(HydrationSceneState sceneState)
        {
            return sceneState != HydrationSceneState.Gameplay && sceneState != HydrationSceneState.Practice;
        }

        public IReadOnlyList<HydrationWorkItem> OrderWork(
            IEnumerable<HydrationWorkItem> workItems,
            BrowsingContext browsingContext,
            HydrationSceneState sceneState)
        {
            if (!AllowsHydration(sceneState))
            {
                return new HydrationWorkItem[0];
            }

            BrowsingContext context = browsingContext ?? new BrowsingContext(null, null, null);
            return (workItems ?? Enumerable.Empty<HydrationWorkItem>())
                .OrderBy(item => MissingRank(item))
                .ThenBy(item => SourceRank(item.Row))
                .ThenBy(item => SpecialRank(item.Row, context))
                .ThenBy(item => VisibilityRank(item.Row, context))
                .ThenBy(item => item.Row.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Row.Hash ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int MissingRank(HydrationWorkItem item)
        {
            if (item.Missing)
            {
                return 0;
            }

            if (item.Stale)
            {
                return 1;
            }

            return 2;
        }

        private static int SourceRank(CatalogRow row)
        {
            return row.HasLocalAsset ? 0 : 1;
        }

        private static int SpecialRank(CatalogRow row, BrowsingContext context)
        {
            return context.SpecialOnlineHashes.Contains(row.Hash) ? 0 : 1;
        }

        private static int VisibilityRank(CatalogRow row, BrowsingContext context)
        {
            if (context.VisibleHashes.Contains(row.Hash))
            {
                return 0;
            }

            if (context.NearbyHashes.Contains(row.Hash))
            {
                return 1;
            }

            return 2;
        }
    }
}
