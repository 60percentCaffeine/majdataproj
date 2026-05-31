# Add test hook shutdown regression test

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add an integration test that verifies `/shutdown` accepts the request, reports the fallback PID, and the MajdataPlay readiness PID exits.

## Acceptance criteria

- [ ] The test calls `/shutdown` directly and parses the structured response.
- [ ] The response includes accepted shutdown state and the readiness PID/fallback PID expected by the harness.
- [ ] The test waits for the game process to exit or applies the existing safe PID fallback.
- [ ] The test leaves no `MajdataPlay` or `ModTestReplClient` process running.

## Blocked by

None - can start immediately

