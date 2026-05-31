$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"
$LibDir = Join-Path $ModsDir "TestModLib"

& (Join-Path $PSScriptRoot "patch-melonloader-043.ps1")
& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $ModsDir | Out-Null
if (Test-Path $LibDir) {
    Remove-Item -Recurse -Force $LibDir
}
New-Item -ItemType Directory -Force -Path $LibDir | Out-Null
Copy-Item -Force (Join-Path $PSScriptRoot "bin\TestMod.dll") (Join-Path $ModsDir "TestMod.dll")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ModsDir "TestMod.Core.dll")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $GameRoot "UserLibs\TestMod.Core.dll")
Copy-Item -Force (Join-Path $PSScriptRoot "bin\TestMod.Core.dll") (Join-Path $LibDir "TestMod.Core.dll")

Write-Host "Installed TestMod.dll to $ModsDir and TestMod.Core.dll to $LibDir"
