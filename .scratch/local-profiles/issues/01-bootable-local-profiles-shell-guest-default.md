Status: ready-for-agent

# Bootable local profiles shell with Guest session default

## Parent

.scratch/local-profiles/PRD.md

## What to build

Create the first bootable local-profiles slice without changing player-visible behavior yet. The game/mod should have a testable active-player session model and local-profile metadata store, but the active mode should default to Guest and preserve current Guest behavior until later UI slices select a different mode.

This slice should make the rest of the feature possible while staying safe: no Login Screen redesign, no profile selection UI, no profile data migration, and no changes to Guest storage semantics.

## Acceptance criteria

- [ ] The active player session defaults to `Guest` with no selected local profile and a Guest save target.
- [ ] The active player session exposes a simple testable API for Guest, Local Profile, and majdata.net Account modes, even if only Guest is reachable from UI in this slice.
- [ ] The local profile store can list profiles on a machine when none exist and returns an empty saved-profile list without creating unintended data.
- [ ] Guest score, favorite, and settings behavior remains unchanged when no local profile has been selected.
- [ ] Startup diagnostics or an equivalent test seam can report the current active player mode for integration tests.
- [ ] Unit tests cover the session default, basic mode transitions, empty local-profile list behavior, and Guest preservation.
- [ ] A real-game smoke test proves MajdataPlay still boots to the current flow without fatal logs or unexpected UI replacement.

## Blocked by

None - can start immediately.
