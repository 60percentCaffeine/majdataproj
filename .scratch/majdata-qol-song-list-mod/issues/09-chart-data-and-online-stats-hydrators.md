Status: completed

# Chart data and online stats hydrators

## What to build

Implement `ChartDataHydrator` for BPM calculation from `maidata` and `OnlineStatsHydrator` for interaction stats such as play count. BPM should display as a single value, range, pending, or unknown without blocking list construction.

## Acceptance criteria

- [ ] Local `maidata` parsing calculates BPM values compatible with the game's chart analysis approach.
- [ ] Online BPM hydration fetches/parses `maidata` only when queued.
- [ ] BPM format supports single values, ranges, pending, and unknown.
- [ ] Online interaction stats use 24-hour stale-while-revalidate semantics.
- [ ] Online aggregate play count can be fetched from interaction endpoints or degrades as unknown.
- [ ] Tests cover fixed BPM, varying BPM, malformed `maidata`, pending/unknown display, and stale interaction stats.

## Blocked by

- 08-hydration-store-and-scheduler

## Comments

- 2026-06-05: Implemented `ChartDataHydrator` and `OnlineStatsHydrator`. BPM hydration extracts Simai timing BPM markers from `maidata`, formats fixed/range/pending/unknown BPM values, fetches online `maidata` only when queued, and returns recoverable failures. Online stats hydration parses interaction JSON, uses 24-hour stale-while-revalidate cache reads, records failures without deleting stale data, and degrades missing play count to unknown. Verification: `test-unit.ps1` passed 87/87 tests; `build.ps1` passed; `install.ps1` installed the updated mod/core DLLs.
