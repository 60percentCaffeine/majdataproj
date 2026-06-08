using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MajdataQolSongListMod.Core
{
    public sealed class LocalProfileMetadata
    {
        public LocalProfileMetadata(string profileId, string displayName, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                throw new ArgumentException("Profile id is required.", "profileId");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name is required.", "displayName");
            }

            ProfileId = profileId.Trim();
            DisplayName = displayName.Trim();
            DirectoryPath = string.IsNullOrWhiteSpace(directoryPath) ? string.Empty : directoryPath;
        }

        public string ProfileId { get; private set; }

        public string DisplayName { get; private set; }

        public string DirectoryPath { get; private set; }
    }

    public enum LocalProfileAccountEntryKind
    {
        CreateNew,
        Guest,
        SavedProfile
    }

    public sealed class LocalProfileAccountEntry
    {
        private LocalProfileAccountEntry(LocalProfileAccountEntryKind kind, string label, LocalProfileMetadata profile)
        {
            Kind = kind;
            Label = string.IsNullOrWhiteSpace(label) ? kind.ToString() : label.Trim();
            Profile = profile;
        }

        public LocalProfileAccountEntryKind Kind { get; private set; }

        public string Label { get; private set; }

        public LocalProfileMetadata Profile { get; private set; }

        public static LocalProfileAccountEntry CreateNew()
        {
            return new LocalProfileAccountEntry(LocalProfileAccountEntryKind.CreateNew, "Create New", null);
        }

        public static LocalProfileAccountEntry Guest()
        {
            return new LocalProfileAccountEntry(LocalProfileAccountEntryKind.Guest, "Guest", null);
        }

        public static LocalProfileAccountEntry SavedProfile(LocalProfileMetadata profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException("profile");
            }

            return new LocalProfileAccountEntry(LocalProfileAccountEntryKind.SavedProfile, profile.DisplayName, profile);
        }
    }

    public sealed class LocalProfileStore
    {
        public const string MetadataFileName = "profile.json";

        private readonly string _rootDirectory;

        public LocalProfileStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("Root directory is required.", "rootDirectory");
            }

            _rootDirectory = rootDirectory;
        }

        public string RootDirectory
        {
            get { return _rootDirectory; }
        }

        public IReadOnlyList<LocalProfileMetadata> ListSavedProfiles()
        {
            if (!Directory.Exists(_rootDirectory))
            {
                return new LocalProfileMetadata[0];
            }

            List<LocalProfileMetadata> profiles = new List<LocalProfileMetadata>();
            foreach (string profileDirectory in Directory.GetDirectories(_rootDirectory))
            {
                LocalProfileMetadata profile = TryReadProfileMetadata(profileDirectory);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }

            profiles.Sort(CompareProfiles);
            return profiles.ToArray();
        }

        public IReadOnlyList<LocalProfileAccountEntry> ListAccountEntries()
        {
            List<LocalProfileAccountEntry> entries = new List<LocalProfileAccountEntry>();
            entries.Add(LocalProfileAccountEntry.CreateNew());
            entries.Add(LocalProfileAccountEntry.Guest());

            foreach (LocalProfileMetadata profile in ListSavedProfiles())
            {
                entries.Add(LocalProfileAccountEntry.SavedProfile(profile));
            }

            return entries.ToArray();
        }

        private static int CompareProfiles(LocalProfileMetadata left, LocalProfileMetadata right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int displayComparison = StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName);
            if (displayComparison != 0)
            {
                return displayComparison;
            }

            return StringComparer.Ordinal.Compare(left.ProfileId, right.ProfileId);
        }

        private static LocalProfileMetadata TryReadProfileMetadata(string profileDirectory)
        {
            if (string.IsNullOrWhiteSpace(profileDirectory))
            {
                return null;
            }

            string metadataPath = Path.Combine(profileDirectory, MetadataFileName);
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LocalProfileMetadataRecord));
                using (FileStream stream = File.OpenRead(metadataPath))
                {
                    LocalProfileMetadataRecord record = serializer.ReadObject(stream) as LocalProfileMetadataRecord;
                    if (record == null || string.IsNullOrWhiteSpace(record.Id) || string.IsNullOrWhiteSpace(record.DisplayName))
                    {
                        return null;
                    }

                    return new LocalProfileMetadata(record.Id, record.DisplayName, profileDirectory);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static class LocalProfilePaths
    {
        public static string DefaultRoot(string gameRootDirectory)
        {
            string baseDirectory = string.IsNullOrWhiteSpace(gameRootDirectory) ? Environment.CurrentDirectory : gameRootDirectory;
            return Path.Combine(baseDirectory, "UserData", "MajdataQolSongListMod", "LocalProfiles");
        }
    }

    [DataContract]
    internal sealed class LocalProfileMetadataRecord
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "displayName")]
        public string DisplayName { get; set; }
    }
}
