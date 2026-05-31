$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$InstallDir = Join-Path $GameRoot "Mods\TestHookModReplClient"
$LegacyInstallDir = Join-Path $GameRoot "Mods\ModTestReplClient"

& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
if (Test-Path $LegacyInstallDir) {
    Remove-Item -Recurse -Force $LegacyInstallDir
}
Copy-Item -Recurse -Force (Join-Path $PSScriptRoot "bin\publish\*") $InstallDir

Write-Host "Installed Test Hook Mod REPL client to $InstallDir"
