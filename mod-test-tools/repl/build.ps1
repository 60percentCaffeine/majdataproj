$ErrorActionPreference = "Stop"

$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$ProjectPath = Join-Path $PSScriptRoot "ModTestReplClient.csproj"
$OutputDir = Join-Path $PSScriptRoot "bin\publish"

& $DotnetPath publish $ProjectPath -c Release -o $OutputDir --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Built $OutputDir\ModTestReplClient.exe"
