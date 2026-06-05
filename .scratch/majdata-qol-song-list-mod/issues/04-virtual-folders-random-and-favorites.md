Status: completed

# Virtual folders for Random Recommend and favorites

## What to build

Add `VirtualCollectionFactory` so `Random Recommended` is always available alongside `MyFavorites` in every grouping mode, with selected-info naming and folder tile naming rules matching the UI proposal.

## Acceptance criteria

- [x] `Random Recommended` is inserted for default folder browsing and alternate grouping modes.
- [x] `MyFavorites` remains available in every grouping mode.
- [x] `All` remains part of default folder behavior and is not exposed as a grouping mode.
- [x] `Random Recommended` exposes tile text as `Random\nRecommended` and selected info text as `Random Recommended`.
- [x] Tests cover insertion order and availability across all supported grouping modes.

## Blocked by

- 03-level-difficulty-and-grouping-core

## Comments

- 2026-06-05: Implemented virtual collection metadata and `VirtualCollectionFactory`. `CatalogNavigator` now ensures `MyFavorites` and `Random Recommended` are available in default folder browsing and every alternate grouping mode. `Random Recommended` uses tile text `Random\nRecommended`, selected-info text `Random Recommended`, is marked virtual, and is online-backed. Verification: `test-unit.ps1` passed 45/45 tests; `build.ps1` passed; `install.ps1` installed the updated mod/core DLLs.
