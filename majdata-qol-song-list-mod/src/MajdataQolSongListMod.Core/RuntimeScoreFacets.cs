using System;

namespace MajdataQolSongListMod.Core
{
    public static class RuntimeScoreFacetAdapter
    {
        public static ScoreFacet FromRuntimeScore(double? dxAccuracy, int? playCount, string comboState, long? dxScore)
        {
            bool hasScoreEvidence = (playCount.HasValue && playCount.Value > 0) ||
                (dxAccuracy.HasValue && dxAccuracy.Value > 0d) ||
                !string.IsNullOrWhiteSpace(comboState) ||
                (dxScore.HasValue && dxScore.Value > 0L);

            if (!hasScoreEvidence)
            {
                return ScoreFacet.Empty();
            }

            return new ScoreFacet(
                RankFromDxAccuracy(dxAccuracy),
                playCount,
                IsFullCombo(comboState),
                IsAllPerfect(comboState),
                ToNullableInt(dxScore));
        }

        public static string RankFromDxAccuracy(double? dxAccuracy)
        {
            if (!dxAccuracy.HasValue || dxAccuracy.Value <= 0d)
            {
                return null;
            }

            double value = dxAccuracy.Value;
            if (value >= 100.5d) return "SSS+";
            if (value >= 100d) return "SSS";
            if (value >= 99.5d) return "SS+";
            if (value >= 99d) return "SS";
            if (value >= 98d) return "S+";
            if (value >= 97d) return "S";
            if (value >= 94d) return "AAA";
            if (value >= 90d) return "AA";
            if (value >= 80d) return "A";
            if (value >= 75d) return "BBB";
            if (value >= 70d) return "BB";
            if (value >= 60d) return "B";
            return "C";
        }

        public static bool IsFullCombo(string comboState)
        {
            string normalized = NormalizeComboState(comboState);
            return normalized == "FC" || normalized == "FCPLUS" || normalized == "AP" || normalized == "APPLUS";
        }

        public static bool IsAllPerfect(string comboState)
        {
            string normalized = NormalizeComboState(comboState);
            return normalized == "AP" || normalized == "APPLUS";
        }

        private static string NormalizeComboState(string comboState)
        {
            return string.IsNullOrWhiteSpace(comboState)
                ? string.Empty
                : comboState.Replace("+", "Plus").Trim().ToUpperInvariant();
        }

        private static int? ToNullableInt(long? value)
        {
            if (!value.HasValue || value.Value <= 0L)
            {
                return null;
            }

            if (value.Value > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)value.Value;
        }
    }
}
