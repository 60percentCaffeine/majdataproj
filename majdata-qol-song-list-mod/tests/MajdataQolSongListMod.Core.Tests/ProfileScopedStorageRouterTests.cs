using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class ProfileScopedStorageRouterTests
    {
        [Theory]
        [InlineData(ProfileOwnedDataKind.Scores)]
        [InlineData(ProfileOwnedDataKind.Favorites)]
        [InlineData(ProfileOwnedDataKind.ProfileWideSettings)]
        public void GuestDefaultKeepsProfileOwnedDataOnExistingGuestStorage(ProfileOwnedDataKind dataKind)
        {
            ActivePlayerSession session = new ActivePlayerSession();

            ProfileOwnedStorageRoute route = ProfileScopedStorageRouter.Resolve(session.Current, dataKind);

            Assert.Equal(dataKind, route.DataKind);
            Assert.Equal(ProfileOwnedStorageKind.ExistingGuestStorage, route.StorageKind);
            Assert.True(route.UsesExistingGuestBehavior);
            Assert.Equal(string.Empty, route.OwnerId);
            Assert.Equal("Guest", route.DisplayName);
        }

        [Fact]
        public void LocalProfileRouteUsesSelectedProfileWithoutAffectingGuestRoute()
        {
            ActivePlayerSession session = new ActivePlayerSession();
            LocalProfileMetadata profile = new LocalProfileMetadata("profile-alice", "Alice", "profiles/profile-alice");
            session.UseLocalProfile(profile);

            ProfileOwnedStorageRoute route = ProfileScopedStorageRouter.Resolve(session.Current, ProfileOwnedDataKind.Favorites);

            Assert.Equal(ProfileOwnedStorageKind.LocalProfileStorage, route.StorageKind);
            Assert.False(route.UsesExistingGuestBehavior);
            Assert.Equal("profile-alice", route.OwnerId);
            Assert.Equal("Alice", route.DisplayName);
        }
    }
}
