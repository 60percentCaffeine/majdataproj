# Add song FPS regression canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a song/gameplay FPS regression test that starts one deterministic chart and samples frame timing during active gameplay.

## Acceptance criteria

- [ ] The test starts the same known song, chart, difficulty, window settings, and gameplay mode every run.
- [ ] Sampling begins only after the play scene/gameplay state is active.
- [ ] The test reports average FPS, p95 frame time, max frame time, frame count, and dropped-frame ratio for the song window.
- [ ] The test compares results to a song baseline or conservative default thresholds and fails on meaningful regression.

## Blocked by

.scratch/mod-test-tools-regression-tests/issues/18-gameplay-dry-run-canary.md
