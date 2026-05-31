# Add enter and exit flow canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a shallow navigation regression test that moves MajdataPlay from the idle/menu state into a next expected state and back out using stable game APIs or deterministic input simulation.

## Acceptance criteria

- [ ] The test starts from the boot stable state and records the initial scene/game state.
- [ ] The test triggers a stable entry/start action through game APIs or deterministic input.
- [ ] The test verifies the scene or state machine advances to an expected non-idle state.
- [ ] The test exits or returns to a clean state before shutdown.

## Blocked by

.scratch/mod-test-tools-regression-tests/issues/10-game-boot-stable-state-canary.md

