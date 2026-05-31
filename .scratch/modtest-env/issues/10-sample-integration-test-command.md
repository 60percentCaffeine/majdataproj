# Add sample integration test command for real MajdataPlay

Status: ready-for-agent

## What to build

Add a sample integration test command that builds and installs the bridge plus sample mod, launches the real MajdataPlay game, waits for bridge readiness, asserts a game/mod invariant through eval, shuts down, and collects logs.

## Acceptance criteria

- [ ] `test-integration.ps1` builds the bridge, REPL client, TestClient/TestHarness, and sample mod.
- [ ] The command installs required DLLs into `Majdata Hub/game/Mods`.
- [ ] The command launches `start-controller.bat`, waits for readiness, and polls `/health`.
- [ ] xUnit integration tests assert at least one invariant through `/eval` or `/eval-isolated`.
- [ ] The command requests shutdown, applies the safe PID fallback if needed, collects logs, and returns nonzero on failure.

## Blocked by

- .scratch/modtest-env/issues/05-bounded-json-serialization.md
- .scratch/modtest-env/issues/07-test-client-harness.md
- .scratch/modtest-env/issues/08-bridge-shutdown-endpoint.md
