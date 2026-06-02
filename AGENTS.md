this project is for making MelonLoader mods for MajdataPlay, a Simai clone
"references" folder contains reference source code for Majdata, Simai and some other tools that can be useful: AquaMai is a MelonLoader mod that adds various features to Simai, and segatools is a hook that adds various features to Simai and other games

a MajdataPlay setup can be found in Majdata Hub/game/start-controller.bat

bat files can be run with
```
powershell.exe -Command "Start-Process '.\start-controller.bat'"
```

you are running in WSL but the game/mod is for Windows

## In-game debugging and testing

Use `mod-test-tools/test-hook-mod` when you need to execute code inside
MajdataPlay for debugging or integration testing. It installs a MelonLoader
HTTP test hook with eval endpoints; `mod-test-tools/harness` and
`mod-test-tools/repl` provide host-side helpers for driving it from tests or a
Windows REPL.

## Screenshot capture

For DPI-aware screenshots of the original game on DISPLAY2, see
`docs/capturing-original-game-display2.md`.

## Agent skills

### Issue tracker

Issues and PRDs are tracked as local markdown files under `.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

The local markdown tracker uses the default skill triage labels, including `ready-for-agent` for AFK-ready issues. See `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repo; read root `CONTEXT.md` and `docs/adr/` when present. See `docs/agents/domain.md`.
