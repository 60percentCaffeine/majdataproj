# TestMod

Minimal MelonLoader mod for MajdataPlay. The sample is split into a pure core assembly and a MelonLoader adapter. The adapter logs:

```text
Loaded
```

The installed log line appears as:

```text
[TestMod] Loaded
```

## Files

- `TestMod.cs`: the mod source.
- `src/TestMod.Core`: pure mod logic that builds without Unity, MajdataPlay, or MelonLoader runtime initialization.
- `tests/TestMod.Core.Tests`: xUnit tests for the pure core assembly.
- `build.ps1`: builds `bin/TestMod.dll` with the Windows .NET SDK Roslyn compiler.
- `install.ps1`: patches the installed MelonLoader v0.4.3 compatibility shims, builds the mod, and copies it plus `TestMod.Core.dll` to `Majdata Hub/game/Mods`.
- `test-unit.ps1`: builds the core/tests, runs xUnit, and writes coverage artifacts under `sample-mod/artifacts/unit`.
- `test-integration.ps1`: builds and installs the test hook, REPL client, harness, and sample mod, then runs real-game xUnit integration tests.
- `test-all.ps1`: runs unit coverage and real-game integration tests in sequence.
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
Majdata Hub/game/MelonLoader/MelonLoader.dll.before-sample-mod-patch
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\install.ps1"
```

Run pure core unit tests with coverage:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\test-unit.ps1"
```

Run real-game integration tests:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\test-integration.ps1"
```

Run the full per-mod suite:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\test-all.ps1"
```

To rebuild the native proxy from WSL:

```bash
./sample-mod/build-native.sh
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

## Fresh Majdata Install

These steps assume a clean Majdata install at:

```text
C:\Users\user0-pc\majdataproj\Majdata Hub\game
```

and this repo checked out at:

```text
C:\Users\user0-pc\majdataproj
```

### 1. Install Build Prerequisites

On Windows, install the .NET SDK so `C:\Program Files\dotnet\dotnet.exe` exists. The tested machine used .NET SDK 9.0.200.

In WSL, install the MinGW cross compiler used for `mlhook1.dll`:

```bash
sudo apt-get update
sudo apt-get install -y mingw-w64
```

### 2. Install MelonLoader v0.4.3

Download:

```text
https://github.com/LavaGang/MelonLoader/releases/download/v0.4.3/MelonLoader.x64.zip
```

Extract it somewhere temporary. Copy only the extracted `MelonLoader` folder into the game root:

```text
Majdata Hub/game/MelonLoader
```

Do not install or keep MelonLoader's `version.dll` in the game root for this setup. This Majdata install did not originally have a local `version.dll`, and Windows did not load MelonLoader's local `version.dll` proxy for this game.

### 3. Provide Mono.Cecil For The Patcher

`patch-melonloader-043.ps1` needs `Mono.Cecil.dll` locally at:

```text
sample-mod/tools/Mono.Cecil.dll
```

The tested setup used `Mono.Cecil.dll` from MelonLoader v0.5.7:

```text
https://github.com/LavaGang/MelonLoader/releases/download/v0.5.7/MelonLoader.x64.zip
```

Extract that archive somewhere temporary and copy:

```text
MelonLoader/Mono.Cecil.dll
```

to:

```text
sample-mod/tools/Mono.Cecil.dll
```

The DLL is intentionally gitignored because it is an external binary.

### 4. Build And Install The Native Proxy

From WSL at the project root:

```bash
./sample-mod/build-native.sh
```

This builds:

```text
sample-mod/native/mlhook1.dll
```

and copies it to:

```text
Majdata Hub/game/mlhook1.dll
```

`mlhook1.dll` forwards the three `VERSION.dll` functions Unity imports to the real System32 `VERSION.dll`, then loads MelonLoader's v0.4.3 bootstrap.

### 5. Patch UnityPlayer.dll To Load mlhook1.dll

Back up `UnityPlayer.dll` first:

```bash
mkdir -p backups
cp -a "Majdata Hub/game/UnityPlayer.dll" "backups/UnityPlayer.$(date +%Y%m%d-%H%M%S).dll"
```

Patch the import name:

```bash
perl -0pi -e 's/VERSION\.dll/mlhook1.dll/' "Majdata Hub/game/UnityPlayer.dll"
```

This works because `VERSION.dll` and `mlhook1.dll` are the same string length. Do not run this repeatedly against the same file unless you first verify the import table.

Verify:

```bash
objdump -p "Majdata Hub/game/UnityPlayer.dll" | rg "DLL Name: (VERSION|mlhook1)"
```

Expected:

```text
DLL Name: mlhook1.dll
```

### 6. Patch MelonLoader And Install TestMod

From the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\user0-pc\majdataproj\sample-mod\install.ps1"
```

This:

- patches `Majdata Hub/game/MelonLoader/MelonLoader.dll` for MajdataPlay's Mono profile;
- builds `sample-mod/bin/TestMod.dll`;
- copies it to `Majdata Hub/game/Mods/TestMod.dll`.

### 7. Start And Verify

From `Majdata Hub/game`:

```powershell
powershell.exe -Command "Start-Process '.\start-controller.bat'"
```

Then check:

```text
Majdata Hub/game/MelonLoader/Latest.log
```

Expected:

```text
MelonLoader v0.4.3 Open-Beta
1 Mod Loaded
TestMod v1.0.0
[TestMod] Loaded
```

### Restore UnityPlayer.dll

To undo the import patch, stop the game and copy the backup over:

```bash
cp -f backups/UnityPlayer.YYYYMMDD-HHMMSS.dll "Majdata Hub/game/UnityPlayer.dll"
```

Then remove the MelonLoader/test files from the game root if desired:

```bash
rm -rf "Majdata Hub/game/MelonLoader" "Majdata Hub/game/Mods/TestMod.dll" "Majdata Hub/game/mlhook1.dll"
```
