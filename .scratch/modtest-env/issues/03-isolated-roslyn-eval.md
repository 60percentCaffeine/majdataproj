# Implement isolated Roslyn eval on Unity main thread

Status: ready-for-agent

## What to build

Add an isolated C# evaluation endpoint that accepts script-style Roslyn snippets over HTTP, runs them on Unity's main thread by default, supports async snippets, and returns structured JSON for success, compilation errors, and execution errors.

## Acceptance criteria

- [ ] `POST /eval-isolated` accepts code, timeoutMs, maxDepth, and maxResponseBytes fields.
- [ ] Snippets can access Unity, MelonLoader, loaded game assemblies, bridge utilities, and bridge-provided globals.
- [ ] Evaluation runs on the Unity main thread by default.
- [ ] Async snippets are supported without deadlocking the Unity update loop.
- [ ] Compilation and execution failures return structured errors without crashing the game.

## Blocked by

- .scratch/modtest-env/issues/01-bridge-health-readiness.md
