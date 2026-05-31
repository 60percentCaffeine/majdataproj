# Add test hook contract smoke regression test

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add an integration test that launches MajdataPlay through the harness and verifies the Test Hook Mod readiness file and `/health` response agree on the active test hook contract.

## Acceptance criteria

- [ ] The test launches the real game through the existing harness and shuts it down cleanly.
- [ ] The readiness file exposes non-empty `pid`, `host`, `port`, `startedAt`, and `bridgeVersion` values.
- [ ] `/health` returns `ok:true` plus matching `pid`, `host`, `port`, `bridgeVersion`, `replEnabled`, and `mainThreadDispatcherReady` fields.
- [ ] The test avoids brittle string-only checks where practical by parsing structured JSON.

## Blocked by

None - can start immediately

