$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ManagedDir = Join-Path $GameRoot "MajdataPlay_Data\Managed"
$MelonLoaderDir = Join-Path $GameRoot "MelonLoader\net35"
if (-not (Test-Path (Join-Path $MelonLoaderDir "MelonLoader.dll"))) {
    $MelonLoaderDir = Join-Path $GameRoot "MelonLoader"
}
$OutputDir = Join-Path $PSScriptRoot "bin"
$OutputDll = Join-Path $OutputDir "MajdataQolSongListMod.dll"
$CoreProject = Join-Path $PSScriptRoot "src\MajdataQolSongListMod.Core\MajdataQolSongListMod.Core.csproj"
$CoreOutput = Join-Path $PSScriptRoot "src\MajdataQolSongListMod.Core\bin\Release\netstandard2.0\MajdataQolSongListMod.Core.dll"
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
    Join-Path $ManagedDir "Assembly-CSharp.dll"
    Join-Path $ManagedDir "UnityEngine.CoreModule.dll"
    Join-Path $ManagedDir "UnityEngine.TextRenderingModule.dll"
    Join-Path $ManagedDir "UnityEngine.UI.dll"
    Join-Path $ManagedDir "Unity.TextMeshPro.dll"
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
    (Join-Path $PSScriptRoot "MajdataQolSongListMod.cs")

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built $OutputDll"
Copy-Item -Force $CoreOutput (Join-Path $OutputDir "MajdataQolSongListMod.Core.dll")
Write-Host "Copied MajdataQolSongListMod.Core.dll to $OutputDir"
