using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class RuntimeScoreFacetAdapterTests
    {
        [Theory]
        [InlineData(100.5, "SSS+")]
        [InlineData(100.0, "SSS")]
        [InlineData(99.5, "SS+")]
        [InlineData(99.0, "SS")]
        [InlineData(98.0, "S+")]
        [InlineData(97.0, "S")]
        [InlineData(94.0, "AAA")]
        [InlineData(90.0, "AA")]
        [InlineData(80.0, "A")]
        [InlineData(75.0, "BBB")]
        [InlineData(70.0, "BB")]
        [InlineData(60.0, "B")]
        [InlineData(12.3, "C")]
        public void DxAccuracyMapsToRankBuckets(double dxAccuracy, string expectedRank)
        {
            Assert.Equal(expectedRank, RuntimeScoreFacetAdapter.RankFromDxAccuracy(dxAccuracy));
        }

        [Fact]
        public void MissingRuntimeScoreStaysNoPlayCompatible()
        {
            ScoreFacet score = RuntimeScoreFacetAdapter.FromRuntimeScore(null, null, "None", null);

            Assert.Null(score.Rank);
            Assert.Null(score.LocalPlayCount);
            Assert.False(score.HasFullCombo);
            Assert.False(score.HasAllPerfect);
            Assert.Null(score.DxScore);
        }

        [Theory]
        [InlineData("FC", true, false)]
        [InlineData("FCPlus", true, false)]
        [InlineData("AP", true, true)]
        [InlineData("APPlus", true, true)]
        [InlineData("None", false, false)]
        public void ComboStateMapsToApFcFacets(string comboState, bool expectedFullCombo, bool expectedAllPerfect)
        {
            ScoreFacet score = RuntimeScoreFacetAdapter.FromRuntimeScore(100.5, 1, comboState, 1234);

            Assert.Equal(expectedFullCombo, score.HasFullCombo);
            Assert.Equal(expectedAllPerfect, score.HasAllPerfect);
        }

        [Fact]
        public void LowPlayedScoreRemainsDistinctFromNoScore()
        {
            ScoreFacet low = RuntimeScoreFacetAdapter.FromRuntimeScore(42.5, 1, "None", 5);
            ScoreFacet none = RuntimeScoreFacetAdapter.FromRuntimeScore(null, null, null, null);

            Assert.Equal("C", low.Rank);
            Assert.Null(none.Rank);
        }
    }
}
