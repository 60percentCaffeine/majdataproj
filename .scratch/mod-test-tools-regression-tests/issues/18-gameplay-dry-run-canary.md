# Add gameplay dry-run canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a deterministic gameplay canary that starts one known song/chart in a safe mode and verifies the play scene, note/judge/score systems, and song progress advance without fatal errors.

## Acceptance criteria

- [ ] The test starts one known short chart through stable game APIs, debug mode, autoplay, or deterministic navigation.
- [ ] The test verifies the play scene or gameplay state becomes active.
- [ ] The test verifies note, judge, score, or progress objects initialize and time advances.
- [ ] The test does not require exact score assertions unless deterministic autoplay/input is available.

## Blocked by

.scratch/mod-test-tools-regression-tests/issues/14-chart-parse-canary.md
.scratch/mod-test-tools-regression-tests/issues/17-enter-exit-flow-canary.md

