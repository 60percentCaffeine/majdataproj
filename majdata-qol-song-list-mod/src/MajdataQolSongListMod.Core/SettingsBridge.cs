using System;
using System.Collections.Generic;
using System.Linq;

namespace MajdataQolSongListMod.Core
{
    public sealed class MapListSettingCard
    {
        public MapListSettingCard(string propertyName, string label, string defaultValue, IEnumerable<string> values)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name is required.", "propertyName");
            }

            PropertyName = propertyName;
            Label = string.IsNullOrWhiteSpace(label) ? propertyName : label;
            DefaultValue = string.IsNullOrWhiteSpace(defaultValue) ? string.Empty : defaultValue;
            Description = Label + " Description";
            Values = (values ?? Enumerable.Empty<string>()).ToArray();
        }

        public string PropertyName { get; private set; }

        public string Label { get; private set; }

        public string DefaultValue { get; private set; }

        public string Description { get; private set; }

        public IReadOnlyList<string> Values { get; private set; }
    }

    public sealed class MapListSettingsGroup
    {
        public MapListSettingsGroup(IEnumerable<MapListSettingCard> cards)
        {
            Name = "Map List";
            Title = "Map List Settings";
            Cards = (cards ?? Enumerable.Empty<MapListSettingCard>()).ToArray();
        }

        public string Name { get; private set; }

        public string Title { get; private set; }

        public IReadOnlyList<MapListSettingCard> Cards { get; private set; }
    }

    public static class MapListSettingsBridge
    {
        public static MapListSettingsGroup BuildGroup()
        {
            return new MapListSettingsGroup(new[]
            {
                new MapListSettingCard(
                    "DifficultyFilter",
                    "Difficulty Filter",
                    MapListOptionLabels.For(DifficultyCountFilter.No),
                    MapListOptionCatalog.GetDifficultyFilters().Select(option => option.Label)),
                new MapListSettingCard(
                    "Sorting",
                    "Sorting",
                    MapListOptionLabels.For(MapListSortMode.Default),
                    MapListOptionCatalog.GetSortModes().Select(option => option.Label)),
                new MapListSettingCard(
                    "Grouping",
                    "Grouping",
                    MapListOptionLabels.For(MapListGroupingMode.Default),
                    MapListOptionCatalog.GetGroupingModes().Select(option => option.Label)),
                new MapListSettingCard(
                    "DownloadedSongsFilter",
                    "Downloaded Songs Filter",
                    MapListOptionLabels.For(DownloadedSongsFilter.Mixed),
                    MapListOptionCatalog.GetDownloadedSongsFilters().Select(option => option.Label))
            });
        }

        public static IReadOnlyList<string> InsertMapListBeforeGame(IEnumerable<string> existingMenuNames)
        {
            List<string> result = new List<string>();
            bool inserted = false;

            foreach (string existing in existingMenuNames ?? Enumerable.Empty<string>())
            {
                if (!inserted && string.Equals(existing, "Game", StringComparison.Ordinal))
                {
                    result.Add("Map List");
                    inserted = true;
                }

                if (!string.Equals(existing, "Map List", StringComparison.Ordinal))
                {
                    result.Add(existing);
                }
            }

            if (!inserted)
            {
                result.Insert(0, "Map List");
            }

            return result.ToArray();
        }

        public static CatalogGroupingRequest BuildGroupingRequest(
            MapListSettings settings,
            int selectedDifficultyIndex,
            IEnumerable<CatalogCollection> existingFolderCollections)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            return new CatalogGroupingRequest(
                settings.Grouping,
                settings.DifficultyFilter,
                selectedDifficultyIndex,
                existingFolderCollections);
        }

        public static CatalogSortRequest BuildSortRequest(MapListSettings settings, int selectedDifficultyIndex)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            return CatalogSortRequest.FromMapListSort(settings.Sorting, selectedDifficultyIndex);
        }
    }
}
