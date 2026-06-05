Status: completed

# Hydration store and scheduler

## What to build

Add persistent cache storage and hydration scheduling for BPM and interaction stats. Hydration must prioritize missing before stale, offline before online, special online before normal online, and visible/nearby before non-visible, while pausing in gameplay/practice scenes.

## Acceptance criteria

- [x] Cache data is stored under the mod's own cache location and survives restart in tests.
- [x] Fresh values are used without refresh; stale values are used while refresh is queued.
- [x] Missing values queue ahead of stale values.
- [x] Offline rows queue ahead of online rows.
- [x] Special online rows queue ahead of normal online rows.
- [x] Visible and nearby rows queue ahead of non-visible rows.
- [x] Gameplay/practice scene state blocks hydration; menu/list/setting/title states allow it.
- [x] Network hydration failures are logged/recoverable and do not delete stale data.

## Blocked by

- 02-catalog-row-and-index-deduplication

## Comments

- 2026-06-05: Implemented `HydrationStore`, `HydrationScheduler`, `BrowsingContext`, and hydration work/cache models. Cache values are stored under a mod-owned `MajdataQolSongListMod` cache directory; fresh/stale/missing read behavior drives refresh decisions; scheduler prioritizes missing before stale, local/offline before online, special online before normal online, visible before nearby before hidden, and blocks gameplay/practice scenes. Failures are recorded recoverably without deleting stale cache data. Verification: `test-unit.ps1` passed 79/79 tests; `build.ps1` passed; `install.ps1` installed the updated mod/core DLLs.
