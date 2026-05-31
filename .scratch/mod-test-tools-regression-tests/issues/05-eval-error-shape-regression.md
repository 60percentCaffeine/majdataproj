# Add eval error shape regression test

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add integration tests that verify eval reports stable, useful error shapes for compilation errors, runtime exceptions, and timeouts.

## Acceptance criteria

- [ ] Invalid C# syntax returns a structured compilation failure.
- [ ] A thrown exception returns a structured execution failure.
- [ ] A deliberately slow snippet with a small timeout returns a structured timeout failure.
- [ ] Assertions avoid exact compiler/runtime text except for broad evidence that the relevant error was surfaced.

## Blocked by

None - can start immediately

