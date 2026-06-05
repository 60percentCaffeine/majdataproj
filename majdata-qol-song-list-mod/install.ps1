$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"
$LibDir = Join-Path $ModsDir "MajdataQolSongListModLib"

& (Join-Path $ProjectRoot "sample-mod\patch-melonloader-043.ps1")
& (Join-Path $PSScriptRoot "build.ps1")

New-Item -ItemType Directory -Force -Path $ModsDir | Out-Null
if (Test-Path $LibDir) {
    Remove-Item -Recurse -Force $LibDir
}
New-Item -ItemType Directory -Force -Path $LibDir | Out-Null

Copy-Item -Force (Join-Path $PSScriptRoot "bin\MajdataQolSongListMod.dll") (Join-Path $ModsDir "MajdataQolSongListMod.dll")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ModsDir "MajdataQolSongListMod.Core.dll")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $GameRoot "UserLibs\MajdataQolSongListMod.Core.dll")
Copy-Item -Force (Join-Path $PSScriptRoot "bin\MajdataQolSongListMod.Core.dll") (Join-Path $LibDir "MajdataQolSongListMod.Core.dll")

Write-Host "Installed MajdataQolSongListMod.dll to $ModsDir and MajdataQolSongListMod.Core.dll to $LibDir"
