# Create reusable bridge TestClient and TestHarness

Status: completed

## What to build

Create reusable host-side libraries for integration tests that launch MajdataPlay, wait for bridge readiness, call bridge HTTP endpoints, collect game logs, and shut the game down safely from WSL-driven workflows.

## Acceptance criteria

- [x] TestClient wraps `/health`, `/eval`, `/eval-isolated`, `/reset-session`, and `/shutdown`.
- [x] TestHarness launches `start-controller.bat` from the game directory so game-relative paths work.
- [x] The harness deletes stale readiness/log artifacts before launch, waits for a fresh readiness file, then polls `/health`.
- [x] The harness can collect MelonLoader/game logs into test artifacts.
- [x] If graceful shutdown fails, the harness kills only the process recorded in the readiness file.

## Blocked by

- .scratch/modtest-env/issues/01-bridge-health-readiness.md
- .scratch/modtest-env/issues/03-isolated-roslyn-eval.md
