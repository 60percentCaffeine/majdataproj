$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$InstallDir = Join-Path $GameRoot "Mods\ModTestReplClient"

& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Recurse -Force (Join-Path $PSScriptRoot "bin\publish\*") $InstallDir

Write-Host "Installed ModTestReplClient to $InstallDir"
