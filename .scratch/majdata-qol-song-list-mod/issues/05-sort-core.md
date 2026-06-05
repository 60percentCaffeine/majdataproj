Status: completed

# Sort core

## What to build

Implement pure sorting for existing Majdata modes plus artist, play count, BPM, AP/FC rank, DX score, and release/upload time where data exists. Unknown values should sort after known values for descending progress-style sorts, and unknown online play count must remain distinct from confirmed zero.

## Acceptance criteria

- [x] Sort option set matches the UI proposal and omits unsupported Sinmai-only modes.
- [x] Artist/title/release sorting are deterministic with stable tie-breakers.
- [x] Local play count sorts immediately when available.
- [x] Unknown online play count is distinguishable from confirmed zero.
- [x] BPM sorting uses known values and places unknown/pending after known values.
- [x] AP/FC and DX score sorts degrade cleanly when no score data exists.
- [x] Unit tests cover known, unknown, stale, and zero values.

## Blocked by

- 03-level-difficulty-and-grouping-core

## Comments

- 2026-06-05: Implemented `BpmFacet`, `CatalogSortMode`, `CatalogSortRequest`, and `CatalogSorter`. Sorting now covers UI-facing modes plus internal DX score sorting, with deterministic title/hash tie-breakers and known-before-unknown behavior for progress-style sorts. Verification: `test-unit.ps1` passed 52/52 tests; `build.ps1` passed; `install.ps1` installed the updated mod/core DLLs.
