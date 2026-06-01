using System.Collections.Generic;

namespace UiPrototypeTemplateMod.Core
{
    public static class PrototypeData
    {
        public static IReadOnlyList<PrototypeSong> CreateDefaultSongs()
        {
            return new[]
            {
                new PrototypeSong(
                    "MAJTITLE",
                    "bbben",
                    "Original",
                    160,
                    false,
                    true,
                    "Built-in",
                    Difficulties(
                        Difficulty("Basic", "3", true, false, "SSS", 1004500),
                        Difficulty("Advanced", "6", true, false, "SS+", 1001200),
                        Difficulty("Expert", "10+", true, false, "S+", 985000),
                        Difficulty("Master", "13", true, false, "AA", 912000),
                        Difficulty("Re:Master", "13+", false, false, "--", 0))),
                new PrototypeSong(
                    "Starlit Packet",
                    "Mira Signal",
                    "Pop & Anime",
                    178,
                    false,
                    false,
                    "New",
                    Difficulties(
                        Difficulty("Basic", "2", true, false, "SS", 997500),
                        Difficulty("Advanced", "5", true, false, "S", 972000),
                        Difficulty("Expert", "9", true, false, "AAA", 948000),
                        Difficulty("Master", "12", true, true, "LOCK", 0))),
                new PrototypeSong(
                    "Longing Orbit",
                    "Kisaragi Drive",
                    "niconico & Vocaloid",
                    132,
                    true,
                    false,
                    "Long",
                    Difficulties(
                        Difficulty("Basic", "4", true, false, "SS+", 1000200),
                        Difficulty("Advanced", "7", true, false, "SS", 995000),
                        Difficulty("Expert", "11", true, false, "S", 970000),
                        Difficulty("Master", "13", false, false, "--", 0))),
                new PrototypeSong(
                    "Voltage Crown",
                    "RDX Project",
                    "Game & Variety",
                    222,
                    false,
                    true,
                    "Boss",
                    Difficulties(
                        Difficulty("Basic", "5", true, false, "S", 968000),
                        Difficulty("Advanced", "8", true, false, "AAA", 940000),
                        Difficulty("Expert", "12+", true, false, "AA", 902000),
                        Difficulty("Master", "14", true, false, "B", 810000),
                        Difficulty("Re:Master", "14+", true, true, "LOCK", 0))),
                new PrototypeSong(
                    "Rain Glass Metro",
                    "Azure Port",
                    "Original",
                    148,
                    false,
                    false,
                    "Favorite",
                    Difficulties(
                        Difficulty("Basic", "1", true, false, "SSS+", 1009000),
                        Difficulty("Advanced", "4", true, false, "SSS", 1005000),
                        Difficulty("Expert", "8", true, false, "SS", 998000),
                        Difficulty("Master", "11+", true, false, "SS", 992000)))
            };
        }

        private static IReadOnlyList<PrototypeDifficulty> Difficulties(params PrototypeDifficulty[] difficulties)
        {
            return difficulties;
        }

        private static PrototypeDifficulty Difficulty(string name, string level, bool available, bool locked, string rank, int dxScore)
        {
            return new PrototypeDifficulty(name, level, available, locked, rank, dxScore);
        }
    }
}
