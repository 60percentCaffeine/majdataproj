using System;
using System.Collections.Generic;
using System.Linq;

namespace MajdataQolSongListMod.Core
{
    public enum WebsiteCollectionKind
    {
        UserOwned,
        Subscribed
    }

    public enum OnlineScope
    {
        Mixed,
        DownloadedOnly,
        OnlineOnly
    }

    public sealed class WebsiteCollectionSummary
    {
        public WebsiteCollectionSummary(string id, string name, string description, WebsiteCollectionKind kind, int? visibility, int? totalCount)
        {
            Id = id;
            Name = name;
            Description = description;
            Kind = kind;
            Visibility = visibility;
            TotalCount = totalCount;
        }

        public string Id { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public WebsiteCollectionKind Kind { get; private set; }

        public int? Visibility { get; private set; }

        public int? TotalCount { get; private set; }
    }

    public sealed class ResolvedWebsiteCollection
    {
        public ResolvedWebsiteCollection(WebsiteCollectionSummary summary, IEnumerable<CatalogRow> playableRows, int totalCount, int unresolvedCount)
        {
            Summary = summary;
            PlayableRows = (playableRows ?? Enumerable.Empty<CatalogRow>()).ToArray();
            TotalCount = totalCount;
            UnresolvedCount = unresolvedCount;
        }

        public WebsiteCollectionSummary Summary { get; private set; }

        public IReadOnlyList<CatalogRow> PlayableRows { get; private set; }

        public int TotalCount { get; private set; }

        public int ResolvedCount
        {
            get { return PlayableRows.Count; }
        }

        public int UnresolvedCount { get; private set; }

        public CatalogCollection ToCatalogCollection()
        {
            string name = string.IsNullOrWhiteSpace(Summary.Name) ? "Collection" : Summary.Name;
            string selectedInfo = string.Format("{0} Count:{1}/{2} resolved", name, ResolvedCount, TotalCount);
            return new CatalogCollection(name, name, selectedInfo, PlayableRows, true, true);
        }
    }

    public sealed class WebsiteCollectionResolver
    {
        private readonly Dictionary<string, CatalogRow> _rowsByHash;

        public WebsiteCollectionResolver(CatalogIndex catalogIndex)
        {
            if (catalogIndex == null)
            {
                throw new ArgumentNullException("catalogIndex");
            }

            _rowsByHash = new Dictionary<string, CatalogRow>(StringComparer.OrdinalIgnoreCase);
            foreach (CatalogRow row in catalogIndex.Rows)
            {
                string hash = NormalizeHash(row.Hash);
                if (!_rowsByHash.ContainsKey(hash))
                {
                    _rowsByHash.Add(hash, row);
                }
            }
        }

        public ResolvedWebsiteCollection Resolve(WebsiteCollectionSummary summary, IEnumerable<string> hashes, OnlineScope onlineScope)
        {
            if (summary == null)
            {
                throw new ArgumentNullException("summary");
            }

            string[] requestedHashes = (hashes ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            List<CatalogRow> playable = new List<CatalogRow>();
            HashSet<string> seenPlayableHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int unresolved = 0;

            foreach (string hash in requestedHashes)
            {
                CatalogRow row;
                if (!_rowsByHash.TryGetValue(NormalizeHash(hash), out row) || !AllowedByScope(row, onlineScope))
                {
                    unresolved++;
                    continue;
                }

                if (seenPlayableHashes.Add(row.Hash))
                {
                    playable.Add(row);
                }
            }

            int total = summary.TotalCount.HasValue && summary.TotalCount.Value > requestedHashes.Length
                ? summary.TotalCount.Value
                : requestedHashes.Length;
            if (summary.TotalCount.HasValue && summary.TotalCount.Value > requestedHashes.Length)
            {
                unresolved += summary.TotalCount.Value - requestedHashes.Length;
            }

            return new ResolvedWebsiteCollection(summary, playable, total, unresolved);
        }

        private static bool AllowedByScope(CatalogRow row, OnlineScope onlineScope)
        {
            switch (onlineScope)
            {
                case OnlineScope.DownloadedOnly:
                    return row.HasLocalAsset;
                case OnlineScope.OnlineOnly:
                    return row.HasOnlineMetadata && !row.HasLocalAsset;
                default:
                    return true;
            }
        }

        private static string NormalizeHash(string hash)
        {
            return string.IsNullOrWhiteSpace(hash) ? string.Empty : hash.Trim();
        }
    }
}
