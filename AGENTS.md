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

For screenshots of the original game, use the TestHookMod + Unity
`ScreenCapture.CaptureScreenshot` workflow. See
`docs/capturing-majdataplay-screenshots.md`.

## UI Guidelines

The UI should be mainly controlled with physical buttons like the current game UI.

It should be situated in the bottom circular part of the screen like the current UI - the user can not see outside of this area.

For pure informational elements the user can't interact with they can be situated in the upper rectangular part of the screen like the current UI.

When possible base your design on existing screens unless requested otherwise.

When prototyping the UI take screenshots to make sure it looks right and satisfies the requirements above.

## Agent skills

### Issue tracker

Issues and PRDs are tracked as local markdown files under `.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

The local markdown tracker uses the default skill triage labels, including `ready-for-agent` for AFK-ready issues. See `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repo; read root `CONTEXT.md` and `docs/adr/` when present. See `docs/agents/domain.md`.
