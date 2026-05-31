# Create bridge mod health endpoint and readiness file

Status: ready-for-agent

## What to build

Build the first end-to-end bridge path: when MajdataPlay starts with the bridge mod installed, the mod starts a loopback HTTP server, writes `UserData/ModTestBridge/ready.json`, and responds to `GET /health` with bridge status.

## Acceptance criteria

- [ ] Starting MajdataPlay with the bridge mod installed starts an HTTP server bound to `127.0.0.1`.
- [ ] The bridge writes a fresh readiness file containing pid, host, port, startedAt, and bridgeVersion after the HTTP server starts.
- [ ] `GET /health` returns structured JSON including ok, bridgeVersion, pid, and mainThreadDispatcherReady.
- [ ] Stale readiness-file behavior is documented for integration runners.

## Blocked by

None - can start immediately
