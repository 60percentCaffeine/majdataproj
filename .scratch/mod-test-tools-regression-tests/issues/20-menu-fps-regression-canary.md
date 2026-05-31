# Add menu FPS regression canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a menu/idle FPS regression test that samples Unity frame timing from inside MajdataPlay while the game is in a stable menu/title/attract state.

## Acceptance criteria

- [ ] The test waits for the boot stable state before sampling.
- [ ] The test samples `Time.unscaledDeltaTime` or an equivalent Unity frame-timing source for a fixed window.
- [ ] The test reports average FPS, p95 frame time, max frame time, frame count, and dropped-frame ratio.
- [ ] The test compares results to a local baseline or conservative default thresholds and fails on meaningful regression.

## Blocked by

.scratch/mod-test-tools-regression-tests/issues/10-game-boot-stable-state-canary.md

