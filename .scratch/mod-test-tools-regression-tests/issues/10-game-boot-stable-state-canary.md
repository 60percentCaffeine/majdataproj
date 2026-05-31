# Add game boot stable state canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a real MajdataPlay regression test that launches with installed mods, waits for a stable idle/title/attract state, and verifies the game is alive rather than only verifying the hook.

## Acceptance criteria

- [ ] The test waits until the active scene and frame counter indicate the game is running.
- [ ] The test verifies key Unity state is sane, including active scene name, frame count advancement, and time scale.
- [ ] The test fails if fatal game or MelonLoader errors appear in logs during boot.
- [ ] The test shuts down and collects artifacts through the existing harness.

## Blocked by

None - can start immediately

