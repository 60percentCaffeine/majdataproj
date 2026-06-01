# UI Prototype Template Mod

Disposable MelonLoader mod for trying MajdataPlay UI ideas inside the real game window.

The template is split into a Unity-free prototype core and a thin MelonLoader adapter. The adapter keeps an IMGUI draw path and also installs a Unity UI canvas fallback because this patched MelonLoader v0.4.3 profile does not reliably invoke later MelonLoader lifecycle hooks during early startup.

## Build

From the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\ui-prototype-template-mod\build.ps1"
```

Output:

```text
ui-prototype-template-mod\bin\UiPrototypeTemplateMod.dll
ui-prototype-template-mod\bin\UiPrototypeTemplateMod.Core.dll
```

## Unit Tests

From the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\ui-prototype-template-mod\test-unit.ps1"
```

The tests cover the Unity-free song-first prototype state machine and write results under `ui-prototype-template-mod\artifacts\unit`.

## Input

Prototype logic consumes semantic actions from `UiPrototypeTemplateMod.Core`, not direct Unity keys. The adapter prefers MajdataPlay input reflection for A3/A6/A4/A5 when that API is available, and otherwise uses the keyboard fallback:

```text
Next / harder: Down, D
Previous / easier: Up, A
OK: Enter, Space
Back: Escape, Backspace
Switch visual variant: Left Arrow, Right Arrow
```

The active input source is logged at startup and shown in the prototype diagnostics line.

The prototype includes exactly three initial visual variants. Each variant displays a large `VARIANT N/3` label and keeps the current song, difficulty, and phase when switching.

## Install

From the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\ui-prototype-template-mod\install.ps1"
```

This patches the local MelonLoader v0.4.3 install using the same compatibility script as `sample-mod`, builds the prototype mod, and copies the DLL to:

```text
Majdata Hub\game\Mods\UiPrototypeTemplateMod.dll
Majdata Hub\game\Mods\UiPrototypeTemplateModLib\UiPrototypeTemplateMod.Core.dll
```

## Run

From `Majdata Hub\game`:

```powershell
powershell.exe -Command "Start-Process '.\start-controller.bat'"
```

Expected log lines in `Majdata Hub\game\MelonLoader\Latest.log`:

```text
UI Prototype Template Mod v0.1.0
[UI Prototype Template Mod] UI prototype takeover active - normal game UI is visually replaced.
[UI Prototype Template Mod] Prototype core ready: phase=SongSelect song=MAJTITLE difficulty=Basic
[UI Prototype Template Mod] Prototype input source: Input: KeyboardFallback
[UI Prototype Template Mod] UI prototype song-first canvas installed.
[UI Prototype Template Mod] Prototype input timer reached Unity main thread.
```

The game window should be covered by the song-first selection prototype.

## Remove

Delete:

```text
Majdata Hub\game\Mods\UiPrototypeTemplateMod.dll
Majdata Hub\game\Mods\UiPrototypeTemplateModLib
```

Then launch MajdataPlay normally. The prototype has no persistent settings or asset files.

## Notes For Future Prototype Screens

- Keep production MajdataPlay scene code untouched.
- Avoid Harmony by default in this environment.
- Put reusable state transitions in a Unity-free core assembly.
- Keep the MelonLoader adapter thin: lifecycle, input collection, state update, and disposable prototype rendering only.
