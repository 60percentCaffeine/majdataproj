using System;

namespace MajdataQolSongListMod.Core
{
    public enum ProfileOwnedDataKind
    {
        Scores,
        Favorites,
        ProfileWideSettings
    }

    public enum ProfileOwnedStorageKind
    {
        ExistingGuestStorage,
        LocalProfileStorage,
        MajdataNetAccountStorage
    }

    public sealed class ProfileOwnedStorageRoute
    {
        public ProfileOwnedStorageRoute(ProfileOwnedDataKind dataKind, ProfileOwnedStorageKind storageKind, string ownerId, string displayName)
        {
            DataKind = dataKind;
            StorageKind = storageKind;
            OwnerId = string.IsNullOrWhiteSpace(ownerId) ? string.Empty : ownerId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? StorageKind.ToString() : displayName.Trim();
        }

        public ProfileOwnedDataKind DataKind { get; private set; }

        public ProfileOwnedStorageKind StorageKind { get; private set; }

        public string OwnerId { get; private set; }

        public string DisplayName { get; private set; }

        public bool UsesExistingGuestBehavior
        {
            get { return StorageKind == ProfileOwnedStorageKind.ExistingGuestStorage; }
        }
    }

    public static class ProfileScopedStorageRouter
    {
        public static ProfileOwnedStorageRoute Resolve(ActivePlayerSessionSnapshot session, ProfileOwnedDataKind dataKind)
        {
            ActivePlayerSessionSnapshot current = session ?? ActivePlayerSessionSnapshot.Guest();
            switch (current.Mode)
            {
                case ActivePlayerMode.LocalProfile:
                    return new ProfileOwnedStorageRoute(dataKind, ProfileOwnedStorageKind.LocalProfileStorage, current.LocalProfileId, current.LocalProfileDisplayName);
                case ActivePlayerMode.MajdataNetAccount:
                    return new ProfileOwnedStorageRoute(dataKind, ProfileOwnedStorageKind.MajdataNetAccountStorage, current.OnlineAccountId, current.OnlineAccountDisplayName);
                default:
                    return new ProfileOwnedStorageRoute(dataKind, ProfileOwnedStorageKind.ExistingGuestStorage, string.Empty, "Guest");
            }
        }
    }
}
