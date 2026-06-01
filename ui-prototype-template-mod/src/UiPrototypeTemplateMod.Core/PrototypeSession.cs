using System;
using System.Collections.Generic;

namespace UiPrototypeTemplateMod.Core
{
    public sealed class PrototypeSession
    {
        private readonly int[] _selectedDifficultyBySong;

        public PrototypeSession(IReadOnlyList<PrototypeSong> songs)
        {
            if (songs == null)
            {
                throw new ArgumentNullException("songs");
            }

            if (songs.Count == 0)
            {
                throw new ArgumentException("At least one song is required.", "songs");
            }

            Songs = songs;
            _selectedDifficultyBySong = new int[songs.Count];
            for (int i = 0; i < songs.Count; i++)
            {
                _selectedDifficultyBySong[i] = FirstSelectableDifficultyIndex(songs[i]);
            }
        }

        public IReadOnlyList<PrototypeSong> Songs { get; }
        public PrototypePhase Phase { get; private set; }
        public int SelectedSongIndex { get; private set; }

        public PrototypeSong SelectedSong
        {
            get { return Songs[SelectedSongIndex]; }
        }

        public int SelectedDifficultyIndex
        {
            get { return _selectedDifficultyBySong[SelectedSongIndex]; }
        }

        public PrototypeDifficulty SelectedDifficulty
        {
            get { return SelectedSong.Difficulties[SelectedDifficultyIndex]; }
        }

        public static PrototypeSession CreateDefault()
        {
            return new PrototypeSession(PrototypeData.CreateDefaultSongs());
        }

        public void Apply(PrototypeAction action)
        {
            switch (Phase)
            {
                case PrototypePhase.SongSelect:
                    ApplySongSelect(action);
                    break;
                case PrototypePhase.DifficultySelect:
                    ApplyDifficultySelect(action);
                    break;
                case PrototypePhase.Confirmed:
                    ApplyConfirmed(action);
                    break;
            }
        }

        private void ApplySongSelect(PrototypeAction action)
        {
            switch (action)
            {
                case PrototypeAction.SongNext:
                    SelectedSongIndex = Wrap(SelectedSongIndex + 1, Songs.Count);
                    break;
                case PrototypeAction.SongPrevious:
                    SelectedSongIndex = Wrap(SelectedSongIndex - 1, Songs.Count);
                    break;
                case PrototypeAction.Ok:
                    NormalizeSelectedDifficulty();
                    Phase = PrototypePhase.DifficultySelect;
                    break;
            }
        }

        private void ApplyDifficultySelect(PrototypeAction action)
        {
            switch (action)
            {
                case PrototypeAction.SongNext:
                case PrototypeAction.DifficultyUp:
                    MoveDifficulty(1);
                    break;
                case PrototypeAction.SongPrevious:
                case PrototypeAction.DifficultyDown:
                    MoveDifficulty(-1);
                    break;
                case PrototypeAction.Ok:
                    NormalizeSelectedDifficulty();
                    Phase = PrototypePhase.Confirmed;
                    break;
                case PrototypeAction.Back:
                    Phase = PrototypePhase.SongSelect;
                    break;
            }
        }

        private void ApplyConfirmed(PrototypeAction action)
        {
            if (action == PrototypeAction.Back)
            {
                Phase = PrototypePhase.DifficultySelect;
            }
        }

        private void MoveDifficulty(int direction)
        {
            PrototypeSong song = SelectedSong;
            int current = SelectedDifficultyIndex;
            int next = current + direction;

            while (next >= 0 && next < song.Difficulties.Count)
            {
                if (song.Difficulties[next].CanSelect)
                {
                    _selectedDifficultyBySong[SelectedSongIndex] = next;
                    return;
                }

                next += direction;
            }
        }

        private void NormalizeSelectedDifficulty()
        {
            PrototypeSong song = SelectedSong;
            int current = SelectedDifficultyIndex;
            if (current >= 0 && current < song.Difficulties.Count && song.Difficulties[current].CanSelect)
            {
                return;
            }

            _selectedDifficultyBySong[SelectedSongIndex] = FirstSelectableDifficultyIndex(song);
        }

        private static int FirstSelectableDifficultyIndex(PrototypeSong song)
        {
            for (int i = 0; i < song.Difficulties.Count; i++)
            {
                if (song.Difficulties[i].CanSelect)
                {
                    return i;
                }
            }

            return 0;
        }

        private static int Wrap(int value, int count)
        {
            if (value < 0)
            {
                return count - 1;
            }

            if (value >= count)
            {
                return 0;
            }

            return value;
        }
    }
}
