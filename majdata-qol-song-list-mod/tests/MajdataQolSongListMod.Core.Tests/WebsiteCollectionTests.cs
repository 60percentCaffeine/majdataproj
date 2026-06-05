using System;
using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class WebsiteCollectionTests
    {
        [Fact]
        public void CollectionJsonConvertsToOwnedAndSubscribedSummaries()
        {
            string json = "[{\"id\":\"owned-1\",\"name\":\"Owned Set\",\"description\":\"mine\",\"visibility\":1,\"totalCount\":3}]";
            MajdataNetAdapter adapter = new MajdataNetAdapter("https://majdata.example", new NoopFetcher());

            WebsiteCollectionSummary owned = Assert.Single(adapter.ConvertCollectionListJson(json, WebsiteCollectionKind.UserOwned));
            WebsiteCollectionSummary subscribed = Assert.Single(adapter.ConvertCollectionListJson(json, WebsiteCollectionKind.Subscribed));

            Assert.Equal("owned-1", owned.Id);
            Assert.Equal("Owned Set", owned.Name);
            Assert.Equal("mine", owned.Description);
            Assert.Equal(WebsiteCollectionKind.UserOwned, owned.Kind);
            Assert.Equal(1, owned.Visibility);
            Assert.Equal(3, owned.TotalCount);
            Assert.Equal(WebsiteCollectionKind.Subscribed, subscribed.Kind);
        }

        [Fact]
        public void CollectionHashListJsonSupportsStringAndObjectShapes()
        {
            MajdataNetAdapter adapter = new MajdataNetAdapter("https://majdata.example", new NoopFetcher());

            Assert.Equal(new[] { "hash-a", "hash-b" }, adapter.ConvertCollectionHashListJson("[\"hash-a\",\"hash-b\"]").ToArray());
            Assert.Equal(
                new[] { "hash-c", "hash-d" },
                adapter.ConvertCollectionHashListJson("[{\"hash\":\"hash-c\"},{\"chartHash\":\"hash-d\"}]").ToArray());
        }

        [Fact]
        public void CollectionResolutionHandlesLocalOnlineDuplicateAndUnresolvedRows()
        {
            CatalogIndex index = CatalogIndex.Build(new[]
            {
                Local("local-only", "Local Only"),
                Online("online-only", "Online Only"),
                Local("duplicate", "Downloaded Duplicate"),
                Online("duplicate", "Website Duplicate")
            });
            WebsiteCollectionSummary summary = new WebsiteCollectionSummary("c1", "Collection One", null, WebsiteCollectionKind.UserOwned, 1, null);
            WebsiteCollectionResolver resolver = new WebsiteCollectionResolver(index);

            ResolvedWebsiteCollection resolved = resolver.Resolve(
                summary,
                new[] { "local-only", "online-only", "duplicate", "missing" },
                OnlineScope.Mixed);

            Assert.Equal(4, resolved.TotalCount);
            Assert.Equal(3, resolved.ResolvedCount);
            Assert.Equal(1, resolved.UnresolvedCount);
            Assert.Contains(resolved.PlayableRows, row => row.Hash == "local-only" && row.PreferredPlaybackSource == PreferredPlaybackSource.Local);
            Assert.Contains(resolved.PlayableRows, row => row.Hash == "online-only" && row.PreferredPlaybackSource == PreferredPlaybackSource.Online);
            CatalogRow duplicate = resolved.PlayableRows.Single(row => row.Hash == "duplicate");
            Assert.Equal(CatalogSource.Local | CatalogSource.Online, duplicate.Source);
            Assert.Equal(PreferredPlaybackSource.Local, duplicate.PreferredPlaybackSource);
            Assert.Equal("online-duplicate", duplicate.OnlineId);
        }

        [Fact]
        public void UnresolvedEntriesAreOmittedButReflectedInCounts()
        {
            CatalogIndex index = CatalogIndex.Build(new[] { Local("local", "Local") });
            WebsiteCollectionSummary summary = new WebsiteCollectionSummary("c2", "Partial", null, WebsiteCollectionKind.Subscribed, 1, 5);

            ResolvedWebsiteCollection resolved = new WebsiteCollectionResolver(index).Resolve(
                summary,
                new[] { "local", "missing-a", "missing-b" },
                OnlineScope.Mixed);

            Assert.Single(resolved.PlayableRows);
            Assert.Equal(5, resolved.TotalCount);
            Assert.Equal(1, resolved.ResolvedCount);
            Assert.Equal(4, resolved.UnresolvedCount);
            CatalogCollection collection = resolved.ToCatalogCollection();
            Assert.Equal("Partial", collection.Name);
            Assert.Equal("Partial Count:1/5 resolved", collection.SelectedInfoText);
            Assert.True(collection.IsVirtual);
            Assert.True(collection.IsOnlineBacked);
        }

        [Fact]
        public void DownloadedOnlyScopeHidesOnlineOnlyCollectionRows()
        {
            CatalogIndex index = CatalogIndex.Build(new[]
            {
                Local("local-only", "Local Only"),
                Online("online-only", "Online Only"),
                Local("duplicate", "Downloaded Duplicate"),
                Online("duplicate", "Website Duplicate")
            });

            ResolvedWebsiteCollection resolved = new WebsiteCollectionResolver(index).Resolve(
                new WebsiteCollectionSummary("c3", "Downloaded", null, WebsiteCollectionKind.UserOwned, 1, null),
                new[] { "local-only", "online-only", "duplicate" },
                OnlineScope.DownloadedOnly);

            Assert.Equal(2, resolved.ResolvedCount);
            Assert.Equal(1, resolved.UnresolvedCount);
            Assert.Contains(resolved.PlayableRows, row => row.Hash == "local-only");
            Assert.Contains(resolved.PlayableRows, row => row.Hash == "duplicate");
            Assert.DoesNotContain(resolved.PlayableRows, row => row.Hash == "online-only");
        }

        [Fact]
        public void OnlineOnlyScopeUsesOnlyOnlineOnlyRows()
        {
            CatalogIndex index = CatalogIndex.Build(new[]
            {
                Local("local-only", "Local Only"),
                Online("online-only", "Online Only"),
                Local("duplicate", "Downloaded Duplicate"),
                Online("duplicate", "Website Duplicate")
            });

            ResolvedWebsiteCollection resolved = new WebsiteCollectionResolver(index).Resolve(
                new WebsiteCollectionSummary("c4", "Online", null, WebsiteCollectionKind.Subscribed, 1, null),
                new[] { "local-only", "online-only", "duplicate" },
                OnlineScope.OnlineOnly);

            Assert.Equal(1, resolved.ResolvedCount);
            Assert.Equal(2, resolved.UnresolvedCount);
            Assert.Equal("online-only", Assert.Single(resolved.PlayableRows).Hash);
        }

        private static CatalogInput Local(string hash, string title)
        {
            return CatalogInput.Local(
                hash,
                title,
                "Artist",
                "Folder",
                new[] { new CatalogLevel(0, "Easy", "1") },
                new[] { "Local Designer" },
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ScoreFacet.Empty(),
                HydrationState.Fresh);
        }

        private static CatalogInput Online(string hash, string title)
        {
            return CatalogInput.Online(
                hash,
                "online-" + hash,
                title,
                "Artist",
                "Uploader",
                new[] { new CatalogLevel(0, "Easy", "1") },
                new[] { "Online Designer" },
                new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                InteractionFacet.Empty(),
                null,
                HydrationState.Unknown);
        }

        private sealed class NoopFetcher : ITextFetcher
        {
            public string GetString(string url)
            {
                throw new InvalidOperationException("No network expected in this test.");
            }
        }
    }
}
