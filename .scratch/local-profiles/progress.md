# Local Profiles Progress

- 2026-06-08: Completed task 01, **Bootable local profiles shell with Guest session default**. Added a Unity-free `ActivePlayerSession` API for Guest, Local Profile, and majdata.net Account modes; added `LocalProfileStore` metadata listing with no root creation on empty machines; added a guest-preserving profile-owned storage routing seam; exposed startup/eval diagnostics for active player mode and profile store state; added unit coverage and a focused real-game local-profiles boot smoke test.
  - Verified: `majdata-qol-song-list-mod/test-unit.ps1`, `majdata-qol-song-list-mod/build.ps1`, `majdata-qol-song-list-mod/test-local-profiles-smoke.ps1`.
