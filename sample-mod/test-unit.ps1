$ErrorActionPreference = "Stop"

$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$TestProject = Join-Path $PSScriptRoot "tests\TestMod.Core.Tests\TestMod.Core.Tests.csproj"
$ResultsDir = Join-Path $PSScriptRoot "artifacts\unit"

if (Test-Path $ResultsDir) {
    Remove-Item -Recurse -Force $ResultsDir
}
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

& $DotnetPath test $TestProject `
    -c Release `
    --nologo `
    --results-directory $ResultsDir `
    --logger "trx;LogFileName=sample-mod-core.trx" `
    --collect "XPlat Code Coverage" `
    -- `
    DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include="[TestMod.Core]*" `
    DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile="**/obj/**"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE"
}

Write-Host "Unit test and coverage artifacts written to $ResultsDir"
