$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"
$HookLibDir = Join-Path $ModsDir "TestHookModLib"
$LegacyBridgeLibDir = Join-Path $ModsDir "ModTestBridgeLib"

& (Join-Path $ProjectRoot "sample-mod\patch-melonloader-043.ps1")
& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $ModsDir | Out-Null
if (Test-Path $HookLibDir) {
    Remove-Item -Recurse -Force $HookLibDir
}
if (Test-Path $LegacyBridgeLibDir) {
    Remove-Item -Recurse -Force $LegacyBridgeLibDir
}
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ModsDir "ModTestBridge.dll")
Copy-Item -Force (Join-Path $PSScriptRoot "bin\TestHookMod.dll") (Join-Path $ModsDir "TestHookMod.dll")

Write-Host "Installed TestHookMod.dll to $ModsDir"
