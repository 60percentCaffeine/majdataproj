using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MajdataQolSongListMod.Core
{
    public static class LevelBucketizer
    {
        public const string Other = "Other";

        public static string Bucketize(string level)
        {
            if (string.IsNullOrWhiteSpace(level))
            {
                return Other;
            }

            string text = level.Trim();
            bool hasPlus = text.EndsWith("+", StringComparison.Ordinal);
            if (hasPlus)
            {
                text = text.Substring(0, text.Length - 1).Trim();
            }

            decimal value;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) || value < 0m)
            {
                return Other;
            }

            int floor = (int)Math.Floor(value);
            if (hasPlus)
            {
                return floor.ToString(CultureInfo.InvariantCulture) + "+";
            }

            if (value == floor)
            {
                return floor.ToString(CultureInfo.InvariantCulture);
            }

            return floor.ToString(CultureInfo.InvariantCulture) + "+";
        }
    }

    public static class DifficultyCount
    {
        public static int CountUsableDifficulties(CatalogRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException("row");
            }

            return row.Levels.Count(level => level != null && level.HasUsableValue);
        }

        public static bool Passes(CatalogRow row, DifficultyCountFilter filter)
        {
            int count = CountUsableDifficulties(row);
            switch (filter)
            {
                case DifficultyCountFilter.MoreThan1:
                    return count > 1;
                case DifficultyCountFilter.MoreThan2:
                    return count > 2;
                case DifficultyCountFilter.MoreThan3:
                    return count > 3;
                default:
                    return true;
            }
        }
    }

    public sealed class CatalogCollection
    {
        public CatalogCollection(string name, IEnumerable<CatalogRow> rows)
        {
            Name = name;
            Rows = (rows ?? Enumerable.Empty<CatalogRow>()).ToArray();
        }

        public string Name { get; private set; }

        public IReadOnlyList<CatalogRow> Rows { get; private set; }
    }

    public sealed class CatalogGroupingRequest
    {
        public CatalogGroupingRequest(
            MapListGroupingMode groupingMode,
            DifficultyCountFilter difficultyFilter,
            int selectedDifficultyIndex,
            IEnumerable<CatalogCollection> existingFolderCollections)
        {
            GroupingMode = groupingMode;
            DifficultyFilter = difficultyFilter;
            SelectedDifficultyIndex = selectedDifficultyIndex;
            ExistingFolderCollections = existingFolderCollections as IReadOnlyList<CatalogCollection>
                ?? (existingFolderCollections ?? Enumerable.Empty<CatalogCollection>()).ToArray();
        }

        public MapListGroupingMode GroupingMode { get; private set; }

        public DifficultyCountFilter DifficultyFilter { get; private set; }

        public int SelectedDifficultyIndex { get; private set; }

        public IReadOnlyList<CatalogCollection> ExistingFolderCollections { get; private set; }

        public static CatalogGroupingRequest DefaultFolders(IEnumerable<CatalogCollection> existingFolderCollections)
        {
            return new CatalogGroupingRequest(MapListGroupingMode.Default, DifficultyCountFilter.No, 0, existingFolderCollections);
        }
    }

    public sealed class CatalogNavigator
    {
        private static readonly string[] DifficultyFolderOrder =
        {
            "Easy",
            "Basic",
            "Advance",
            "Expert",
            "Master",
            "ReMaster",
            "UTAGE",
            LevelBucketizer.Other
        };

        private readonly IReadOnlyList<CatalogRow> _rows;

        public CatalogNavigator(CatalogIndex index)
        {
            if (index == null)
            {
                throw new ArgumentNullException("index");
            }

            _rows = index.Rows;
        }

        public IReadOnlyList<CatalogCollection> BuildCollections(CatalogGroupingRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.GroupingMode == MapListGroupingMode.Default)
            {
                return request.ExistingFolderCollections;
            }

            IReadOnlyList<CatalogRow> filteredRows = _rows.Where(row => DifficultyCount.Passes(row, request.DifficultyFilter)).ToArray();
            switch (request.GroupingMode)
            {
                case MapListGroupingMode.DifficultyBracket:
                    return GroupByDifficulty(filteredRows);
                case MapListGroupingMode.DifficultyLevel:
                    return GroupByLevel(filteredRows, request.SelectedDifficultyIndex);
                case MapListGroupingMode.Artist:
                    return GroupBySingleKey(filteredRows, row => string.IsNullOrWhiteSpace(row.Artist) ? "Unknown Artist" : row.Artist.Trim());
                case MapListGroupingMode.Title:
                    return GroupBySingleKey(filteredRows, row => TitleBucket(row.Title));
                case MapListGroupingMode.Rank:
                    return GroupBySingleKey(filteredRows, row => string.IsNullOrWhiteSpace(row.Score.Rank) ? "No Play" : row.Score.Rank.Trim());
                default:
                    return request.ExistingFolderCollections;
            }
        }

        private static IReadOnlyList<CatalogCollection> GroupByDifficulty(IEnumerable<CatalogRow> rows)
        {
            Dictionary<string, List<CatalogRow>> groups = CreateGroupMap(DifficultyFolderOrder);
            foreach (CatalogRow row in rows)
            {
                CatalogLevel[] usableLevels = row.Levels.Where(level => level != null && level.HasUsableValue).ToArray();
                if (usableLevels.Length == 0)
                {
                    groups[LevelBucketizer.Other].Add(row);
                    continue;
                }

                HashSet<string> rowFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (CatalogLevel level in usableLevels)
                {
                    rowFolders.Add(DifficultyFolderName(level));
                }

                foreach (string folder in rowFolders)
                {
                    EnsureGroup(groups, folder).Add(row);
                }
            }

            return MaterializeOrdered(groups, DifficultyFolderOrder);
        }

        private static IReadOnlyList<CatalogCollection> GroupByLevel(IEnumerable<CatalogRow> rows, int selectedDifficultyIndex)
        {
            return GroupBySingleKey(rows, row =>
            {
                CatalogLevel level = row.Levels.FirstOrDefault(candidate => candidate != null && candidate.DifficultyIndex == selectedDifficultyIndex);
                return level == null ? LevelBucketizer.Other : LevelBucketizer.Bucketize(level.Value);
            });
        }

        private static IReadOnlyList<CatalogCollection> GroupBySingleKey(IEnumerable<CatalogRow> rows, Func<CatalogRow, string> keySelector)
        {
            Dictionary<string, List<CatalogRow>> groups = new Dictionary<string, List<CatalogRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (CatalogRow row in rows)
            {
                string key = keySelector(row);
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = LevelBucketizer.Other;
                }

                EnsureGroup(groups, key).Add(row);
            }

            return groups.OrderBy(pair => SortKey(pair.Key), StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new CatalogCollection(pair.Key, SortRows(pair.Value)))
                .ToArray();
        }

        private static string DifficultyFolderName(CatalogLevel level)
        {
            string name = level.DifficultyName == null ? string.Empty : level.DifficultyName.Trim();
            foreach (string known in DifficultyFolderOrder)
            {
                if (string.Equals(known, name, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }

            switch (level.DifficultyIndex)
            {
                case 0:
                    return "Easy";
                case 1:
                    return "Basic";
                case 2:
                    return "Advance";
                case 3:
                    return "Expert";
                case 4:
                    return "Master";
                case 5:
                    return "ReMaster";
                case 6:
                    return "UTAGE";
                default:
                    return LevelBucketizer.Other;
            }
        }

        private static string TitleBucket(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return LevelBucketizer.Other;
            }

            char first = char.ToUpperInvariant(title.Trim()[0]);
            if (first >= 'A' && first <= 'Z')
            {
                return first.ToString();
            }

            if (char.IsDigit(first))
            {
                return "#";
            }

            return LevelBucketizer.Other;
        }

        private static Dictionary<string, List<CatalogRow>> CreateGroupMap(IEnumerable<string> names)
        {
            Dictionary<string, List<CatalogRow>> groups = new Dictionary<string, List<CatalogRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                groups[name] = new List<CatalogRow>();
            }

            return groups;
        }

        private static List<CatalogRow> EnsureGroup(Dictionary<string, List<CatalogRow>> groups, string name)
        {
            List<CatalogRow> rows;
            if (!groups.TryGetValue(name, out rows))
            {
                rows = new List<CatalogRow>();
                groups.Add(name, rows);
            }

            return rows;
        }

        private static IReadOnlyList<CatalogCollection> MaterializeOrdered(Dictionary<string, List<CatalogRow>> groups, IEnumerable<string> orderedNames)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<CatalogCollection> result = new List<CatalogCollection>();
            foreach (string name in orderedNames)
            {
                seen.Add(name);
                List<CatalogRow> rows;
                if (groups.TryGetValue(name, out rows) && rows.Count > 0)
                {
                    result.Add(new CatalogCollection(name, SortRows(rows)));
                }
            }

            foreach (KeyValuePair<string, List<CatalogRow>> pair in groups.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!seen.Contains(pair.Key) && pair.Value.Count > 0)
                {
                    result.Add(new CatalogCollection(pair.Key, SortRows(pair.Value)));
                }
            }

            return result;
        }

        private static IReadOnlyList<CatalogRow> SortRows(IEnumerable<CatalogRow> rows)
        {
            return rows.OrderBy(row => row.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Hash ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string SortKey(string key)
        {
            if (key == LevelBucketizer.Other || key == "Unknown Artist" || key == "No Play")
            {
                return "zzzz_" + key;
            }

            if (key == "#")
            {
                return "0000";
            }

            return key;
        }
    }
}
