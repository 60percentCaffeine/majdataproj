# Add bridge configuration precedence

Status: completed

## What to build

Allow the bridge runtime settings to be controlled deterministically by game process CLI args, environment variables, config file, and built-in defaults, in that precedence order. The effective host, port, and REPL setting should be observable through bridge startup behavior and readiness/health output.

## Acceptance criteria

- [x] CLI args such as `--modtest-port` and `--modtest-no-repl` override every other source.
- [x] Environment variables such as `MODTEST_BRIDGE_PORT` override config-file and default values.
- [x] `UserData/ModTestBridge/config.json` is read when higher-precedence sources are absent.
- [x] Built-in defaults bind to `127.0.0.1` on port `17443` with REPL launch enabled.
- [x] Invalid configuration fails visibly without silently starting on the wrong port.

## Blocked by

- .scratch/modtest-env/issues/01-bridge-health-readiness.md
