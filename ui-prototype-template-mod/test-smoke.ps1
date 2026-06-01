$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$MelonLog = Join-Path $GameRoot "MelonLoader\Latest.log"
$RuntimeLog = Join-Path $GameRoot "Logs\MajPlayRuntime.log"
$ArtifactsDir = Join-Path $PSScriptRoot "artifacts\smoke"

function Stop-PrototypeProcesses {
    Get-Process MajdataPlay -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process ModTestReplClient -ErrorAction SilentlyContinue | Stop-Process -Force
}

function Wait-ForLogLines {
    param(
        [string]$Path,
        [string[]]$Needles,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $Path) {
            $content = Get-Content $Path -Raw
            $missing = @($Needles | Where-Object { -not $content.Contains($_) })
            if ($missing.Count -eq 0) {
                return $content
            }
        }

        Start-Sleep -Milliseconds 500
    }

    if (Test-Path $Path) {
        $tail = (Get-Content $Path -Tail 160) -join [Environment]::NewLine
    } else {
        $tail = "<missing log: $Path>"
    }

    throw "Timed out waiting for smoke log lines: $($Needles -join ', ')`nLatest log tail:`n$tail"
}

function Assert-NoFatalLogLines {
    param([string[]]$Paths)

    $needles = @(
        "[ERROR]",
        "[FATAL]",
        "Failed to Resolve Melons",
        "Unhandled Exception",
        "Unhandled exception",
        "Fatal error",
        "fatal error",
        "TypeLoadException",
        "MissingMethodException",
        "NullReferenceException"
    )

    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($path in $Paths) {
        if (-not (Test-Path $path)) {
            continue
        }

        foreach ($line in Get-Content $path) {
            foreach ($needle in $needles) {
                if ($line.Contains($needle)) {
                    $hits.Add("$path`: $line")
                }
            }
        }
    }

    if ($hits.Count -gt 0) {
        throw "Fatal smoke log lines found:`n$($hits -join [Environment]::NewLine)"
    }
}

if (Test-Path $ArtifactsDir) {
    Remove-Item -Recurse -Force $ArtifactsDir
}
New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null

Stop-PrototypeProcesses
& (Join-Path $PSScriptRoot "install.ps1")

try {
    Push-Location $GameRoot
    Start-Process -FilePath ".\start-controller.bat"
    Pop-Location

    $requiredLines = @(
        "UI Prototype Template Mod v0.1.0",
        "UI prototype takeover active - normal game UI is visually replaced.",
        "Prototype core ready: phase=SongSelect song=MAJTITLE difficulty=Basic",
        "UI prototype song-first canvas installed.",
        "Prototype input timer reached Unity main thread."
    )

    $content = Wait-ForLogLines -Path $MelonLog -Needles $requiredLines -TimeoutSeconds 60
    Assert-NoFatalLogLines -Paths @($MelonLog, $RuntimeLog)

    Copy-Item -Force $MelonLog (Join-Path $ArtifactsDir "MelonLoader-Latest.log")
    if (Test-Path $RuntimeLog) {
        Copy-Item -Force $RuntimeLog (Join-Path $ArtifactsDir "MajPlayRuntime.log")
    }

    Write-Host "Prototype smoke verification passed."
    Write-Host "Verified log lines:"
    foreach ($line in $requiredLines) {
        Write-Host "  $line"
    }
    Write-Host "Artifacts written to $ArtifactsDir"
}
finally {
    Pop-Location -ErrorAction SilentlyContinue
    Stop-PrototypeProcesses
}
