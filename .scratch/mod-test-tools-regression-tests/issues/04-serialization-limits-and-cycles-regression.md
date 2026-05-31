# Add serialization limits and cycles regression test

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add an integration test that verifies bounded eval serialization handles cycles and reports serialization failures for depth and response-size limits.

## Acceptance criteria

- [ ] The test returns a cyclic object graph and verifies `$id`/`$ref` style cycle markers are present.
- [ ] The test requests a too-small `maxDepth` and verifies the response fails with `phase:"serialization"`.
- [ ] The test requests a too-small `maxResponseBytes` and verifies the response fails with `phase:"serialization"`.
- [ ] The test does not depend on exact exception wording.

## Blocked by

None - can start immediately

