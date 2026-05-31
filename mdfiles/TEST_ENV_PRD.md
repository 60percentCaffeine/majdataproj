# PRD: MajdataPlay Mod Debugging and Testing Environment

## Overview

Build a local debugging and testing environment for MelonLoader mods targeting MajdataPlay. The environment consists of a reusable bridge mod, a separate REPL console client, shared test client/harness libraries, and per-mod test commands.

The bridge mod runs inside MajdataPlay and exposes a local HTTP API that can evaluate Roslyn C# snippets on the Unity main thread. This API is used both by an interactive REPL window and by automated integration tests. Unit tests run outside the game against pure mod logic assemblies.

CI integration is not part of the MVP. The target workflow is deterministic local automation on the developer machine, with WSL orchestrating a Windows game install where needed.

## Goals

- Provide an interactive C# REPL for live debugging inside MajdataPlay.
- Provide an always-on local HTTP bridge for automated game control and invariant checks.
- Allow integration tests to launch the game, wait for bridge readiness, evaluate C# in the running game, collect logs, and shut the game down.
- Allow unit tests to run without launching Unity, MajdataPlay, or MelonLoader.
- Measure unit-test coverage of each mod's pure logic code.
- Keep bridge tooling reusable across multiple mods while each mod owns its own build and test commands.

## Non-Goals

- No CI integration in MVP.
- No in-game overlay REPL or Unity UI for the REPL.
- No structured helper endpoint such as `/invoke`; tests use raw C# eval.
- No public-network security model. The bridge is intended for local development only.
- No requirement to support Unity Test Framework, EditMode, or PlayMode test runners directly.

## Users

- Mod developers debugging MajdataPlay MelonLoader mods interactively.
- Automated test authors writing unit and integration tests for mods.
- Coding agents running local test commands and inspecting full JSON bridge output.

## Architecture

### Bridge Components

- `mod-test-tools/test-hook-mod`: MelonLoader test hook mod loaded by MajdataPlay.
- `mod-test-tools/repl`: separate Windows console REPL client launched on game startup.
- `mod-test-tools/harness/TestClient`: .NET client library for test hook HTTP endpoints.
- `mod-test-tools/harness/TestHarness`: reusable utilities for launching MajdataPlay, waiting for readiness, collecting logs, and shutting down the game.
- `mod-test-tools/test-hook-mod/build.ps1` and `mod-test-tools/test-hook-mod/install.ps1`: test hook mod development scripts.

### Per-Mod Components

Each mod should own its commands and test projects:

- `MyMod/build.ps1`
- `MyMod/test-unit.ps1`
- `MyMod/test-integration.ps1`
- `MyMod/test-all.ps1`
- `src/MyMod.Core`: pure logic assembly.
- `src/MyMod.Melon`: MelonLoader entrypoint and Unity/MajdataPlay adapters.
- `tests/MyMod.UnitTests`: host-side xUnit tests with coverage.
- `tests/MyMod.IntegrationTests`: xUnit tests that launch the game and use the bridge.

The exact directory names may vary per mod, but the split between pure core logic and game-bound adapter code is required for meaningful unit-test coverage.

## Bridge Mod Requirements

### Startup Behavior

- The bridge mod starts automatically when MajdataPlay starts.
- The HTTP server is always enabled while the bridge mod is loaded.
- The HTTP server binds to `127.0.0.1` by default.
- The default port is fixed, recommended `17443`.
- The effective port must be easy to change.
- On startup, the bridge launches or reveals a separate REPL console window by default.
- The REPL is never implemented as an in-game overlay in MVP.

### Configuration

Configuration precedence:

1. Game process CLI args, for example `--modtest-port 17444` or `--modtest-no-repl`.
2. Environment variables, for example `MODTEST_BRIDGE_PORT=17444`.
3. Config file under `UserData/TestHookMod/config.json`.
4. Built-in defaults.

The integration harness may generate a temporary config file before launch when passing CLI args or environment variables through Windows process startup is awkward.

### Readiness File

The bridge writes a readiness file after the HTTP server has started:

`Majdata Hub/game/UserData/TestHookMod/ready.json`

Example:

```json
{
  "pid": 1234,
  "host": "127.0.0.1",
  "port": 17443,
  "startedAt": "2026-06-01T00:00:00Z",
  "bridgeVersion": "0.1.0"
}
```

Integration runners delete stale readiness files before launch, wait for a new file, then poll `/health`.

## HTTP API Requirements

### `GET /health`

Returns bridge status.

Minimum response fields:

```json
{
  "ok": true,
  "bridgeVersion": "0.1.0",
  "pid": 1234,
  "mainThreadDispatcherReady": true
}
```

### `POST /eval`

Evaluates Roslyn C# code inside the running game.

Requirements:

- Runs on Unity's main thread by default.
- Supports async snippets.
- Uses a persistent Roslyn session by default, so REPL variables/imports can carry across requests.
- Exposes a globals object with access to Unity, MelonLoader, loaded game assemblies, bridge utilities, and prior session state.
- Accepts request-level timeout, max-depth, and max-response-size overrides.
- Returns structured compile, execution, and serialization errors.

Example request:

```json
{
  "code": "return UnityEngine.GameObject.Find(\"Foo\") != null;",
  "timeoutMs": 30000,
  "maxDepth": 64,
  "maxResponseBytes": 52428800
}
```

Example response:

```json
{
  "ok": true,
  "result": true,
  "logs": [],
  "durationMs": 12
}
```

### `POST /eval-isolated`

Evaluates Roslyn C# code in a fresh temporary session.

This is useful for tests that must not depend on or mutate persistent REPL state.

### `POST /reset-session`

Clears the persistent Roslyn session.

### `POST /shutdown`

Requests graceful game shutdown after test completion.

If graceful shutdown fails, the integration harness may kill only the process recorded in `ready.json`.

## Roslyn Evaluation Requirements

- Use Roslyn scripting rather than runtime project compilation for MVP.
- Snippets are script-style C# statements/expressions.
- The bridge preloads common references/imports for MajdataPlay, Unity, MelonLoader, and bridge APIs.
- The session model must support REPL-style continuity.
- Integration tests can choose isolated evaluation when continuity is harmful.
- Compilation diagnostics must be returned in structured form.

## Serialization Requirements

`/eval` should fully JSON serialize returned values as far as practical.

Rules:

- Primitive values, strings, arrays, dictionaries, and DTO-shaped objects are serialized normally.
- Object cycles are represented with reference markers rather than causing unbounded recursion.
- Unsupported values such as delegates, pointers, native handles, `IntPtr`, streams, tasks, and reflection/runtime objects are represented with pointer-like identifiers and string conversion where available.
- Unity and arbitrary CLR object graphs should be traversed according to the configured serializer limits.
- Default limits are finite:
  - `timeoutMs`: `30000`
  - `maxDepth`: `64`
  - `maxResponseBytes`: `52428800`
- Limits can be overridden per request.
- Unexpected serialization failures should produce a structured error with `phase: "serialization"`.
- Over-limit responses should produce structured errors rather than hanging the test runner.

Agents and test tooling may run large JSON outputs through separate summarization scripts. The bridge itself should prioritize complete machine-readable output within configured limits.

## REPL Requirements

- The REPL is a separate Windows console client.
- The bridge launches it on game startup unless disabled by configuration.
- The REPL communicates with the in-game bridge through the same HTTP API as tests.
- The REPL uses `/eval` by default and therefore shares persistent session state.
- Minimum REPL commands:
  - `:reset`: call `/reset-session`.
  - `:clear`: clear console output.
  - `:exit`: close the REPL client without shutting down the game.
  - `:help`: show REPL commands.

## Test Mod Requirements

### Unit Tests

- Unit tests run without launching MajdataPlay.
- Unit tests run without Unity or MelonLoader runtime initialization.
- Unit tests use xUnit by default.
- Unit coverage is calculated with coverlet or equivalent .NET coverage tooling.
- Coverage targets the mod's pure core assembly, not generated files or bridge infrastructure.

Recommended command:

```powershell
.\test-unit.ps1
```

Expected behavior:

- Builds pure logic and unit test projects.
- Runs all unit tests.
- Produces human-readable test results.
- Produces coverage output for the mod codebase.

### Integration Tests

- Integration tests use xUnit by default.
- Integration tests launch the real MajdataPlay game.
- Integration tests install the bridge mod and the target mod into `Majdata Hub/game/Mods`.
- Integration tests interact with the game only through bridge HTTP eval.
- Integration tests assert game/mod invariants by evaluating C# snippets.

Recommended command:

```powershell
.\test-integration.ps1
```

Runner lifecycle:

1. Build the bridge, REPL client, test client/harness, and target mod.
2. Install required DLLs into the game directory.
3. Remove stale readiness and log artifacts.
4. Launch `start-controller.bat` from `Majdata Hub/game`.
5. Wait for `UserData/TestHookMod/ready.json`.
6. Poll `/health` until ready.
7. Run integration tests through `/eval` and `/eval-isolated`.
8. Call `/shutdown`.
9. If shutdown fails, kill only the PID recorded in `ready.json`.
10. Collect MelonLoader/game logs into test artifacts.

### All Tests

Recommended command:

```powershell
.\test-all.ps1
```

Expected behavior:

- Runs unit tests with coverage.
- Runs integration tests.
- Returns nonzero exit code if either test phase fails.

## WSL and Windows Constraints

- Development commands may be invoked from WSL, but the game runs on Windows.
- Existing project guidance for launching batch files applies:

```powershell
powershell.exe -Command "Start-Process '.\start-controller.bat'"
```

- The integration harness must launch from the game directory so game-relative paths behave correctly.
- Scripts should avoid assumptions that only work from an interactive PowerShell session.

## Success Criteria

- Starting MajdataPlay with the bridge mod installed opens a separate REPL console.
- A developer can evaluate C# from the REPL and inspect live game state.
- `GET /health` reports readiness after game startup.
- `POST /eval` can run a Unity main-thread snippet and return JSON.
- A sample integration test can launch the game, wait for readiness, evaluate an invariant, and shut the game down.
- A sample unit test project can run without launching the game.
- Unit-test coverage can be generated for a pure mod core assembly.
- Per-mod `test-unit.ps1`, `test-integration.ps1`, and `test-all.ps1` commands provide stable entrypoints for humans and agents.

## Risks and Open Questions

- Roslyn compatibility with MajdataPlay's Mono/MelonLoader environment must be verified. The bridge may need carefully selected Roslyn assemblies and dependency loading.
- Full JSON serialization of arbitrary Unity/game object graphs may be expensive or fragile. Serializer limits and reference handling are required.
- Main-thread eval must avoid deadlocks when snippets await tasks that require Unity update ticks.
- Game launch/shutdown behavior may need process-discovery safeguards because startup is mediated by Windows batch files.
- Existing MelonLoader compatibility patches disable some MelonLoader/Harmony functionality. The bridge implementation must account for the currently working loader profile.
