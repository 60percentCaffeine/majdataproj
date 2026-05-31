# Add test hook config and launch options regression test

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a focused regression test for Test Hook Mod configuration behavior, including a minimal harness option path if needed to launch with command-line arguments or environment/config overrides.

## Acceptance criteria

- [ ] The harness can launch MajdataPlay with a non-default test-hook configuration without breaking existing callers.
- [ ] The test verifies at least one supported config path, such as disabled REPL or non-default port.
- [ ] The test cleans up any config file, environment variable, or launched process side effects.
- [ ] Compatibility paths under `UserData/TestHookMod` are preserved.

## Blocked by

None - can start immediately

