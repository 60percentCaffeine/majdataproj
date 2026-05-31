# Add persistent eval session regression test

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add an integration test that verifies `/eval` maintains replayed session state, `/eval-isolated` remains isolated from that state, and `/reset-session` clears persistent eval state.

## Acceptance criteria

- [ ] The test proves a variable or import created through `/eval` is visible to a later `/eval` call.
- [ ] The test proves `/eval-isolated` cannot observe `/eval` session state.
- [ ] The test calls `/reset-session` and proves the prior `/eval` state is no longer available.
- [ ] Failures report which eval mode or session transition regressed.

## Blocked by

None - can start immediately

