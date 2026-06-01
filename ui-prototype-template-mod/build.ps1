$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ManagedDir = Join-Path $GameRoot "MajdataPlay_Data\Managed"
$MelonLoaderDir = Join-Path $GameRoot "MelonLoader\net35"
if (-not (Test-Path (Join-Path $MelonLoaderDir "MelonLoader.dll"))) {
    $MelonLoaderDir = Join-Path $GameRoot "MelonLoader"
}

$OutputDir = Join-Path $PSScriptRoot "bin"
$OutputDll = Join-Path $OutputDir "UiPrototypeTemplateMod.dll"
$CscPath = "C:\Program Files\dotnet\sdk\9.0.200\Roslyn\bincore\csc.dll"
$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$References = @(
    Join-Path $ManagedDir "mscorlib.dll"
    Join-Path $ManagedDir "System.dll"
    Join-Path $ManagedDir "System.Core.dll"
    Join-Path $MelonLoaderDir "MelonLoader.dll"
    Join-Path $ManagedDir "UnityEngine.CoreModule.dll"
    Join-Path $ManagedDir "UnityEngine.IMGUIModule.dll"
    Join-Path $ManagedDir "UnityEngine.InputLegacyModule.dll"
    Join-Path $ManagedDir "UnityEngine.TextRenderingModule.dll"
    Join-Path $ManagedDir "UnityEngine.UI.dll"
    Join-Path $ManagedDir "UnityEngine.UIModule.dll"
)

$ReferenceArgs = $References | ForEach-Object { "/reference:$_" }

& $DotnetPath $CscPath `
    /nologo `
    /target:library `
    /optimize+ `
    /nostdlib+ `
    /out:$OutputDll `
    $ReferenceArgs `
    (Join-Path $PSScriptRoot "PrototypeTemplateMod.cs")

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built $OutputDll"
