Status: completed

# Wire website collections into live folder carousel

## What to build

Fetch and resolve MajdataNet owned and subscribed/favorited website collections into live folder tiles in the existing folder carousel. Collection folders should prefer local downloaded charts when duplicate hashes exist, include online-only charts when online scope allows it, and communicate resolved-versus-total counts in the selected folder info.

This slice should make website collection support visible and playable in the real list UI while preserving default folder behavior and clean degradation when online data is unavailable.

## Acceptance criteria

- [x] Owned website collections and subscribed/favorited website collections appear as folder tiles when account/network data is available.
- [x] Collection contents resolve by hash against local-only, online-only, and duplicate local-plus-online catalog rows.
- [x] Local downloaded rows are preferred for duplicate hashes while online metadata remains attached for collection context.
- [x] Online-only collection rows are included or hidden/degraded according to downloaded/online scope settings.
- [x] Selected folder info communicates resolved count versus total count when unresolved collection entries exist.
- [x] Network failures are recoverable, retain stale cached collection data when available, and use the upper-screen status treatment for non-fatal notices.
- [x] In-game canaries verify at least one resolved collection folder, resolved-versus-total count behavior, online-scope degradation, and list-to-gameplay flow from a collection row when a playable row is available.
- [x] Unit tests and in-game smoke/canaries still pass.

## Blocked by

- 14-apply-live-map-list-sorting-and-filters

## Completion notes

- Added a runtime website collection registry that injects collection folders into the live carousel before Random Recommended.
- Added hash resolution against live `SongStorage` rows with local-row preference for duplicate local-plus-online hashes, and current downloaded/online scope filtering.
- Added selected folder status text with resolved-versus-total counts for website collections.
- Added recoverable failure handling that keeps cached collection data and reports the retained-cache status through the upper-screen status overlay.
- Expanded the smoke canary to install a diagnostic website collection, verify mixed/downloaded/online scope behavior, verify retained cached data after a simulated failure, and enter gameplay from a resolved website collection row.
- Verification: `build.ps1` passed; `test-unit.ps1` passed 120/120; expanded `test-smoke.ps1` passed against MajdataPlay.
