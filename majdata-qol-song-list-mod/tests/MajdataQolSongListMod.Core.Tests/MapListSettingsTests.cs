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
                    "AP/FC Rank"
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
        public void StartupMessageIncludesDefaults()
        {
            QolSongListModLogic logic = new QolSongListModLogic();

            Assert.Equal(
                "Majdata QoL Song List core ready: difficultyFilter=No sorting=Default grouping=Default downloadedSongsFilter=Mixed",
                logic.StartupMessage());
        }
    }
}
