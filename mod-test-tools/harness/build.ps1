$ErrorActionPreference = "Stop"

$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$ProjectPath = Join-Path $PSScriptRoot "ModTestHarness.csproj"

& $DotnetPath build $ProjectPath -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Built ModTestHarness"
