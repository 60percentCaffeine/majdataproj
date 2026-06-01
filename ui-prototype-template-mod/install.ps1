$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"
$LibDir = Join-Path $ModsDir "UiPrototypeTemplateModLib"

& (Join-Path $ProjectRoot "sample-mod\patch-melonloader-043.ps1")
& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $ModsDir | Out-Null
if (Test-Path $LibDir) {
    Remove-Item -Recurse -Force $LibDir
}
New-Item -ItemType Directory -Force -Path $LibDir | Out-Null
Copy-Item -Force (Join-Path $PSScriptRoot "bin\UiPrototypeTemplateMod.dll") (Join-Path $ModsDir "UiPrototypeTemplateMod.dll")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ModsDir "UiPrototypeTemplateMod.Core.dll")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $GameRoot "UserLibs\UiPrototypeTemplateMod.Core.dll")
Copy-Item -Force (Join-Path $PSScriptRoot "bin\UiPrototypeTemplateMod.Core.dll") (Join-Path $LibDir "UiPrototypeTemplateMod.Core.dll")

Write-Host "Installed UiPrototypeTemplateMod.dll to $ModsDir and UiPrototypeTemplateMod.Core.dll to $LibDir"
