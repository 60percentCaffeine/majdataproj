$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"
$BridgeLibDir = Join-Path $ModsDir "ModTestBridgeLib"

& (Join-Path $ProjectRoot "testmod\patch-melonloader-043.ps1")
& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $ModsDir | Out-Null
if (Test-Path $BridgeLibDir) {
    Remove-Item -Recurse -Force $BridgeLibDir
}
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ModsDir "ModTestBridge.dll")
Copy-Item -Force (Join-Path $PSScriptRoot "bin\TestHookMod.dll") (Join-Path $ModsDir "TestHookMod.dll")

Write-Host "Installed TestHookMod.dll to $ModsDir"
