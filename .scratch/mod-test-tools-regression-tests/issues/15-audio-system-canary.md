# Add audio system canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a regression test that verifies MajdataPlay's Unity/game audio system remains initialized and sane with candidate mods installed.

## Acceptance criteria

- [ ] The test verifies an audio listener and representative audio manager/source state exist after boot.
- [ ] The test verifies relevant volume, mixer, or mute state is within sane bounds.
- [ ] If a known short clip can be loaded safely, the test verifies it loads without requiring loud playback.
- [ ] The test fails on new audio initialization errors in logs.

## Blocked by

None - can start immediately

