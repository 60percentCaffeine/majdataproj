Status: ready-for-agent

# Wire website collections into live folder carousel

## What to build

Fetch and resolve MajdataNet owned and subscribed/favorited website collections into live folder tiles in the existing folder carousel. Collection folders should prefer local downloaded charts when duplicate hashes exist, include online-only charts when online scope allows it, and communicate resolved-versus-total counts in the selected folder info.

This slice should make website collection support visible and playable in the real list UI while preserving default folder behavior and clean degradation when online data is unavailable.

## Acceptance criteria

- [ ] Owned website collections and subscribed/favorited website collections appear as folder tiles when account/network data is available.
- [ ] Collection contents resolve by hash against local-only, online-only, and duplicate local-plus-online catalog rows.
- [ ] Local downloaded rows are preferred for duplicate hashes while online metadata remains attached for collection context.
- [ ] Online-only collection rows are included or hidden/degraded according to downloaded/online scope settings.
- [ ] Selected folder info communicates resolved count versus total count when unresolved collection entries exist.
- [ ] Network failures are recoverable, retain stale cached collection data when available, and use the upper-screen status treatment for non-fatal notices.
- [ ] In-game canaries verify at least one resolved collection folder, resolved-versus-total count behavior, online-scope degradation, and list-to-gameplay flow from a collection row when a playable row is available.
- [ ] Unit tests and in-game smoke/canaries still pass.

## Blocked by

- 14-apply-live-map-list-sorting-and-filters
