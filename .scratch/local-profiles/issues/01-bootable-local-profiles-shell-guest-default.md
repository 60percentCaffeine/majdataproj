Status: completed

# Bootable local profiles shell with Guest session default

## Parent

.scratch/local-profiles/PRD.md

## What to build

Create the first bootable local-profiles slice without changing player-visible behavior yet. The game/mod should have a testable active-player session model and local-profile metadata store, but the active mode should default to Guest and preserve current Guest behavior until later UI slices select a different mode.

This slice should make the rest of the feature possible while staying safe: no Login Screen redesign, no profile selection UI, no profile data migration, and no changes to Guest storage semantics.

## Acceptance criteria

- [x] The active player session defaults to `Guest` with no selected local profile and a Guest save target.
- [x] The active player session exposes a simple testable API for Guest, Local Profile, and majdata.net Account modes, even if only Guest is reachable from UI in this slice.
- [x] The local profile store can list profiles on a machine when none exist and returns an empty saved-profile list without creating unintended data.
- [x] Guest score, favorite, and settings behavior remains unchanged when no local profile has been selected.
- [x] Startup diagnostics or an equivalent test seam can report the current active player mode for integration tests.
- [x] Unit tests cover the session default, basic mode transitions, empty local-profile list behavior, and Guest preservation.
- [x] A real-game smoke test proves MajdataPlay still boots to the current flow without fatal logs or unexpected UI replacement.

## Blocked by

None - can start immediately.

## Comments

- 2026-06-08: Implemented the bootable local profiles shell with a default Guest `ActivePlayerSession`, local profile metadata listing, guest-preserving storage route seam, startup/eval diagnostics, focused unit coverage, and a real-game local-profiles boot smoke test.
