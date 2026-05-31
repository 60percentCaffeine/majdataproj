$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$MelonLoaderDir = Join-Path $ProjectRoot "Majdata Hub\game\MelonLoader"
$MelonLoaderDll = Join-Path $MelonLoaderDir "MelonLoader.dll"
$CecilDll = Join-Path $PSScriptRoot "tools\Mono.Cecil.dll"
$BackupDll = Join-Path $MelonLoaderDir "MelonLoader.dll.before-sample-mod-patch"

if (-not (Test-Path $BackupDll)) {
    Copy-Item $MelonLoaderDll $BackupDll
}

[void][Reflection.Assembly]::LoadFrom($CecilDll)
$readerParameters = New-Object Mono.Cecil.ReaderParameters
$readerParameters.ReadWrite = $true
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($MelonLoaderDll, $readerParameters)

$coreType = $assembly.MainModule.Types | Where-Object { $_.FullName -eq "MelonLoader.Core" } | Select-Object -First 1
if ($null -eq $coreType) {
    throw "Could not find MelonLoader.Core"
}

$cctor = $coreType.Methods | Where-Object { $_.Name -eq ".cctor" } | Select-Object -First 1
if ($null -eq $cctor) {
    throw "Could not find MelonLoader.Core .cctor"
}

$patched = 0
foreach ($instruction in $cctor.Body.Instructions) {
    if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and
        $instruction.Operand -ne $null -and
        (
            $instruction.Operand.FullName -eq "System.Void MelonLoader.Fixes.InvariantCurrentCulture::Install()" -or
            $instruction.Operand.FullName -eq "System.Void MelonLoader.Fixes.ApplicationBase::Run(System.AppDomain)" -or
            $instruction.Operand.FullName -eq "System.Void MelonLoader.Fixes.ExtraCleanup::Run()" -or
            $instruction.Operand.FullName -eq "System.Void MelonLoader.MelonPreferences::Load()" -or
            $instruction.Operand.FullName -eq "System.Void MelonLoader.PatchShield::Install()"
        )) {
        # MajdataPlay's Mono profile is missing several framework APIs used by
        # these optional MelonLoader startup services. The test mod does not
        # need culture fixes, AppDomain base rewrites, preferences, cleanup, or
        # Harmony patch shielding, so skip them and keep mod loading alive.
        if ($instruction.Operand.FullName -eq "System.Void MelonLoader.Fixes.ApplicationBase::Run(System.AppDomain)") {
            $instruction.OpCode = [Mono.Cecil.Cil.OpCodes]::Pop
        } else {
            $instruction.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
        }
        $instruction.Operand = $null
        $patched++
    }
}

$initialize = $coreType.Methods | Where-Object { $_.Name -eq "Initialize" } | Select-Object -First 1
if ($null -ne $initialize) {
    foreach ($instruction in $initialize.Body.Instructions) {
        if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and
            $instruction.Operand -ne $null -and
            $instruction.Operand.FullName -eq "System.Void MelonLoader.bHaptics::Load()") {
            # Optional haptics integration touches Harmony/native-library paths
            # that are not needed for this test mod.
            $instruction.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
            $instruction.Operand = $null
            $patched++
        }
    }
}

$start = $coreType.Methods | Where-Object { $_.Name -eq "Start" } | Select-Object -First 1
if ($null -ne $start) {
    foreach ($instruction in $start.Body.Instructions) {
        if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and
            $instruction.Operand -ne $null -and
            (
                $instruction.Operand.FullName -eq "System.Boolean MelonLoader.SupportModule::Initialize()" -or
                $instruction.Operand.FullName -eq "System.Void MelonLoader.Core::AddUnityDebugLog()" -or
                $instruction.Operand.FullName -eq "System.Void MelonLoader.bHaptics::Start()"
            )) {
            # The v0.4.3 support module uses a SceneManager event signature that
            # this game/runtime does not expose. Pretend support initialization
            # succeeded, but skip Unity debug-banner and haptics calls that rely
            # on that support module.
            if ($instruction.Operand.FullName -eq "System.Boolean MelonLoader.SupportModule::Initialize()") {
                $instruction.OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_I4_1
            } else {
                $instruction.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop
            }
            $instruction.Operand = $null
            $patched++
        }
    }
}

if ($patched -eq 0) {
    Write-Host "MelonLoader.dll already patched or target calls were not present."
} else {
    $assembly.Write()
    Write-Host "Patched $patched optional startup call(s) in MelonLoader.dll."
}

$assembly.Dispose()
