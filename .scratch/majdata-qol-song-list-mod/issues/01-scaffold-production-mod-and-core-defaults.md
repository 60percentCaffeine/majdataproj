Status: completed

# Scaffold production mod and core defaults

## What to build

Create a production `majdata-qol-song-list-mod` MelonLoader mod with a Unity-free core library and focused unit tests. The first runnable behavior should be conservative: installing the mod must not change MajdataPlay's default folder browsing, while the core exposes the map-list settings defaults and supported option values from the PRD/UI proposal.

## Acceptance criteria

- [ ] The repo has a buildable production mod project, distinct from prototype scratch scripts.
- [ ] The mod installs into the MajdataPlay `Mods` directory with its core support assembly under a mod-owned library folder.
- [ ] Startup logs prove the mod and core defaults load without covering or replacing the game's UI.
- [ ] The pure core exposes map-list settings defaults: grouping `Default`, sorting `Default`, difficulty filter `No`, downloaded songs filter `Mixed`.
- [ ] Unit tests cover the defaults and supported option sets, including that `Version` is not a grouping mode and `All` is not its own grouping mode.
- [ ] Build/install scripts and a unit test script are present and pass.

## Blocked by

None - can start immediately.

## Comments

- 2026-06-05: Implemented production mod scaffold, Unity-free core defaults, build/install/test scripts, and runtime startup logging. Verified unit tests, serial build, install, and a short MajdataPlay launch that logged the core defaults and default folder behavior preservation.
