# ModTestBridge Readiness

`ModTestBridge` writes its readiness file after the loopback HTTP listener has successfully bound:

```text
Majdata Hub/game/UserData/ModTestBridge/ready.json
```

The file contains `pid`, `host`, `port`, `startedAt`, and `bridgeVersion`. Integration runners must delete any existing readiness file before launching MajdataPlay, then wait for a newly-created file whose `pid` still exists and whose `startedAt` is after the launch attempt began. A readiness file left behind by a crashed or force-killed game process is stale and must not be trusted.

The default listener is `127.0.0.1:17443` with REPL launch enabled. Bridge configuration is applied in this order, from lowest to highest precedence:

1. Built-in defaults.
2. `Majdata Hub/game/UserData/ModTestBridge/config.json`.
3. Environment variables: `MODTEST_BRIDGE_HOST`, `MODTEST_BRIDGE_PORT`, `MODTEST_BRIDGE_REPL`.
4. Game process command-line arguments: `--modtest-host`, `--modtest-port`, `--modtest-repl`, `--modtest-no-repl`.

Example `config.json`:

```json
{
  "host": "127.0.0.1",
  "port": 17443,
  "replEnabled": true
}
```

Invalid configuration fails bridge startup visibly in the MelonLoader log and does not write a fresh readiness file. Supported boolean values include `true`, `false`, `1`, `0`, `yes`, `no`, `on`, and `off`.

After reading a fresh readiness file, runners should poll:

```text
GET http://127.0.0.1:17443/health
```

`/health` returns structured JSON with `ok`, `bridgeVersion`, `pid`, `host`, `port`, `replEnabled`, and `mainThreadDispatcherReady`.
