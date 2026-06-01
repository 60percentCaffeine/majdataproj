$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"

& (Join-Path $ProjectRoot "sample-mod\patch-melonloader-043.ps1")
& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $ModsDir | Out-Null
Copy-Item -Force (Join-Path $PSScriptRoot "bin\UiPrototypeTemplateMod.dll") (Join-Path $ModsDir "UiPrototypeTemplateMod.dll")

Write-Host "Installed UiPrototypeTemplateMod.dll to $ModsDir"
