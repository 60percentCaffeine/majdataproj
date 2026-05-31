# MajdataPlay Mod Test Project

This repo contains a working MajdataPlay MelonLoader mod setup, a sample mod, and a local test/debug hook for building and testing mods against the real Windows game from WSL.

## Layout

The root folders are split by role: `sample-mod/` is the example mod being built and tested, while `mod-test-tools/` is reusable infrastructure that can test any mod.

- `sample-mod/`: sample MelonLoader mod, pure core library, unit tests, install scripts, and per-mod test commands.
- `mod-test-tools/test-hook-mod/`: in-game HTTP test hook mod with health, eval, persistent session, serialization, and shutdown endpoints.
- `mod-test-tools/repl/`: separate Windows console REPL client for the test hook.
- `mod-test-tools/harness/`: host-side `TestClient` and `TestHarness` helpers for integration tests.
- `mod-test-tools/integration/`: xUnit integration tests that launch the real game.
- `Majdata Hub/game/`: local MajdataPlay install used for runtime testing.
- `references/`: reference source for Majdata, Simai, AquaMai, segatools, and related tools.

## Common Commands

Run the full sample mod test suite:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\test-all.ps1"
```

Run only unit tests with coverage:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\test-unit.ps1"
```

Run real-game integration tests:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\test-integration.ps1"
```

Install the sample mod into the game:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\install.ps1"
```

## Notes

This setup runs from WSL but targets the Windows MajdataPlay game. The current game install uses MelonLoader v0.4.3 with local compatibility patches; see `sample-mod/README.md` for the detailed setup and troubleshooting notes.
