using System;

namespace MajdataQolSongListMod.Core
{
    public enum ActivePlayerMode
    {
        Guest,
        LocalProfile,
        MajdataNetAccount
    }

    public enum PlayerSaveTargetKind
    {
        Guest,
        LocalProfile,
        MajdataNetAccount
    }

    public sealed class PlayerSaveTarget
    {
        private PlayerSaveTarget(PlayerSaveTargetKind kind, string stableId, string displayName)
        {
            Kind = kind;
            StableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? KindLabel(kind) : displayName.Trim();
        }

        public PlayerSaveTargetKind Kind { get; private set; }

        public string StableId { get; private set; }

        public string DisplayName { get; private set; }

        public static PlayerSaveTarget Guest()
        {
            return new PlayerSaveTarget(PlayerSaveTargetKind.Guest, string.Empty, "Guest");
        }

        public static PlayerSaveTarget LocalProfile(LocalProfileMetadata profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException("profile");
            }

            return new PlayerSaveTarget(PlayerSaveTargetKind.LocalProfile, profile.ProfileId, profile.DisplayName);
        }

        public static PlayerSaveTarget MajdataNetAccount(string accountId, string displayName)
        {
            string normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "majdata.net Account" : displayName.Trim();
            string normalizedId = string.IsNullOrWhiteSpace(accountId) ? normalizedDisplayName : accountId.Trim();
            return new PlayerSaveTarget(PlayerSaveTargetKind.MajdataNetAccount, normalizedId, normalizedDisplayName);
        }

        public string ToDiagnosticString()
        {
            return "saveTargetKind=" + Kind.ToString() + "; saveTargetDisplayName=" + DisplayName + "; saveTargetId=" + StableId;
        }

        private static string KindLabel(PlayerSaveTargetKind kind)
        {
            switch (kind)
            {
                case PlayerSaveTargetKind.LocalProfile:
                    return "Local Profile";
                case PlayerSaveTargetKind.MajdataNetAccount:
                    return "majdata.net Account";
                default:
                    return "Guest";
            }
        }
    }

    public sealed class ActivePlayerSessionSnapshot
    {
        private ActivePlayerSessionSnapshot(
            ActivePlayerMode mode,
            string displayName,
            string localProfileId,
            string localProfileDisplayName,
            string onlineAccountId,
            string onlineAccountDisplayName,
            PlayerSaveTarget saveTarget)
        {
            Mode = mode;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? GetModeLabel(mode) : displayName.Trim();
            LocalProfileId = string.IsNullOrWhiteSpace(localProfileId) ? null : localProfileId.Trim();
            LocalProfileDisplayName = string.IsNullOrWhiteSpace(localProfileDisplayName) ? null : localProfileDisplayName.Trim();
            OnlineAccountId = string.IsNullOrWhiteSpace(onlineAccountId) ? null : onlineAccountId.Trim();
            OnlineAccountDisplayName = string.IsNullOrWhiteSpace(onlineAccountDisplayName) ? null : onlineAccountDisplayName.Trim();
            SaveTarget = saveTarget ?? PlayerSaveTarget.Guest();
        }

        public ActivePlayerMode Mode { get; private set; }

        public string ModeLabel
        {
            get { return GetModeLabel(Mode); }
        }

        public string DisplayName { get; private set; }

        public string LocalProfileId { get; private set; }

        public string LocalProfileDisplayName { get; private set; }

        public string OnlineAccountId { get; private set; }

        public string OnlineAccountDisplayName { get; private set; }

        public PlayerSaveTarget SaveTarget { get; private set; }

        public bool HasSelectedLocalProfile
        {
            get { return !string.IsNullOrWhiteSpace(LocalProfileId); }
        }

        public bool HasOnlineAccount
        {
            get { return !string.IsNullOrWhiteSpace(OnlineAccountId) || Mode == ActivePlayerMode.MajdataNetAccount; }
        }

        public static ActivePlayerSessionSnapshot Guest()
        {
            return new ActivePlayerSessionSnapshot(
                ActivePlayerMode.Guest,
                "Guest",
                null,
                null,
                null,
                null,
                PlayerSaveTarget.Guest());
        }

        public static ActivePlayerSessionSnapshot LocalProfile(LocalProfileMetadata profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException("profile");
            }

            return new ActivePlayerSessionSnapshot(
                ActivePlayerMode.LocalProfile,
                profile.DisplayName,
                profile.ProfileId,
                profile.DisplayName,
                null,
                null,
                PlayerSaveTarget.LocalProfile(profile));
        }

        public static ActivePlayerSessionSnapshot MajdataNetAccount(string accountId, string displayName)
        {
            PlayerSaveTarget saveTarget = PlayerSaveTarget.MajdataNetAccount(accountId, displayName);
            return new ActivePlayerSessionSnapshot(
                ActivePlayerMode.MajdataNetAccount,
                saveTarget.DisplayName,
                null,
                null,
                saveTarget.StableId,
                saveTarget.DisplayName,
                saveTarget);
        }

        public string ToDiagnosticString()
        {
            return "activePlayerMode=" + Mode.ToString() +
                "; activePlayerModeLabel=" + ModeLabel +
                "; activePlayerDisplayName=" + DisplayName +
                "; localProfileId=" + (LocalProfileId ?? string.Empty) +
                "; localProfileDisplayName=" + (LocalProfileDisplayName ?? string.Empty) +
                "; onlineAccountId=" + (OnlineAccountId ?? string.Empty) +
                "; onlineAccountDisplayName=" + (OnlineAccountDisplayName ?? string.Empty) +
                "; " + SaveTarget.ToDiagnosticString();
        }

        private static string GetModeLabel(ActivePlayerMode mode)
        {
            switch (mode)
            {
                case ActivePlayerMode.LocalProfile:
                    return "Local Profile";
                case ActivePlayerMode.MajdataNetAccount:
                    return "majdata.net Account";
                default:
                    return "Guest";
            }
        }
    }

    public sealed class ActivePlayerSession
    {
        private readonly object _syncRoot = new object();
        private ActivePlayerSessionSnapshot _current;

        public ActivePlayerSession()
        {
            _current = ActivePlayerSessionSnapshot.Guest();
        }

        public ActivePlayerSessionSnapshot Current
        {
            get
            {
                lock (_syncRoot)
                {
                    return _current;
                }
            }
        }

        public ActivePlayerSessionSnapshot UseGuest()
        {
            lock (_syncRoot)
            {
                _current = ActivePlayerSessionSnapshot.Guest();
                return _current;
            }
        }

        public ActivePlayerSessionSnapshot UseLocalProfile(LocalProfileMetadata profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException("profile");
            }

            lock (_syncRoot)
            {
                _current = ActivePlayerSessionSnapshot.LocalProfile(profile);
                return _current;
            }
        }

        public ActivePlayerSessionSnapshot UseMajdataNetAccount(string accountId, string displayName)
        {
            lock (_syncRoot)
            {
                _current = ActivePlayerSessionSnapshot.MajdataNetAccount(accountId, displayName);
                return _current;
            }
        }

        public string ToDiagnosticString()
        {
            return Current.ToDiagnosticString();
        }
    }
}
