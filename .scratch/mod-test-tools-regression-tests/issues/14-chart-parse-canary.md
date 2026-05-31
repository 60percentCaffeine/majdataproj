# Add chart parse canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a regression test that parses one known-good chart through MajdataPlay's normal chart loading/parsing path and verifies rough musical/gameplay invariants.

## Acceptance criteria

- [ ] The test selects one deterministic known-good chart from the local install or test fixture.
- [ ] The chart parser/loader succeeds without fatal errors.
- [ ] The parsed chart has nonzero notes and timing/BPM data.
- [ ] Assertions tolerate harmless metadata differences while catching parser/load failures.

## Blocked by

.scratch/mod-test-tools-regression-tests/issues/13-game-asset-data-load-canary.md

