# Add per-mod aggregate test entrypoint

Status: completed

## What to build

Add a stable per-mod aggregate test command for humans and agents that runs unit tests with coverage and integration tests in sequence, then reports failure with a nonzero exit code if either phase fails.

## Acceptance criteria

- [x] `test-all.ps1` invokes the unit-test command and integration-test command.
- [x] Unit-test coverage artifacts remain available after the aggregate command finishes.
- [x] Integration logs remain available after the aggregate command finishes.
- [x] The aggregate command returns nonzero if either unit tests or integration tests fail.

## Blocked by

- .scratch/modtest-env/issues/09-sample-mod-unit-coverage.md
- .scratch/modtest-env/issues/10-sample-integration-test-command.md
