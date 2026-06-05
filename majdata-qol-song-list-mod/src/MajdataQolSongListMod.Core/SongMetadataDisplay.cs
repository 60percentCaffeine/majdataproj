using System;

namespace MajdataQolSongListMod.Core
{
    public sealed class SelectedSongMetadata
    {
        public SelectedSongMetadata(string source, string length, int difficultyCount, BpmFacet bpm)
        {
            Source = NormalizeSource(source);
            Length = string.IsNullOrWhiteSpace(length) ? "--:--" : length.Trim();
            DifficultyCount = Math.Max(0, difficultyCount);
            Bpm = bpm ?? BpmFacet.Pending();
        }

        public string Source { get; private set; }

        public string Length { get; private set; }

        public int DifficultyCount { get; private set; }

        public BpmFacet Bpm { get; private set; }

        public string FormatLine()
        {
            return string.Format(
                "{0} | {1} | {2} diffs | {3}",
                Source,
                Length,
                DifficultyCount,
                ChartDataHydrator.FormatBpm(Bpm));
        }

        public static string NormalizeSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "Unknown";
            }

            return source.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    public static class SelectedSongMetadataFormatter
    {
        public static SelectedSongMetadata FromKnownFacts(string source, string length, int difficultyCount, BpmFacet bpm)
        {
            return new SelectedSongMetadata(source, length, difficultyCount, bpm);
        }

        public static string FormatLength(TimeSpan? length)
        {
            if (!length.HasValue || length.Value.TotalSeconds <= 0)
            {
                return "--:--";
            }

            int totalSeconds = (int)Math.Round(length.Value.TotalSeconds);
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            return hours > 0
                ? string.Format("{0}:{1:00}:{2:00}", hours, minutes, seconds)
                : string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
