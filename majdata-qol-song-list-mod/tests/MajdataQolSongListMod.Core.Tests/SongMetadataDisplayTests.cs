using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class SongMetadataDisplayTests
    {
        [Fact]
        public void MetadataLineUsesPrdFormat()
        {
            SelectedSongMetadata metadata = SelectedSongMetadataFormatter.FromKnownFacts(
                "JPORTAL",
                "01:20",
                3,
                BpmFacet.KnownRange(240m, 240m));

            Assert.Equal("JPORTAL | 01:20 | 3 diffs | 240BPM", metadata.FormatLine());
        }

        [Fact]
        public void NewlineSourceNamesRenderAsSpaces()
        {
            SelectedSongMetadata metadata = SelectedSongMetadataFormatter.FromKnownFacts(
                "Random\nRecommended",
                null,
                1,
                BpmFacet.Pending());

            Assert.Equal("Random Recommended | --:-- | 1 diffs | BPM pending", metadata.FormatLine());
        }

        [Theory]
        [InlineData("Downloaded")]
        [InlineData("Online")]
        [InlineData("Mixed")]
        public void SourceTextCanRepresentKnownRuntimeSourceKinds(string source)
        {
            SelectedSongMetadata metadata = SelectedSongMetadataFormatter.FromKnownFacts(
                source,
                "--:--",
                2,
                BpmFacet.Unknown());

            Assert.StartsWith(source + " | --:-- | 2 diffs | unknown BPM", metadata.FormatLine());
        }
    }
}
