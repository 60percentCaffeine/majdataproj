# Add core assemblies and game types canary

Status: ready-for-agent

## Parent

.scratch/mod-test-tools-regression-tests/PRD.md

## What to build

Add a regression test that probes the loaded AppDomain from inside MajdataPlay and verifies core game assemblies and important game/system types are still loadable with mods installed.

## Acceptance criteria

- [ ] The test verifies `Assembly-CSharp` or the current core game assembly is loaded.
- [ ] The test verifies representative game manager, input, audio, chart/music, and gameplay-related types can be resolved.
- [ ] The test avoids running destructive type methods or mutating game state.
- [ ] Missing type failures report the missing assembly/type names clearly.

## Blocked by

None - can start immediately

