# Scaffold Runnable Prototype Mod

Status: done

## Parent

.scratch/ui-prototype-template-mod/PRD.md

## What to build

Build the first runnable vertical slice of the UI prototype template mod. The slice should install and run inside MajdataPlay through MelonLoader, log that the prototype takeover is active, block or visually replace normal game content enough for prototype work, and render a simple full-screen IMGUI placeholder.

The implementation should follow the existing sample mod build/install conventions and avoid Harmony. It should include concise documentation for building, installing, launching, and removing the prototype mod.

## Acceptance criteria

- [x] A prototype MelonLoader mod can be built with one documented command.
- [x] The prototype mod can be installed into the MajdataPlay `Mods` directory with one documented command.
- [x] Launching MajdataPlay with the mod installed logs a clear activation message.
- [x] Launching MajdataPlay with the mod installed shows a full-screen prototype placeholder instead of a usable game screen.
- [x] The mod avoids Harmony patches and uses plain MelonLoader lifecycle hooks.
- [x] The docs explain how to remove or disable the prototype mod and return to normal MajdataPlay behavior.

## Completion notes

- Added `ui-prototype-template-mod` with `PrototypeTemplateMod.cs`, `build.ps1`, `install.ps1`, and `README.md`.
- Verified `build.ps1` creates `bin\UiPrototypeTemplateMod.dll`.
- Verified `install.ps1` copies the DLL to `Majdata Hub\game\Mods`.
- Verified a real MajdataPlay launch logs activation plus `UI prototype canvas placeholder installed.`

## Blocked by

None - can start immediately
