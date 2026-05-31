# Add input mapping canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a regression test that verifies MajdataPlay's keyboard/touch/input configuration and runtime input components remain initialized with candidate mods installed.

## Acceptance criteria

- [ ] The test verifies input configuration or key-binding data loads successfully.
- [ ] The test verifies representative input/touch manager components are alive after boot.
- [ ] The test fails on hardware/input initialization errors in logs.
- [ ] The test does not require physical touch-panel hardware.

## Blocked by

None - can start immediately

