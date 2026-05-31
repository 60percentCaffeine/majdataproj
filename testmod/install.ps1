$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"

& (Join-Path $PSScriptRoot "patch-melonloader-043.ps1")
& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $ModsDir | Out-Null
Copy-Item -Force (Join-Path $PSScriptRoot "bin\TestMod.dll") (Join-Path $ModsDir "TestMod.dll")
Copy-Item -Force (Join-Path $PSScriptRoot "bin\TestMod.Core.dll") (Join-Path $ModsDir "TestMod.Core.dll")

Write-Host "Installed TestMod.dll and TestMod.Core.dll to $ModsDir"
