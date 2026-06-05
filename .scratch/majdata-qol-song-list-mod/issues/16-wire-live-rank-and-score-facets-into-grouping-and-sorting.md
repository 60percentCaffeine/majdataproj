Status: ready-for-agent

# Wire live rank and score facets into grouping and sorting

## What to build

Replace the live Rank grouping placeholder with real score/rank buckets and make score-dependent sorting use available local or online score facts. The player should be able to browse played and unplayed charts by meaningful rank state instead of seeing every chart in `No Play`.

This slice should preserve the existing no-score fallback behavior: charts without score evidence remain discoverable under `No Play`, and unavailable online score data degrades cleanly rather than being treated as confirmed zero.

## Acceptance criteria

- [ ] Live Rank grouping creates rank folders from available score data and places unplayed or unknown-score charts in `No Play`.
- [ ] At least one in-game canary demonstrates that a chart with score/rank evidence appears in the expected rank folder.
- [ ] Rank sorting uses available score facts and keeps unknown/no-score rows distinguishable from confirmed low score states.
- [ ] AP/FC and DX-score sorting use available local or online score facts where supported and degrade cleanly when data is unavailable.
- [ ] Default folder behavior and list-to-gameplay flow remain functional after rank grouping or score sorting is applied.
- [ ] Unit tests cover runtime score facet adaptation, `No Play` fallback, rank grouping, and score-dependent sort degradation.

## Blocked by

- 14-apply-live-map-list-sorting-and-filters
