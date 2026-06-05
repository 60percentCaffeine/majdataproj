$ErrorActionPreference = "Stop"

$TestProject = Join-Path $PSScriptRoot "tests\MajdataQolSongListMod.Core.Tests\MajdataQolSongListMod.Core.Tests.csproj"
$ArtifactsDir = Join-Path $PSScriptRoot "artifacts\unit"
$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"

New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null

& $DotnetPath test $TestProject `
    -c Release `
    --nologo `
    --results-directory $ArtifactsDir `
    --collect:"XPlat Code Coverage"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE"
}
