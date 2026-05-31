# TestMod

Minimal MelonLoader mod for MajdataPlay. It logs:

```text
Loaded
```

The installed log line appears as:

```text
[TestMod] Loaded
```

## Files

- `TestMod.cs`: the mod source.
- `build.ps1`: builds `bin/TestMod.dll` with the Windows .NET SDK Roslyn compiler.
- `install.ps1`: patches the installed MelonLoader v0.4.3 compatibility shims, builds the mod, and copies it to `Majdata Hub/game/Mods/TestMod.dll`.
- `native/mlhook1.c`: small `VERSION.dll` import proxy used because this game did not load MelonLoader's original local `version.dll` proxy.
- `build-native.sh`: rebuilds `native/mlhook1.dll` and installs it to `Majdata Hub/game/mlhook1.dll`.
- `tools/Mono.Cecil.dll`: local helper used by `patch-melonloader-043.ps1`.

## Current Game Install

The working setup uses MelonLoader v0.4.3 with compatibility patches for MajdataPlay's Mono profile. Newer MelonLoader versions failed during bootstrap or loader startup in this environment.

`UnityPlayer.dll` was patched to import `mlhook1.dll` instead of `VERSION.dll`. The backup made before that patch is:

```text
backups/UnityPlayer.20260601-034550.dll
```

There was no original `Majdata Hub/game/version.dll`; any local `version.dll` seen during setup came from MelonLoader experiments and has been removed.

## MelonLoader Patches

`patch-melonloader-043.ps1` edits the installed `Majdata Hub/game/MelonLoader/MelonLoader.dll` with Mono.Cecil. The script keeps a first-run backup at:

```text
Majdata Hub/game/MelonLoader/MelonLoader.dll.before-testmod-patch
```

The patches are compatibility shims for MajdataPlay's Unity/Mono profile. They are intentionally scoped to loading a simple non-Harmony test mod.

Disabled startup calls:

- `MelonLoader.Fixes.InvariantCurrentCulture::Install()`: uses Harmony code paths that fail on this Mono profile with a missing `AmbiguousMatchException(string, Exception)` constructor.
- `MelonLoader.Fixes.ApplicationBase::Run(AppDomain)`: app-domain base-directory fix is unnecessary here because the game is launched from its own directory, and related directory APIs are incomplete in this runtime.
- `MelonLoader.Fixes.ExtraCleanup::Run()`: optional cleanup hook; skipped to avoid Harmony/runtime compatibility paths.
- `MelonLoader.MelonPreferences::Load()`: v0.4.3 preferences use `FileSystemWatcher(string, string)`, which this Mono profile does not expose.
- `MelonLoader.PatchShield::Install()`: optional Harmony protection layer; skipped because Harmony initialization is incompatible here.
- `MelonLoader.bHaptics::Load()` and `MelonLoader.bHaptics::Start()`: optional haptics integration; skipped because it triggers Harmony/native-library compatibility paths and is irrelevant for TestMod.
- `MelonLoader.SupportModule::Initialize()`: Unity support module fails on `SceneManager.add_sceneLoaded(...)` in this game; the patch replaces the call with `true` so the loader continues.
- `MelonLoader.Core::AddUnityDebugLog()`: skipped because it depends on the support module interface.

Important limitation: this install is not a general-purpose MelonLoader setup. Harmony patching and some MelonLoader services are disabled or unreliable. For TestMod, this is enough because the mod only uses `MelonMod.OnApplicationStart()` and `MelonLogger.Msg()`.

`TestMod.cs` also has:

```csharp
[assembly: HarmonyDontPatchAll]
```

That prevents MelonLoader v0.4.3 from automatically calling `Harmony.PatchAll()` for this assembly before `OnApplicationStart()`.

## Rebuild And Install

From the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\testmod\install.ps1"
```

To rebuild the native proxy from WSL:

```bash
./testmod/build-native.sh
```

Then start Majdata with:

```powershell
powershell.exe -Command "Start-Process '.\start-controller.bat'"
```

from `Majdata Hub/game`.

## Verification

Check:

```text
Majdata Hub/game/MelonLoader/Latest.log
```

Expected lines:

```text
1 Mod Loaded
TestMod v1.0.0
[TestMod] Loaded
```
