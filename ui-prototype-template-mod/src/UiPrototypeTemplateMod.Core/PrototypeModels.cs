using System.Collections.Generic;

namespace UiPrototypeTemplateMod.Core
{
    public sealed class PrototypeSong
    {
        public PrototypeSong(
            string title,
            string artist,
            string category,
            int bpm,
            bool isLong,
            bool isSpecial,
            string badge,
            IReadOnlyList<PrototypeDifficulty> difficulties)
        {
            Title = title;
            Artist = artist;
            Category = category;
            Bpm = bpm;
            IsLong = isLong;
            IsSpecial = isSpecial;
            Badge = badge;
            Difficulties = difficulties;
        }

        public string Title { get; }
        public string Artist { get; }
        public string Category { get; }
        public int Bpm { get; }
        public bool IsLong { get; }
        public bool IsSpecial { get; }
        public string Badge { get; }
        public IReadOnlyList<PrototypeDifficulty> Difficulties { get; }
    }

    public sealed class PrototypeDifficulty
    {
        public PrototypeDifficulty(string name, string level, bool available, bool locked, string rank, int dxScore)
        {
            Name = name;
            Level = level;
            Available = available;
            Locked = locked;
            Rank = rank;
            DxScore = dxScore;
        }

        public string Name { get; }
        public string Level { get; }
        public bool Available { get; }
        public bool Locked { get; }
        public string Rank { get; }
        public int DxScore { get; }

        public bool CanSelect
        {
            get { return Available && !Locked; }
        }
    }
}
