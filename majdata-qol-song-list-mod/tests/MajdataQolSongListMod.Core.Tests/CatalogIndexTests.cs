using System;
using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class CatalogIndexTests
    {
        [Fact]
        public void LocalOnlyInputProducesLocalPreferredRow()
        {
            CatalogIndex index = CatalogIndex.Build(new[]
            {
                Local("hash-a", "Local Song", "Local Artist", "JPORTAL")
            });

            CatalogRow row = Assert.Single(index.Rows);
            Assert.Equal("hash-a", row.Hash);
            Assert.Equal("Local Song", row.Title);
            Assert.Equal("Local Artist", row.Artist);
            Assert.Equal(CatalogSource.Local, row.Source);
            Assert.True(row.HasLocalAsset);
            Assert.False(row.HasOnlineMetadata);
            Assert.Equal(PreferredPlaybackSource.Local, row.PreferredPlaybackSource);
            Assert.Equal("JPORTAL", row.LocalFolder);
            Assert.Null(row.OnlineId);
            Assert.Equal("Local Designer", Assert.Single(row.Designers));
            Assert.Equal(2, row.Levels.Count);
            Assert.Equal(7, row.Score.LocalPlayCount);
            Assert.Equal(HydrationState.Fresh, row.HydrationState);
        }

        [Fact]
        public void OnlineOnlyInputProducesOnlinePreferredRow()
        {
            CatalogIndex index = CatalogIndex.Build(new[]
            {
                Online("hash-b", "online-1", "Online Song", "Online Artist", "Uploader")
            });

            CatalogRow row = Assert.Single(index.Rows);
            Assert.Equal("hash-b", row.Hash);
            Assert.Equal("Online Song", row.Title);
            Assert.Equal("Online Artist", row.Artist);
            Assert.Equal("Uploader", row.Uploader);
            Assert.Equal(CatalogSource.Online, row.Source);
            Assert.False(row.HasLocalAsset);
            Assert.True(row.HasOnlineMetadata);
            Assert.Equal(PreferredPlaybackSource.Online, row.PreferredPlaybackSource);
            Assert.Equal("online-1", row.OnlineId);
            Assert.Null(row.LocalFolder);
            Assert.Equal(42, row.Interaction.OnlinePlayCount);
            Assert.Equal("featured", Assert.Single(row.CollectionMemberships).Id);
            Assert.Equal(HydrationState.Stale, row.HydrationState);
        }

        [Fact]
        public void DuplicateHashPrefersLocalPlaybackAndRetainsOnlineMetadata()
        {
            CatalogIndex index = CatalogIndex.Build(new[]
            {
                Local("shared", "Downloaded Title", "Downloaded Artist", "DownloadedFolder"),
                Online("SHARED", "online-shared", "Website Title", "Website Artist", "Website Uploader")
            });

            CatalogRow row = Assert.Single(index.Rows);
            Assert.Equal(CatalogSource.Local | CatalogSource.Online, row.Source);
            Assert.True(row.HasLocalAsset);
            Assert.True(row.HasOnlineMetadata);
            Assert.Equal(PreferredPlaybackSource.Local, row.PreferredPlaybackSource);
            Assert.Equal("DownloadedFolder", row.LocalFolder);
            Assert.Equal("online-shared", row.OnlineId);
            Assert.Equal("Website Title", row.Title);
            Assert.Equal("Website Artist", row.Artist);
            Assert.Equal("Website Uploader", row.Uploader);
            Assert.Equal(42, row.Interaction.OnlinePlayCount);
            Assert.Equal(7, row.Score.LocalPlayCount);
            Assert.Contains(row.Designers, value => value == "Local Designer");
            Assert.Contains(row.Designers, value => value == "Online Designer");
            Assert.Equal("featured", Assert.Single(row.CollectionMemberships).Id);
            Assert.Equal(HydrationState.Fresh, row.HydrationState);
        }

        [Fact]
        public void MissingAndBlankFieldsDoNotDropRows()
        {
            CatalogIndex index = CatalogIndex.Build(new[]
            {
                CatalogInput.Local(
                    " ",
                    "",
                    null,
                    "",
                    new CatalogLevel[0],
                    new string[] { "", null },
                    null,
                    null,
                    HydrationState.Unknown),
                CatalogInput.Online(
                    null,
                    "",
                    null,
                    "",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    HydrationState.Pending)
            });

            CatalogRow row = Assert.Single(index.Rows);
            Assert.Equal(string.Empty, row.Hash);
            Assert.Null(row.Title);
            Assert.Null(row.Artist);
            Assert.Null(row.Uploader);
            Assert.Empty(row.Designers);
            Assert.Empty(row.Levels);
            Assert.Equal(CatalogSource.Local | CatalogSource.Online, row.Source);
            Assert.Equal(PreferredPlaybackSource.Local, row.PreferredPlaybackSource);
            Assert.Equal(HydrationState.Pending, row.HydrationState);
        }

        [Fact]
        public void DistinctHashesRemainDistinctRows()
        {
            CatalogIndex index = CatalogIndex.Build(new[]
            {
                Local("hash-a", "A", "Artist", "Folder"),
                Online("hash-b", "online-b", "B", "Artist", "Uploader")
            });

            Assert.Equal(2, index.Rows.Count);
            Assert.Contains(index.Rows, row => row.Hash == "hash-a" && row.Source == CatalogSource.Local);
            Assert.Contains(index.Rows, row => row.Hash == "hash-b" && row.Source == CatalogSource.Online);
        }

        private static CatalogInput Local(string hash, string title, string artist, string folder)
        {
            return CatalogInput.Local(
                hash,
                title,
                artist,
                folder,
                new[]
                {
                    new CatalogLevel(0, "Basic", "7"),
                    new CatalogLevel(1, "Expert", "12+")
                },
                new[] { "Local Designer" },
                new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                new ScoreFacet("SS", 7, true, false, 987654),
                HydrationState.Fresh);
        }

        private static CatalogInput Online(string hash, string onlineId, string title, string artist, string uploader)
        {
            return CatalogInput.Online(
                hash,
                onlineId,
                title,
                artist,
                uploader,
                new[]
                {
                    new CatalogLevel(0, "Basic", "8"),
                    new CatalogLevel(1, "Expert", "13")
                },
                new[] { "Online Designer" },
                new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero),
                new InteractionFacet(42, 5, 3, new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero)),
                new[] { new CollectionMembership("featured", "Featured") },
                HydrationState.Stale);
        }
    }
}
