$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"

& (Join-Path $ProjectRoot "testmod\patch-melonloader-043.ps1")
& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $ModsDir | Out-Null
Copy-Item -Force (Join-Path $PSScriptRoot "bin\ModTestBridge.dll") (Join-Path $ModsDir "ModTestBridge.dll")

Write-Host "Installed ModTestBridge.dll to $ModsDir"
