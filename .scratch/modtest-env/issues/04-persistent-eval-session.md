# Add persistent eval session resettable through HTTP

Status: completed

## What to build

Add REPL-style C# evaluation backed by a persistent Roslyn session, plus an endpoint to reset that session. Developers should be able to carry variables and imports across requests, while tests can clear state before running.

## Acceptance criteria

- [x] `POST /eval` evaluates code using a persistent Roslyn session by default.
- [x] Variables/imports created by one `/eval` request can be used by a later `/eval` request.
- [x] `POST /reset-session` clears the persistent session and returns structured success or error JSON.
- [x] `/eval` and `/eval-isolated` remain behaviorally distinct and covered by tests.

## Blocked by

- .scratch/modtest-env/issues/03-isolated-roslyn-eval.md
