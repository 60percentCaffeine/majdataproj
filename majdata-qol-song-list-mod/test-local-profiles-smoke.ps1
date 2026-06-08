$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$HookPort = 17444
$HookUrl = "http://127.0.0.1:$HookPort/eval-isolated"

Get-Process -Name "MajdataPlay" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

function Invoke-GameEval {
    param([string]$Code)

    $body = @{
        code = $Code
        timeoutMs = 60000
        maxDepth = 10
        maxResponseBytes = 800000
    } | ConvertTo-Json -Compress

    Invoke-RestMethod -Uri $HookUrl -Method Post -ContentType application/json -Body $body
}

function Wait-ForHook {
    $deadline = (Get-Date).AddSeconds(120)
    do {
        Start-Sleep -Seconds 1
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$HookPort/health" -TimeoutSec 2
            if ($health.ok) {
                return
            }
        } catch {
        }
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for TestHookMod."
}

& (Join-Path $PSScriptRoot "install.ps1")
& (Join-Path $ProjectRoot "mod-test-tools\test-hook-mod\install.ps1")

$HookConfigDir = Join-Path $GameRoot "UserData\TestHookMod"
New-Item -ItemType Directory -Force -Path $HookConfigDir | Out-Null
@{
    host = "127.0.0.1"
    port = $HookPort
    replEnabled = $false
} | ConvertTo-Json -Compress | Set-Content -Path (Join-Path $HookConfigDir "config.json") -Encoding UTF8

try {
    Push-Location $GameRoot
    try {
        powershell.exe -Command "Start-Process '.\start-controller.bat'"
    } finally {
        Pop-Location
    }

    Wait-ForHook

    $bootState = $null
    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Seconds 1
        $result = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string session = (string)bridgeType.GetMethod("ActivePlayerSessionDiagnosticsSnapshot").Invoke(null, null);
    string profiles = (string)bridgeType.GetMethod("LocalProfileStoreDiagnosticsSnapshot").Invoke(null, null);
    string[] names = MajdataPlay.SongStorage.Collections.Select(c => c == null ? "<null>" : c.Name).ToArray();
    return new {
        session = session,
        profiles = profiles,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        collectionCount = names.Length,
        hasAll = names.Any(n => n == "All"),
        hasFavorites = names.Any(n => n == "MyFavorites"),
        collections = string.Join("|", names)
    };
})()
"@
        $bootState = $result.result.properties
        if ($bootState.hasAll -and $bootState.hasFavorites) {
            break
        }
    } while ((Get-Date) -lt $deadline)

    if (-not (($bootState.session -like "*activePlayerMode=Guest*") -and ($bootState.session -like "*saveTargetKind=Guest*"))) {
        throw "Local profiles smoke failed: active player session did not default to Guest. Session: $($bootState.session)"
    }

    if (-not ($bootState.profiles -like "*savedProfiles=0*")) {
        throw "Local profiles smoke failed: local profile store did not report an empty saved-profile list. Profiles: $($bootState.profiles)"
    }

    if (-not $bootState.hasAll -or -not $bootState.hasFavorites) {
        throw "Local profiles smoke failed: expected current song-list boot collections were missing. Scene=$($bootState.scene) Collections=$($bootState.collections)"
    }

    $latestLog = Join-Path $GameRoot "MelonLoader\Latest.log"
    $fatalLogs = Select-String -Path $latestLog -Pattern "\[ERROR\]|Unhandled Exception|Fatal" -SimpleMatch:$false -ErrorAction SilentlyContinue
    if ($fatalLogs) {
        throw "Local profiles smoke failed: fatal/error logs were present. $($fatalLogs | Select-Object -First 5 | ForEach-Object { $_.Line } | Out-String)"
    }

    Write-Host "Local profiles boot smoke passed. $($bootState.session) $($bootState.profiles)"
} finally {
    Get-Process -Name "MajdataPlay" -ErrorAction SilentlyContinue | Stop-Process -Force
}
