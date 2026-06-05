# Majdata QoL Song List Mod

Production MelonLoader mod for MajdataPlay song-list quality-of-life behavior.

This directory is distinct from `.scratch/majdata-qol-song-list-mod` prototype artifacts. Runtime patches will be added incrementally; the first slice only verifies the production mod shell and pure map-list defaults.

## Commands

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\majdata-qol-song-list-mod\test-unit.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\majdata-qol-song-list-mod\build.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\majdata-qol-song-list-mod\install.ps1"
```

The install script copies:

- `MajdataQolSongListMod.dll` to `Majdata Hub\game\Mods`
- `MajdataQolSongListMod.Core.dll` to `Majdata Hub\game\Mods\MajdataQolSongListModLib`
