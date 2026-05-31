# Split sample mod into core, Melon adapter, and unit tests with coverage

Status: ready-for-agent

## What to build

Create a sample mod structure that separates pure mod logic from MelonLoader and Unity adapter code, then add a stable unit-test command that runs outside the game and reports coverage for the pure core assembly.

## Acceptance criteria

- [ ] The sample mod has a pure core assembly that can build without launching Unity, MajdataPlay, or MelonLoader runtime initialization.
- [ ] The MelonLoader entrypoint delegates game-bound behavior through adapter code.
- [ ] xUnit unit tests cover the pure core assembly.
- [ ] `test-unit.ps1` builds the relevant projects, runs all unit tests, and produces human-readable results.
- [ ] Coverage output targets mod core logic and excludes generated files and bridge infrastructure.

## Blocked by

None - can start immediately
