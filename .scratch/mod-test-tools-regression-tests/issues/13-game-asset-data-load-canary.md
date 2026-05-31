# Add game asset and data load canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a regression test that verifies MajdataPlay can still load or enumerate known music, chart, config, or asset metadata with candidate mods installed.

## Acceptance criteria

- [ ] The test identifies a stable built-in or local test song/chart/config asset to use as a canary.
- [ ] The test verifies the relevant game database or metadata collection has entries.
- [ ] The test verifies at least one known asset path or metadata record resolves successfully.
- [ ] The test fails on new asset/data load exceptions in logs.

## Blocked by

None - can start immediately

