# Add scene object invariant canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a regression test that inspects the live Unity scene after boot and verifies broad object/component invariants that should survive new mods.

## Acceptance criteria

- [ ] The test verifies at least one active camera and audio listener are present.
- [ ] The test verifies expected UI/event-system objects or canvases are present when the game is idle.
- [ ] The test verifies representative MajdataPlay root/controller objects or components are present when names/types are stable enough.
- [ ] The test uses broad invariants first and avoids brittle exact object-count assertions.

## Blocked by

None - can start immediately

