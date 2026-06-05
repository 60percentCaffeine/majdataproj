using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MajdataQolSongListMod.Core
{
    public enum CatalogSortMode
    {
        Default,
        DateAdded,
        Difficulty,
        NoteDesigner,
        Title,
        Rank,
        Artist,
        PlayCount,
        Bpm,
        ApFcRank,
        DxScore
    }

    public sealed class CatalogSortRequest
    {
        public CatalogSortRequest(CatalogSortMode sortMode, int selectedDifficultyIndex)
        {
            SortMode = sortMode;
            SelectedDifficultyIndex = selectedDifficultyIndex;
        }

        public CatalogSortMode SortMode { get; private set; }

        public int SelectedDifficultyIndex { get; private set; }

        public static CatalogSortRequest FromMapListSort(MapListSortMode sortMode, int selectedDifficultyIndex)
        {
            return new CatalogSortRequest(ToCatalogSortMode(sortMode), selectedDifficultyIndex);
        }

        private static CatalogSortMode ToCatalogSortMode(MapListSortMode sortMode)
        {
            switch (sortMode)
            {
                case MapListSortMode.DateAdded:
                    return CatalogSortMode.DateAdded;
                case MapListSortMode.Difficulty:
                    return CatalogSortMode.Difficulty;
                case MapListSortMode.NoteDesigner:
                    return CatalogSortMode.NoteDesigner;
                case MapListSortMode.Title:
                    return CatalogSortMode.Title;
                case MapListSortMode.Rank:
                    return CatalogSortMode.Rank;
                case MapListSortMode.Artist:
                    return CatalogSortMode.Artist;
                case MapListSortMode.PlayCount:
                    return CatalogSortMode.PlayCount;
                case MapListSortMode.Bpm:
                    return CatalogSortMode.Bpm;
                case MapListSortMode.ApFcRank:
                    return CatalogSortMode.ApFcRank;
                case MapListSortMode.DxScore:
                    return CatalogSortMode.DxScore;
                default:
                    return CatalogSortMode.Default;
            }
        }
    }

    public sealed class CatalogSorter
    {
        public IReadOnlyList<CatalogRow> Sort(IEnumerable<CatalogRow> rows, CatalogSortRequest request)
        {
            if (rows == null)
            {
                throw new ArgumentNullException("rows");
            }

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            Comparison<CatalogRow> comparison = ComparisonFor(request);
            return rows.OrderBy(row => row, Comparer<CatalogRow>.Create(comparison)).ToArray();
        }

        private static Comparison<CatalogRow> ComparisonFor(CatalogSortRequest request)
        {
            switch (request.SortMode)
            {
                case CatalogSortMode.DateAdded:
                    return (left, right) => CompareNullableDescending(left.Timestamp, right.Timestamp, FinalTieBreak(left, right));
                case CatalogSortMode.Difficulty:
                    return (left, right) => CompareNullableAscending(LevelSortValue(left, request.SelectedDifficultyIndex), LevelSortValue(right, request.SelectedDifficultyIndex), FinalTieBreak(left, right));
                case CatalogSortMode.NoteDesigner:
                    return (left, right) => CompareText(FirstDesigner(left), FirstDesigner(right), FinalTieBreak(left, right));
                case CatalogSortMode.Title:
                    return (left, right) => CompareText(left.Title, right.Title, FinalTieBreak(left, right));
                case CatalogSortMode.Rank:
                    return (left, right) => CompareNullableDescending(RankValue(left.Score.Rank), RankValue(right.Score.Rank), FinalTieBreak(left, right));
                case CatalogSortMode.Artist:
                    return (left, right) => CompareText(left.Artist, right.Artist, FinalTieBreak(left, right));
                case CatalogSortMode.PlayCount:
                    return (left, right) => CompareNullableDescending(PlayCount(left), PlayCount(right), FinalTieBreak(left, right));
                case CatalogSortMode.Bpm:
                    return (left, right) => CompareNullableAscending(left.Bpm.SortValue, right.Bpm.SortValue, FinalTieBreak(left, right));
                case CatalogSortMode.ApFcRank:
                    return (left, right) => CompareNullableDescending(ApFcValue(left), ApFcValue(right), FinalTieBreak(left, right));
                case CatalogSortMode.DxScore:
                    return (left, right) => CompareNullableDescending(left.Score.DxScore, right.Score.DxScore, FinalTieBreak(left, right));
                default:
                    return FinalTieBreak;
            }
        }

        private static int FinalTieBreak(CatalogRow left, CatalogRow right)
        {
            int title = StringComparer.OrdinalIgnoreCase.Compare(left.Title ?? string.Empty, right.Title ?? string.Empty);
            if (title != 0)
            {
                return title;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.Hash ?? string.Empty, right.Hash ?? string.Empty);
        }

        private static int CompareText(string left, string right, int tieBreak)
        {
            bool leftKnown = !string.IsNullOrWhiteSpace(left);
            bool rightKnown = !string.IsNullOrWhiteSpace(right);
            if (leftKnown && !rightKnown)
            {
                return -1;
            }

            if (!leftKnown && rightKnown)
            {
                return 1;
            }

            if (!leftKnown && !rightKnown)
            {
                return tieBreak;
            }

            int compare = StringComparer.OrdinalIgnoreCase.Compare(left.Trim(), right.Trim());
            return compare != 0 ? compare : tieBreak;
        }

        private static int CompareNullableAscending<T>(T? left, T? right, int tieBreak)
            where T : struct, IComparable<T>
        {
            if (left.HasValue && !right.HasValue)
            {
                return -1;
            }

            if (!left.HasValue && right.HasValue)
            {
                return 1;
            }

            if (!left.HasValue && !right.HasValue)
            {
                return tieBreak;
            }

            int compare = left.Value.CompareTo(right.Value);
            return compare != 0 ? compare : tieBreak;
        }

        private static int CompareNullableDescending<T>(T? left, T? right, int tieBreak)
            where T : struct, IComparable<T>
        {
            if (left.HasValue && !right.HasValue)
            {
                return -1;
            }

            if (!left.HasValue && right.HasValue)
            {
                return 1;
            }

            if (!left.HasValue && !right.HasValue)
            {
                return tieBreak;
            }

            int compare = right.Value.CompareTo(left.Value);
            return compare != 0 ? compare : tieBreak;
        }

        private static string FirstDesigner(CatalogRow row)
        {
            return row.Designers.FirstOrDefault();
        }

        private static int? PlayCount(CatalogRow row)
        {
            if (row.Score.LocalPlayCount.HasValue)
            {
                return row.Score.LocalPlayCount.Value;
            }

            return row.Interaction.OnlinePlayCount;
        }

        private static decimal? LevelSortValue(CatalogRow row, int selectedDifficultyIndex)
        {
            CatalogLevel level = row.Levels.FirstOrDefault(candidate => candidate != null && candidate.DifficultyIndex == selectedDifficultyIndex);
            if (level == null || string.IsNullOrWhiteSpace(level.Value))
            {
                return null;
            }

            string text = level.Value.Trim();
            bool hasPlus = text.EndsWith("+", StringComparison.Ordinal);
            if (hasPlus)
            {
                text = text.Substring(0, text.Length - 1).Trim();
            }

            decimal value;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }

            return hasPlus ? value + 0.7m : value;
        }

        private static int? RankValue(string rank)
        {
            if (string.IsNullOrWhiteSpace(rank))
            {
                return null;
            }

            switch (rank.Trim().ToUpperInvariant())
            {
                case "SSS+":
                    return 12;
                case "SSS":
                    return 11;
                case "SS+":
                    return 10;
                case "SS":
                    return 9;
                case "S+":
                    return 8;
                case "S":
                    return 7;
                case "AAA":
                    return 6;
                case "AA":
                    return 5;
                case "A":
                    return 4;
                case "BBB":
                    return 3;
                case "BB":
                    return 2;
                case "B":
                    return 1;
                case "C":
                    return 0;
                default:
                    return null;
            }
        }

        private static int? ApFcValue(CatalogRow row)
        {
            if (row.Score.HasAllPerfect)
            {
                return 2;
            }

            if (row.Score.HasFullCombo)
            {
                return 1;
            }

            return null;
        }
    }
}
