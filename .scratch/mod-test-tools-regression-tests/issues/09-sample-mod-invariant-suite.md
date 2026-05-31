# Expand sample mod invariant regression tests

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Expand the current sample mod integration coverage into a small invariant suite that proves the sample mod assembly loads, expected types are discoverable, core behavior works, and startup did not log failures.

## Acceptance criteria

- [ ] The suite verifies the sample mod assembly is loaded inside MajdataPlay.
- [ ] The suite verifies at least one expected sample mod type is discoverable through reflection.
- [ ] The suite verifies core sample mod behavior still returns expected values.
- [ ] The suite checks collected logs for sample-mod startup failures or unhandled exceptions.

## Blocked by

None - can start immediately

