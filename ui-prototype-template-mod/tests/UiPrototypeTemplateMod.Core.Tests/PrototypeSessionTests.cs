using System.Linq;
using UiPrototypeTemplateMod.Core;
using Xunit;

namespace UiPrototypeTemplateMod.Core.Tests
{
    public sealed class PrototypeSessionTests
    {
        [Fact]
        public void DefaultFakeDataIncludesCategoriesDifficultyAvailabilityScoresAndFlags()
        {
            PrototypeSession session = PrototypeSession.CreateDefault();

            Assert.True(session.Songs.Select(song => song.Category).Distinct().Count() >= 3);
            Assert.Contains(session.Songs, song => song.IsLong);
            Assert.Contains(session.Songs, song => song.IsSpecial);
            Assert.Contains(session.Songs.SelectMany(song => song.Difficulties), difficulty => !difficulty.Available);
            Assert.Contains(session.Songs.SelectMany(song => song.Difficulties), difficulty => difficulty.Locked);
            Assert.Contains(session.Songs.SelectMany(song => song.Difficulties), difficulty => difficulty.DxScore > 0 && difficulty.Rank != "--");
        }

        [Fact]
        public void SongNavigationWrapsAcrossSongList()
        {
            PrototypeSession session = PrototypeSession.CreateDefault();

            session.Apply(PrototypeAction.SongPrevious);
            Assert.Equal(session.Songs.Count - 1, session.SelectedSongIndex);

            session.Apply(PrototypeAction.SongNext);
            Assert.Equal(0, session.SelectedSongIndex);
        }

        [Fact]
        public void OkFromSongSelectionMovesToDifficultySelectionAndPreservesSong()
        {
            PrototypeSession session = PrototypeSession.CreateDefault();
            session.Apply(PrototypeAction.SongNext);
            int songIndex = session.SelectedSongIndex;

            session.Apply(PrototypeAction.Ok);

            Assert.Equal(PrototypePhase.DifficultySelect, session.Phase);
            Assert.Equal(songIndex, session.SelectedSongIndex);
        }

        [Fact]
        public void BackFromDifficultySelectionReturnsToSongSelectionWithSongPreserved()
        {
            PrototypeSession session = PrototypeSession.CreateDefault();
            session.Apply(PrototypeAction.SongNext);
            session.Apply(PrototypeAction.SongNext);
            session.Apply(PrototypeAction.Ok);
            int songIndex = session.SelectedSongIndex;

            session.Apply(PrototypeAction.Back);

            Assert.Equal(PrototypePhase.SongSelect, session.Phase);
            Assert.Equal(songIndex, session.SelectedSongIndex);
        }

        [Fact]
        public void DifficultyChangesSkipUnavailableOrLockedEntriesAndClampAtEdges()
        {
            PrototypeSession session = PrototypeSession.CreateDefault();
            session.Apply(PrototypeAction.Ok);

            session.Apply(PrototypeAction.DifficultyUp);
            session.Apply(PrototypeAction.DifficultyUp);
            session.Apply(PrototypeAction.DifficultyUp);
            session.Apply(PrototypeAction.DifficultyUp);
            session.Apply(PrototypeAction.DifficultyUp);

            Assert.Equal("Master", session.SelectedDifficulty.Name);
            Assert.True(session.SelectedDifficulty.CanSelect);

            session.Apply(PrototypeAction.DifficultyDown);
            Assert.Equal("Expert", session.SelectedDifficulty.Name);
            Assert.True(session.SelectedDifficulty.CanSelect);
        }

        [Fact]
        public void ConfirmedBackReturnsToDifficultySelection()
        {
            PrototypeSession session = PrototypeSession.CreateDefault();
            session.Apply(PrototypeAction.Ok);

            session.Apply(PrototypeAction.Ok);
            Assert.Equal(PrototypePhase.Confirmed, session.Phase);

            session.Apply(PrototypeAction.Back);
            Assert.Equal(PrototypePhase.DifficultySelect, session.Phase);
        }

        [Fact]
        public void SelectedDifficultyIsPreservedPerSong()
        {
            PrototypeSession session = PrototypeSession.CreateDefault();
            session.Apply(PrototypeAction.Ok);
            session.Apply(PrototypeAction.DifficultyUp);
            session.Apply(PrototypeAction.DifficultyUp);
            int firstSongDifficulty = session.SelectedDifficultyIndex;

            session.Apply(PrototypeAction.Back);
            session.Apply(PrototypeAction.SongNext);
            session.Apply(PrototypeAction.Ok);
            Assert.NotEqual(firstSongDifficulty, session.SelectedDifficultyIndex);

            session.Apply(PrototypeAction.Back);
            session.Apply(PrototypeAction.SongPrevious);
            session.Apply(PrototypeAction.Ok);

            Assert.Equal(firstSongDifficulty, session.SelectedDifficultyIndex);
        }
    }
}
