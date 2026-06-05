Status: completed

# Integration canaries and completion audit

## What to build

Add focused in-game canaries for the completed QoL mod and audit the PRD requirements against current evidence. The canaries should verify that song storage remains nonempty, known local charts remain playable, default folder behavior is intact, grouped catalog behavior is visible, hydration pauses during gameplay, and cache files stay under the mod's own location.

## Acceptance criteria

- [x] Canary verifies built catalog contains default folder behavior, `MyFavorites`, and `Random Recommended`.
- [x] Canary verifies level grouping places a known chart into the expected normalized bucket.
- [x] Canary verifies hydration does not run during gameplay/practice.
- [x] Canary verifies persistent cache files are written only under the mod-owned cache location.
- [x] Canary verifies known local chart list-to-gameplay flow still works.
- [x] Completion audit maps every PRD requirement and issue acceptance criterion to current evidence.
- [x] All unit and practical integration tests pass or any remaining limitation is explicitly documented with evidence.

## Blocked by

- 11-selected-song-metadata-and-status-ui
