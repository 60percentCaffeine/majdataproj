$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ManagedDir = Join-Path $GameRoot "MajdataPlay_Data\Managed"
$MelonLoaderDir = Join-Path $GameRoot "MelonLoader\net35"
if (-not (Test-Path (Join-Path $MelonLoaderDir "MelonLoader.dll"))) {
    $MelonLoaderDir = Join-Path $GameRoot "MelonLoader"
}
$OutputDir = Join-Path $PSScriptRoot "bin"
$OutputDll = Join-Path $OutputDir "TestMod.dll"
$CoreProject = Join-Path $PSScriptRoot "src\TestMod.Core\TestMod.Core.csproj"
$CoreOutput = Join-Path $PSScriptRoot "src\TestMod.Core\bin\Release\netstandard2.0\TestMod.Core.dll"
$CscPath = "C:\Program Files\dotnet\sdk\9.0.200\Roslyn\bincore\csc.dll"
$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

& $DotnetPath build $CoreProject -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$References = @(
    Join-Path $ManagedDir "mscorlib.dll"
    Join-Path $ManagedDir "System.dll"
    Join-Path $ManagedDir "System.Core.dll"
    Join-Path $ManagedDir "netstandard.dll"
    Join-Path $MelonLoaderDir "MelonLoader.dll"
    $CoreOutput
)

$ReferenceArgs = $References | ForEach-Object { "/reference:$_" }

& $DotnetPath $CscPath `
    /nologo `
    /target:library `
    /optimize+ `
    /nostdlib+ `
    /out:$OutputDll `
    $ReferenceArgs `
    (Join-Path $PSScriptRoot "TestMod.cs")

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built $OutputDll"
Copy-Item -Force $CoreOutput (Join-Path $OutputDir "TestMod.Core.dll")
Write-Host "Copied TestMod.Core.dll to $OutputDir"
