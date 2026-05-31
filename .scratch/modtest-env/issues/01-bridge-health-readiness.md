# Create bridge mod health endpoint and readiness file

Status: completed

## What to build

Build the first end-to-end bridge path: when MajdataPlay starts with the bridge mod installed, the mod starts a loopback HTTP server, writes `UserData/ModTestBridge/ready.json`, and responds to `GET /health` with bridge status.

## Acceptance criteria

- [x] Starting MajdataPlay with the bridge mod installed starts an HTTP server bound to `127.0.0.1`.
- [x] The bridge writes a fresh readiness file containing pid, host, port, startedAt, and bridgeVersion after the HTTP server starts.
- [x] `GET /health` returns structured JSON including ok, bridgeVersion, pid, and mainThreadDispatcherReady.
- [x] Stale readiness-file behavior is documented for integration runners.

## Blocked by

None - can start immediately
