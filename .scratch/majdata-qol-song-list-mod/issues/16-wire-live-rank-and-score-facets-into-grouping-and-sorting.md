Status: completed

# Wire live rank and score facets into grouping and sorting

## What to build

Replace the live Rank grouping placeholder with real score/rank buckets and make score-dependent sorting use available local or online score facts. The player should be able to browse played and unplayed charts by meaningful rank state instead of seeing every chart in `No Play`.

This slice should preserve the existing no-score fallback behavior: charts without score evidence remain discoverable under `No Play`, and unavailable online score data degrades cleanly rather than being treated as confirmed zero.

## Acceptance criteria

- [x] Live Rank grouping creates rank folders from available score data and places unplayed or unknown-score charts in `No Play`.
- [x] At least one in-game canary demonstrates that a chart with score/rank evidence appears in the expected rank folder.
- [x] Rank sorting uses available score facts and keeps unknown/no-score rows distinguishable from confirmed low score states.
- [x] AP/FC and DX-score sorting use available local or online score facts where supported and degrade cleanly when data is unavailable.
- [x] Default folder behavior and list-to-gameplay flow remain functional after rank grouping or score sorting is applied.
- [x] Unit tests cover runtime score facet adaptation, `No Play` fallback, rank grouping, and score-dependent sort degradation.

## Blocked by

- 14-apply-live-map-list-sorting-and-filters

## Comments

- 2026-06-05: Wired live score facts into runtime Rank grouping and score-dependent sorting through a reflection adapter over `ScoreManager.GetScore`, with diagnostic score overrides for deterministic canaries. Added `DX Score` as a Map List sort mode so the issue's DX-score sorting requirement is reachable. Rank folders now use score-derived rank names and keep no-score rows in `No Play`; rank, play count, AP/FC, and DX-score sorts use available score facts while missing facts sort after known facts. Verification: `build.ps1` passed, `test-unit.ps1` passed 120/120, and expanded `test-smoke.ps1` passed against MajdataPlay.
