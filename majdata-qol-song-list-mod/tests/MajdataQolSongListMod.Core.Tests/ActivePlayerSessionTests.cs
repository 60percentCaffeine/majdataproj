using MajdataQolSongListMod.Core;
using Xunit;

namespace MajdataQolSongListMod.Core.Tests
{
    public sealed class ActivePlayerSessionTests
    {
        [Fact]
        public void DefaultsToGuestWithNoSelectedLocalProfileAndGuestSaveTarget()
        {
            ActivePlayerSession session = new ActivePlayerSession();

            ActivePlayerSessionSnapshot snapshot = session.Current;

            Assert.Equal(ActivePlayerMode.Guest, snapshot.Mode);
            Assert.Equal("Guest", snapshot.ModeLabel);
            Assert.Equal("Guest", snapshot.DisplayName);
            Assert.False(snapshot.HasSelectedLocalProfile);
            Assert.Null(snapshot.LocalProfileId);
            Assert.Null(snapshot.LocalProfileDisplayName);
            Assert.Null(snapshot.OnlineAccountId);
            Assert.Equal(PlayerSaveTargetKind.Guest, snapshot.SaveTarget.Kind);
            Assert.Equal("Guest", snapshot.SaveTarget.DisplayName);
        }

        [Fact]
        public void BasicModeTransitionsAreMutuallyExclusive()
        {
            ActivePlayerSession session = new ActivePlayerSession();
            LocalProfileMetadata alice = new LocalProfileMetadata("profile-alice", " Alice ", "profiles/profile-alice");

            ActivePlayerSessionSnapshot local = session.UseLocalProfile(alice);

            Assert.Equal(ActivePlayerMode.LocalProfile, local.Mode);
            Assert.Equal("Local Profile", local.ModeLabel);
            Assert.Equal("profile-alice", local.LocalProfileId);
            Assert.Equal("Alice", local.LocalProfileDisplayName);
            Assert.Null(local.OnlineAccountId);
            Assert.Equal(PlayerSaveTargetKind.LocalProfile, local.SaveTarget.Kind);
            Assert.Equal("Alice", local.SaveTarget.DisplayName);

            ActivePlayerSessionSnapshot online = session.UseMajdataNetAccount("net-42", "Net Player");

            Assert.Equal(ActivePlayerMode.MajdataNetAccount, online.Mode);
            Assert.Equal("majdata.net Account", online.ModeLabel);
            Assert.Null(online.LocalProfileId);
            Assert.Null(online.LocalProfileDisplayName);
            Assert.Equal("net-42", online.OnlineAccountId);
            Assert.Equal("Net Player", online.OnlineAccountDisplayName);
            Assert.Equal(PlayerSaveTargetKind.MajdataNetAccount, online.SaveTarget.Kind);

            ActivePlayerSessionSnapshot guest = session.UseGuest();

            Assert.Equal(ActivePlayerMode.Guest, guest.Mode);
            Assert.Null(guest.LocalProfileId);
            Assert.Null(guest.OnlineAccountId);
            Assert.Equal(PlayerSaveTargetKind.Guest, guest.SaveTarget.Kind);
        }

        [Fact]
        public void DiagnosticsReportCurrentActivePlayerMode()
        {
            ActivePlayerSession session = new ActivePlayerSession();

            string diagnostics = session.ToDiagnosticString();

            Assert.Contains("activePlayerMode=Guest", diagnostics);
            Assert.Contains("saveTargetKind=Guest", diagnostics);
        }
    }
}
