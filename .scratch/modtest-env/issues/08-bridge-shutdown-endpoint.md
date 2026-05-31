# Add graceful bridge shutdown endpoint

Status: ready-for-agent

## What to build

Add an HTTP endpoint that lets integration tests request graceful MajdataPlay shutdown after test completion, with behavior clear enough for the harness to fall back to killing only the readiness-file PID if needed.

## Acceptance criteria

- [ ] `POST /shutdown` requests graceful game shutdown from inside the running game.
- [ ] The endpoint returns structured JSON describing whether shutdown was accepted or failed.
- [ ] Shutdown behavior is safe to call at the end of integration tests.
- [ ] Harness fallback expectations are documented with the readiness-file PID.

## Blocked by

- .scratch/modtest-env/issues/01-bridge-health-readiness.md
