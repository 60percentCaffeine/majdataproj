# Add structured eval serialization regression test

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add an integration test that returns primitive values, arrays, dictionaries, and simple DTO-shaped objects from eval and verifies their JSON shape remains structured and usable.

## Acceptance criteria

- [ ] The test covers at least one primitive value, array, dictionary, and DTO-shaped object.
- [ ] Assertions parse the eval response JSON and validate result shape and values.
- [ ] The test fails if values degrade to opaque strings when structured JSON should be available.

## Blocked by

None - can start immediately

