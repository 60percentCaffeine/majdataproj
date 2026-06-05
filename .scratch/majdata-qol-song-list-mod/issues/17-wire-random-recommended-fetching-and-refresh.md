Status: completed

# Wire Random Recommended fetching and refresh

## What to build

Connect the live `Random Recommended` folder to real recommendation data from MajdataNet or configured local fallback. The folder should contain playable recommendation rows when data is available, should refresh through the existing user-facing affordance, and should report recoverable refresh/fetch failures through the upper-screen status style.

This slice should turn `Random Recommended` from an always-present shell folder into a useful live folder without changing the default folder workflow.

## Acceptance criteria

- [x] `Random Recommended` is populated from MajdataNet recommendation/chart-list data when network data is available.
- [x] A configured local fallback can populate `Random Recommended` from local catalog rows when online recommendation fetch fails or is disabled.
- [x] Refreshing recommendations produces a new deterministic-but-varied batch according to the existing recommendation rules.
- [x] Refresh progress, success, and recoverable failure states use the upper-screen status treatment and do not block input.
- [x] The folder remains present in every grouping mode even when recommendations are empty or fetch fails.
- [x] In-game canaries verify populated recommendations, refresh status text, recoverable failure/fallback behavior, and list-to-gameplay flow from a recommended chart when a playable row is available.
- [x] Unit tests and in-game smoke/canaries still pass.

## Blocked by

- 14-apply-live-map-list-sorting-and-filters

## Comments

- 2026-06-05: Wired `Random Recommended` to hold playable rows instead of an empty shell. Runtime refresh attempts MajdataNet chart-list recommendations with bounded wait and hash resolution against live rows, then falls back to deterministic local catalog shuffles when network data fails, is unavailable, or cannot resolve. Refresh diagnostics use the upper-screen status overlay for progress/success/fallback, and forced collection refresh now syncs active list internals so recommended rows can enter gameplay. Verification: `build.ps1` passed, `test-unit.ps1` passed 120/120, and expanded `test-smoke.ps1` passed against MajdataPlay with populated fallback refresh, varied batches, folder presence under grouping, and Random Recommended list-to-gameplay.
