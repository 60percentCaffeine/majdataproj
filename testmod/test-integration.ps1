$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$IntegrationProject = Join-Path $ProjectRoot "modtest\integration\ModTest.Integration.Tests.csproj"
$ResultsDir = Join-Path $ProjectRoot ".scratch\modtest-artifacts\integration-test-results"

if (Test-Path $ResultsDir) {
    Remove-Item -Recurse -Force $ResultsDir
}
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

Get-Process MajdataPlay,ModTestReplClient -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

& (Join-Path $ProjectRoot "modtest\bridge\install.ps1")
& (Join-Path $ProjectRoot "modtest\repl\install.ps1")
& (Join-Path $ProjectRoot "modtest\harness\build.ps1")
& (Join-Path $PSScriptRoot "install.ps1")

$env:MODTEST_PROJECT_ROOT = $ProjectRoot.Path
try {
    & $DotnetPath test $IntegrationProject `
        -c Release `
        --nologo `
        --results-directory $ResultsDir `
        --logger "trx;LogFileName=integration.trx"

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE"
    }
}
finally {
    Remove-Item Env:MODTEST_PROJECT_ROOT -ErrorAction SilentlyContinue
    Get-Process ModTestReplClient -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

Write-Host "Integration test artifacts written to $ResultsDir"
