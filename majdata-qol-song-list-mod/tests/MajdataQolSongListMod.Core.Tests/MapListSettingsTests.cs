using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class MapListSettingsTests
    {
        [Fact]
        public void DefaultsMatchPrdAndUiProposal()
        {
            MapListSettings settings = MapListSettings.Defaults();

            Assert.Equal(DifficultyCountFilter.No, settings.DifficultyFilter);
            Assert.Equal(MapListSortMode.Default, settings.Sorting);
            Assert.Equal(MapListGroupingMode.Default, settings.Grouping);
            Assert.Equal(DownloadedSongsFilter.Mixed, settings.DownloadedSongsFilter);
            Assert.Equal("No", MapListOptionLabels.For(settings.DifficultyFilter));
            Assert.Equal("Default", MapListOptionLabels.For(settings.Sorting));
            Assert.Equal("Default", MapListOptionLabels.For(settings.Grouping));
            Assert.Equal("Mixed", MapListOptionLabels.For(settings.DownloadedSongsFilter));
        }

        [Fact]
        public void DifficultyFilterOptionsMatchProposalOrder()
        {
            string[] labels = MapListOptionCatalog.GetDifficultyFilters().Select(option => option.Label).ToArray();

            Assert.Equal(new[] { "No", ">1 difficulty", ">2 difficulties", ">3 difficulties" }, labels);
        }

        [Fact]
        public void SortOptionsMatchFirstImplementationSurface()
        {
            string[] labels = MapListOptionCatalog.GetSortModes().Select(option => option.Label).ToArray();

            Assert.Equal(
                new[]
                {
                    "Default",
                    "Date Added",
                    "Difficulty",
                    "Note Designer",
                    "Title",
                    "Rank",
                    "Artist",
                    "Play Count",
                    "BPM",
                    "AP/FC Rank",
                    "DX Score"
                },
                labels);
        }

        [Fact]
        public void GroupingOptionsOmitVersionAndAll()
        {
            string[] labels = MapListOptionCatalog.GetGroupingModes().Select(option => option.Label).ToArray();

            Assert.Equal(
                new[]
                {
                    "Default",
                    "Difficulty Bracket",
                    "Difficulty Level",
                    "Title",
                    "Artist",
                    "Rank"
                },
                labels);
            Assert.False(MapListOptionCatalog.HasGroupingLabel("Version"));
            Assert.False(MapListOptionCatalog.HasGroupingLabel("All"));
        }

        [Fact]
        public void DownloadedSongsFilterOptionsMatchProposalOrder()
        {
            string[] labels = MapListOptionCatalog.GetDownloadedSongsFilters().Select(option => option.Label).ToArray();

            Assert.Equal(new[] { "Mixed", "Downloaded only", "Online only" }, labels);
        }

        [Fact]
        public void SettingsBridgeBuildsMapListGroupCardsWithPrdDefaults()
        {
            MapListSettingsGroup group = MapListSettingsBridge.BuildGroup();

            Assert.Equal("Map List", group.Name);
            Assert.Equal("Map List Settings", group.Title);
            Assert.Equal(
                new[] { "Difficulty Filter", "Sorting", "Grouping", "Downloaded Songs Filter" },
                group.Cards.Select(card => card.Label).ToArray());
            Assert.Equal(new[] { "No", "Default", "Default", "Mixed" }, group.Cards.Select(card => card.DefaultValue).ToArray());
            Assert.Equal("Difficulty Filter Description", group.Cards[0].Description);
        }

        [Fact]
        public void SettingsBridgeInsertsMapListBeforeGame()
        {
            string[] ordered = MapListSettingsBridge.InsertMapListBeforeGame(new[] { "Game", "Judge", "Display" }).ToArray();

            Assert.Equal(new[] { "Map List", "Game", "Judge", "Display" }, ordered);
        }

        [Fact]
        public void SettingsBridgeDoesNotDuplicateMapList()
        {
            string[] ordered = MapListSettingsBridge.InsertMapListBeforeGame(new[] { "Map List", "Game", "Judge" }).ToArray();

            Assert.Equal(new[] { "Map List", "Game", "Judge" }, ordered);
        }

        [Fact]
        public void SettingsBridgeBuildsGroupingAndSortRequestsFromSettings()
        {
            MapListSettings settings = new MapListSettings(
                DifficultyCountFilter.MoreThan2,
                MapListSortMode.Bpm,
                MapListGroupingMode.DifficultyLevel,
                DownloadedSongsFilter.DownloadedOnly);

            CatalogGroupingRequest grouping = MapListSettingsBridge.BuildGroupingRequest(settings, 4, new[] { new CatalogCollection("All", new CatalogRow[0]) });
            CatalogSortRequest sorting = MapListSettingsBridge.BuildSortRequest(settings, 4);

            Assert.Equal(MapListGroupingMode.DifficultyLevel, grouping.GroupingMode);
            Assert.Equal(DifficultyCountFilter.MoreThan2, grouping.DifficultyFilter);
            Assert.Equal(4, grouping.SelectedDifficultyIndex);
            Assert.Equal(CatalogSortMode.Bpm, sorting.SortMode);
            Assert.Equal(4, sorting.SelectedDifficultyIndex);
        }

        [Fact]
        public void StartupMessageIncludesDefaults()
        {
            QolSongListModLogic logic = new QolSongListModLogic();

            Assert.Equal(
                "Majdata QoL Song List core ready: difficultyFilter=No sorting=Default grouping=Default downloadedSongsFilter=Mixed",
                logic.StartupMessage());
        }

        [Fact]
        public void StartupDiagnosticsReportGuestActivePlayerSession()
        {
            QolSongListModLogic logic = new QolSongListModLogic();

            string diagnostics = logic.StartupDiagnostics();

            Assert.Contains("activePlayerMode=Guest", diagnostics);
            Assert.Contains("saveTargetKind=Guest", diagnostics);
        }
    }
}
