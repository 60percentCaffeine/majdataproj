Status: ready-for-agent

# Virtual folders for Random Recommend and favorites

## What to build

Add `VirtualCollectionFactory` so `Random Recommended` is always available alongside `MyFavorites` in every grouping mode, with selected-info naming and folder tile naming rules matching the UI proposal.

## Acceptance criteria

- [ ] `Random Recommended` is inserted for default folder browsing and alternate grouping modes.
- [ ] `MyFavorites` remains available in every grouping mode.
- [ ] `All` remains part of default folder behavior and is not exposed as a grouping mode.
- [ ] `Random Recommended` exposes tile text as `Random\nRecommended` and selected info text as `Random Recommended`.
- [ ] Tests cover insertion order and availability across all supported grouping modes.

## Blocked by

- 03-level-difficulty-and-grouping-core
