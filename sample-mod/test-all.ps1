$ErrorActionPreference = "Stop"

$UnitScript = Join-Path $PSScriptRoot "test-unit.ps1"
$IntegrationScript = Join-Path $PSScriptRoot "test-integration.ps1"

Write-Host "Running TestMod unit tests with coverage..."
& $UnitScript
if ($LASTEXITCODE -ne 0) {
    throw "Unit tests failed with exit code $LASTEXITCODE"
}

Write-Host "Running TestMod integration tests..."
& $IntegrationScript
if ($LASTEXITCODE -ne 0) {
    throw "Integration tests failed with exit code $LASTEXITCODE"
}

Write-Host "All TestMod tests passed."
Write-Host "Unit artifacts: $PSScriptRoot\artifacts\unit"
Write-Host "Integration artifacts: $(Resolve-Path (Join-Path $PSScriptRoot '..\.scratch\mod-test-tools-artifacts'))"
