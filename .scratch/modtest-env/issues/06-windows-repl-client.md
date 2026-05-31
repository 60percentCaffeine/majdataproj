# Build separate Windows REPL client

Status: completed

## What to build

Build a separate Windows console REPL client that talks to the in-game bridge over the same HTTP API used by tests. The bridge should launch or reveal the REPL on game startup unless configuration disables it.

## Acceptance criteria

- [x] The REPL runs as a separate Windows console client, not as an in-game overlay.
- [x] Bridge startup launches or reveals the REPL by default.
- [x] REPL input evaluates through `/eval` and shares persistent session state.
- [x] `:reset`, `:clear`, `:exit`, and `:help` commands are implemented.
- [x] Disabling REPL startup through bridge configuration leaves the HTTP bridge available.

## Blocked by

- .scratch/modtest-env/issues/04-persistent-eval-session.md
