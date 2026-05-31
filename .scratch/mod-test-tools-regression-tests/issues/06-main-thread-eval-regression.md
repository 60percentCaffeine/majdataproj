# Add Unity main-thread eval regression test

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add an integration test that proves eval snippets run on a Unity-safe main-thread path by reading stable Unity state from inside MajdataPlay.

## Acceptance criteria

- [ ] The test performs at least one Unity API read that would be unsafe or invalid from a background-only context.
- [ ] The test verifies the game continues advancing after the probe.
- [ ] The test documents the selected Unity/game invariant in the test name or assertion message.

## Blocked by

None - can start immediately

