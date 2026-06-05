Status: completed

# Hydrate selected-song duration and BPM in live metadata

## What to build

Replace the live selected-song metadata fallback values with real duration and BPM data when they are available, while preserving quiet pending/unknown display when data has not been hydrated yet. The metadata line should continue to use the prototype-approved format and layout.

The implementation should use the existing hydration/cache concepts so chart data improves outside gameplay without blocking list startup or causing the visible list to jump while the player browses.

## Acceptance criteria

- [x] Selected-song metadata displays a real song length when a reliable nonblocking runtime source is available, and falls back to `--:--` only when length is genuinely unknown.
- [x] Selected-song metadata displays hydrated BPM from local or online chart data when available.
- [x] Pending and unknown BPM states remain explicit and do not look like confirmed zero or confirmed data.
- [x] BPM hydration does not run during gameplay/practice and can resume in list/menu/settings scenes.
- [x] Hydrated metadata updates do not force an immediate live resort or cursor jump while the player is browsing.
- [x] In-game canaries prove at least one selected song can show hydrated BPM or a controlled hydrated test value in the metadata line.
- [x] Unit tests and in-game smoke/canaries still pass.

## Blocked by

None - can start immediately

## Comments

- 2026-06-05: Added live selected-song metadata hydration backed by the existing hydration scheduler policy. The runtime now caches selected-song duration/BPM facts by hash, queues background length/BPM extraction only outside gameplay/practice, keeps pending/unknown BPM states explicit, and updates the metadata line without rebuilding `SongStorage.Collections` or moving the cursor. Added a diagnostic hydrated-value setter for deterministic canaries and expanded smoke coverage to prove `02:34`/`145BPM` appears while collection order and cursor remain unchanged. Verification: `build.ps1` passed, `test-unit.ps1` passed 100/100, and expanded `test-smoke.ps1` passed against MajdataPlay.
