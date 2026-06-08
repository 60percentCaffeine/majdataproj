using System;
using System.IO;
using System.Linq;
using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class LocalProfileStoreTests
    {
        [Fact]
        public void ListSavedProfilesReturnsEmptyAndDoesNotCreateRootWhenNoneExist()
        {
            string root = NewTempPath();
            try
            {
                LocalProfileStore store = new LocalProfileStore(root);

                LocalProfileMetadata[] profiles = store.ListSavedProfiles().ToArray();

                Assert.Empty(profiles);
                Assert.False(Directory.Exists(root));
            }
            finally
            {
                DeleteIfExists(root);
            }
        }

        [Fact]
        public void ListSavedProfilesReadsExistingMetadataWithoutCreatingExtraData()
        {
            string root = NewTempPath();
            try
            {
                WriteProfile(root, "profile-bob", "Bob");
                WriteProfile(root, "profile-alice", "Alice");
                Directory.CreateDirectory(Path.Combine(root, "missing-metadata"));

                LocalProfileStore store = new LocalProfileStore(root);

                LocalProfileMetadata[] profiles = store.ListSavedProfiles().ToArray();

                Assert.Equal(new[] { "Alice", "Bob" }, profiles.Select(profile => profile.DisplayName).ToArray());
                Assert.Equal(new[] { "profile-alice", "profile-bob" }, profiles.Select(profile => profile.ProfileId).ToArray());
                Assert.False(File.Exists(Path.Combine(root, "missing-metadata", LocalProfileStore.MetadataFileName)));
            }
            finally
            {
                DeleteIfExists(root);
            }
        }

        [Fact]
        public void AccountEntriesAlwaysStartWithCreateNewAndGuest()
        {
            string root = NewTempPath();
            try
            {
                LocalProfileStore store = new LocalProfileStore(root);

                LocalProfileAccountEntry[] entries = store.ListAccountEntries().ToArray();

                Assert.Equal(new[] { LocalProfileAccountEntryKind.CreateNew, LocalProfileAccountEntryKind.Guest }, entries.Select(entry => entry.Kind).ToArray());
                Assert.Equal(new[] { "Create New", "Guest" }, entries.Select(entry => entry.Label).ToArray());
                Assert.False(Directory.Exists(root));
            }
            finally
            {
                DeleteIfExists(root);
            }
        }

        [Fact]
        public void DefaultRootIsDeterministicUnderGameUserData()
        {
            string root = LocalProfilePaths.DefaultRoot("game-root");

            Assert.Equal(Path.Combine("game-root", "UserData", "MajdataQolSongListMod", "LocalProfiles"), root);
        }

        private static string NewTempPath()
        {
            return Path.Combine(Path.GetTempPath(), "MajdataQolSongListMod.Core.Tests", Guid.NewGuid().ToString("N"));
        }

        private static void WriteProfile(string root, string id, string displayName)
        {
            string profileDirectory = Path.Combine(root, id);
            Directory.CreateDirectory(profileDirectory);
            File.WriteAllText(
                Path.Combine(profileDirectory, LocalProfileStore.MetadataFileName),
                "{\"id\":\"" + id + "\",\"displayName\":\"" + displayName + "\"}");
        }

        private static void DeleteIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
