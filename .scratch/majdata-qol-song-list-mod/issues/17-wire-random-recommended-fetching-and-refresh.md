Status: ready-for-agent

# Wire Random Recommended fetching and refresh

## What to build

Connect the live `Random Recommended` folder to real recommendation data from MajdataNet or configured local fallback. The folder should contain playable recommendation rows when data is available, should refresh through the existing user-facing affordance, and should report recoverable refresh/fetch failures through the upper-screen status style.

This slice should turn `Random Recommended` from an always-present shell folder into a useful live folder without changing the default folder workflow.

## Acceptance criteria

- [ ] `Random Recommended` is populated from MajdataNet recommendation/chart-list data when network data is available.
- [ ] A configured local fallback can populate `Random Recommended` from local catalog rows when online recommendation fetch fails or is disabled.
- [ ] Refreshing recommendations produces a new deterministic-but-varied batch according to the existing recommendation rules.
- [ ] Refresh progress, success, and recoverable failure states use the upper-screen status treatment and do not block input.
- [ ] The folder remains present in every grouping mode even when recommendations are empty or fetch fails.
- [ ] In-game canaries verify populated recommendations, refresh status text, recoverable failure/fallback behavior, and list-to-gameplay flow from a recommended chart when a playable row is available.
- [ ] Unit tests and in-game smoke/canaries still pass.

## Blocked by

- 14-apply-live-map-list-sorting-and-filters
