# ModTestBridge Readiness

`ModTestBridge` writes its readiness file after the loopback HTTP listener has successfully bound:

```text
Majdata Hub/game/UserData/ModTestBridge/ready.json
```

The file contains `pid`, `host`, `port`, `startedAt`, and `bridgeVersion`. Integration runners must delete any existing readiness file before launching MajdataPlay, then wait for a newly-created file whose `pid` still exists and whose `startedAt` is after the launch attempt began. A readiness file left behind by a crashed or force-killed game process is stale and must not be trusted.

After reading a fresh readiness file, runners should poll:

```text
GET http://127.0.0.1:17443/health
```

`/health` returns structured JSON with `ok`, `bridgeVersion`, `pid`, and `mainThreadDispatcherReady`.
