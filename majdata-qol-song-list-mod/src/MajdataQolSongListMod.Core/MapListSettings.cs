using System;
using System.Collections.Generic;
using System.Linq;

namespace MajdataQolSongListMod.Core
{
    public enum DifficultyCountFilter
    {
        No,
        MoreThan1,
        MoreThan2,
        MoreThan3
    }

    public enum MapListSortMode
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

    public enum MapListGroupingMode
    {
        Default,
        DifficultyBracket,
        DifficultyLevel,
        Title,
        Artist,
        Rank
    }

    public enum DownloadedSongsFilter
    {
        Mixed,
        DownloadedOnly,
        OnlineOnly
    }

    public sealed class MapListSettings
    {
        public MapListSettings(
            DifficultyCountFilter difficultyFilter,
            MapListSortMode sorting,
            MapListGroupingMode grouping,
            DownloadedSongsFilter downloadedSongsFilter)
        {
            DifficultyFilter = difficultyFilter;
            Sorting = sorting;
            Grouping = grouping;
            DownloadedSongsFilter = downloadedSongsFilter;
        }

        public DifficultyCountFilter DifficultyFilter { get; private set; }

        public MapListSortMode Sorting { get; private set; }

        public MapListGroupingMode Grouping { get; private set; }

        public DownloadedSongsFilter DownloadedSongsFilter { get; private set; }

        public static MapListSettings Defaults()
        {
            return new MapListSettings(
                DifficultyCountFilter.No,
                MapListSortMode.Default,
                MapListGroupingMode.Default,
                DownloadedSongsFilter.Mixed);
        }

        public string Describe()
        {
            return string.Format(
                "difficultyFilter={0} sorting={1} grouping={2} downloadedSongsFilter={3}",
                MapListOptionLabels.For(DifficultyFilter),
                MapListOptionLabels.For(Sorting),
                MapListOptionLabels.For(Grouping),
                MapListOptionLabels.For(DownloadedSongsFilter));
        }
    }

    public sealed class MapListOption<T>
    {
        public MapListOption(T value, string label)
        {
            Value = value;
            Label = label;
        }

        public T Value { get; private set; }

        public string Label { get; private set; }
    }

    public static class MapListOptionCatalog
    {
        private static readonly MapListOption<DifficultyCountFilter>[] DifficultyFilters =
        {
            new MapListOption<DifficultyCountFilter>(DifficultyCountFilter.No, "No"),
            new MapListOption<DifficultyCountFilter>(DifficultyCountFilter.MoreThan1, ">1 difficulty"),
            new MapListOption<DifficultyCountFilter>(DifficultyCountFilter.MoreThan2, ">2 difficulties"),
            new MapListOption<DifficultyCountFilter>(DifficultyCountFilter.MoreThan3, ">3 difficulties")
        };

        private static readonly MapListOption<MapListSortMode>[] SortModes =
        {
            new MapListOption<MapListSortMode>(MapListSortMode.Default, "Default"),
            new MapListOption<MapListSortMode>(MapListSortMode.DateAdded, "Date Added"),
            new MapListOption<MapListSortMode>(MapListSortMode.Difficulty, "Difficulty"),
            new MapListOption<MapListSortMode>(MapListSortMode.NoteDesigner, "Note Designer"),
            new MapListOption<MapListSortMode>(MapListSortMode.Title, "Title"),
            new MapListOption<MapListSortMode>(MapListSortMode.Rank, "Rank"),
            new MapListOption<MapListSortMode>(MapListSortMode.Artist, "Artist"),
            new MapListOption<MapListSortMode>(MapListSortMode.PlayCount, "Play Count"),
            new MapListOption<MapListSortMode>(MapListSortMode.Bpm, "BPM"),
            new MapListOption<MapListSortMode>(MapListSortMode.ApFcRank, "AP/FC Rank"),
            new MapListOption<MapListSortMode>(MapListSortMode.DxScore, "DX Score")
        };

        private static readonly MapListOption<MapListGroupingMode>[] GroupingModes =
        {
            new MapListOption<MapListGroupingMode>(MapListGroupingMode.Default, "Default"),
            new MapListOption<MapListGroupingMode>(MapListGroupingMode.DifficultyBracket, "Difficulty Bracket"),
            new MapListOption<MapListGroupingMode>(MapListGroupingMode.DifficultyLevel, "Difficulty Level"),
            new MapListOption<MapListGroupingMode>(MapListGroupingMode.Title, "Title"),
            new MapListOption<MapListGroupingMode>(MapListGroupingMode.Artist, "Artist"),
            new MapListOption<MapListGroupingMode>(MapListGroupingMode.Rank, "Rank")
        };

        private static readonly MapListOption<DownloadedSongsFilter>[] DownloadedFilters =
        {
            new MapListOption<DownloadedSongsFilter>(DownloadedSongsFilter.Mixed, "Mixed"),
            new MapListOption<DownloadedSongsFilter>(DownloadedSongsFilter.DownloadedOnly, "Downloaded only"),
            new MapListOption<DownloadedSongsFilter>(DownloadedSongsFilter.OnlineOnly, "Online only")
        };

        public static IReadOnlyList<MapListOption<DifficultyCountFilter>> GetDifficultyFilters()
        {
            return DifficultyFilters;
        }

        public static IReadOnlyList<MapListOption<MapListSortMode>> GetSortModes()
        {
            return SortModes;
        }

        public static IReadOnlyList<MapListOption<MapListGroupingMode>> GetGroupingModes()
        {
            return GroupingModes;
        }

        public static IReadOnlyList<MapListOption<DownloadedSongsFilter>> GetDownloadedSongsFilters()
        {
            return DownloadedFilters;
        }

        public static bool HasGroupingLabel(string label)
        {
            return GroupingModes.Any(option => string.Equals(option.Label, label, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class MapListOptionLabels
    {
        public static string For(DifficultyCountFilter value)
        {
            return FindLabel(MapListOptionCatalog.GetDifficultyFilters(), value);
        }

        public static string For(MapListSortMode value)
        {
            return FindLabel(MapListOptionCatalog.GetSortModes(), value);
        }

        public static string For(MapListGroupingMode value)
        {
            return FindLabel(MapListOptionCatalog.GetGroupingModes(), value);
        }

        public static string For(DownloadedSongsFilter value)
        {
            return FindLabel(MapListOptionCatalog.GetDownloadedSongsFilters(), value);
        }

        private static string FindLabel<T>(IEnumerable<MapListOption<T>> options, T value)
        {
            foreach (MapListOption<T> option in options)
            {
                if (EqualityComparer<T>.Default.Equals(option.Value, value))
                {
                    return option.Label;
                }
            }

            throw new ArgumentOutOfRangeException("value", value, "Unsupported map-list option value.");
        }
    }
}
