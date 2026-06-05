$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$HookPort = 17444
$HookUrl = "http://127.0.0.1:$HookPort/eval-isolated"
$ScreenshotDir = Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\screenshots\production"
$ArtifactPath = Join-Path $ScreenshotDir "latest-production-ui-screenshots.json"

New-Item -ItemType Directory -Force -Path $ScreenshotDir | Out-Null

Get-Process -Name "MajdataPlay" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

function Invoke-GameEval {
    param([string]$Code)

    $body = @{
        code = $Code
        timeoutMs = 60000
        maxDepth = 12
        maxResponseBytes = 1000000
    } | ConvertTo-Json -Compress

    Invoke-RestMethod -Uri $HookUrl -Method Post -ContentType application/json -Body $body -TimeoutSec 90
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

function Convert-ToGamePathLiteral {
    param([string]$Path)
    return $Path.Replace("\", "\\")
}

function Wait-ForScreenshotFile {
    param([string]$Path)

    $deadline = (Get-Date).AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 250
        if (Test-Path $Path) {
            $item = Get-Item $Path
            if ($item.Length -gt 0) {
                return $item
            }
        }
    } while ((Get-Date) -lt $deadline)

    throw "Screenshot was not written: $Path"
}

function Capture-ProductionScreenshot {
    param(
        [string]$Name,
        [string]$Description
    )

    $path = Join-Path $ScreenshotDir "$Name.png"
    Remove-Item -Force -ErrorAction SilentlyContinue $path
    $gamePath = Convert-ToGamePathLiteral $path
    $captureResult = Invoke-GameEval @"
new Func<object>(() => {
    string path = @"$gamePath";
    UnityEngine.ScreenCapture.CaptureScreenshot(path);
    return new {
        path = path,
        width = UnityEngine.Screen.width,
        height = UnityEngine.Screen.height,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        frame = UnityEngine.Time.frameCount
    };
})()
"@

    $item = Wait-ForScreenshotFile $path
    return [ordered]@{
        name = $Name
        description = $Description
        path = $item.FullName
        length = $item.Length
        lastWriteTime = $item.LastWriteTime.ToString("o")
        capture = $captureResult.result.properties
    }
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Switch-Scene {
    param([string]$Scene)

    Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags InstanceFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    Type sceneSwitcherType = Type.GetType("MajdataPlay.SceneSwitcher, Assembly-CSharp", true);
    object switcher = UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (switcher != null) {
        sceneSwitcherType.GetMethod("SwitchScene", InstanceFlags).Invoke(switcher, new object[] { "$Scene", true });
    }

    return new { requested = switcher != null, scene = "$Scene" };
})()
"@ | Out-Null
}

function Add-Step {
    param(
        [System.Collections.Generic.List[object]]$Steps,
        [string]$Name,
        [object]$Assertions,
        [object]$Screenshot
    )

    $Steps.Add([ordered]@{
        name = $Name
        assertions = $Assertions
        screenshot = $Screenshot
    }) | Out-Null
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

Push-Location $GameRoot
try {
    powershell.exe -Command "Start-Process '.\start-controller.bat'"
} finally {
    Pop-Location
}

Wait-ForHook

$steps = [System.Collections.Generic.List[object]]::new()

$runtimeInfoResult = Invoke-GameEval @"
new Func<object>(() => {
    return new {
        applicationVersion = UnityEngine.Application.version,
        unityAssemblyVersion = typeof(UnityEngine.Application).Assembly.GetName().Version.ToString(),
        productName = UnityEngine.Application.productName,
        currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        screenWidth = UnityEngine.Screen.width,
        screenHeight = UnityEngine.Screen.height
    };
})()
"@
$runtimeInfo = $runtimeInfoResult.result.properties

$collectionReady = $null
$deadline = (Get-Date).AddSeconds(90)
do {
    Start-Sleep -Seconds 1
    $collectionReadyResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("DiagnosticsSnapshot").Invoke(null, null);
    var names = MajdataPlay.SongStorage.Collections.Select(c => c.Name).ToArray();
    return new {
        snapshot = snapshot,
        collectionCount = names.Length,
        hasAll = names.Any(n => n == "All"),
        hasFavorites = names.Any(n => n == "MyFavorites"),
        hasRandomRecommended = names.Any(n => n == "Random Recommended")
    };
})()
"@
    $collectionReady = $collectionReadyResult.result.properties
    if ($collectionReady.hasAll -and $collectionReady.hasFavorites -and $collectionReady.hasRandomRecommended) {
        break
    }
} while ((Get-Date) -lt $deadline)

Assert-True ($collectionReady.hasAll -and $collectionReady.hasFavorites -and $collectionReady.hasRandomRecommended) "Startup readiness canary failed: $($collectionReady.snapshot)"

Switch-Scene "Setting"
Start-Sleep -Seconds 1

$settings = $null
$deadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Seconds 1
    $settingsResult = Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags InstanceFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    Type managerType = Type.GetType("MajdataPlay.Scenes.Setting.SettingManager, Assembly-CSharp", true);
    UnityEngine.Component manager = UnityEngine.Resources.FindObjectsOfTypeAll(managerType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (manager == null) {
        return new { found = false, order = new string[0], mapListFirst = false, gameSecond = false, activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name };
    }

    var menus = (Array)managerType.GetField("menus", InstanceFlags).GetValue(manager);
    string[] order = menus.Cast<object>().Select(menu => (string)menu.GetType().GetProperty("Name").GetValue(menu, null)).ToArray();
    return new {
        found = true,
        order = order,
        mapListFirst = order.Length > 0 && order[0] == "Map List",
        gameSecond = order.Length > 1 && order[1] == "Game",
        activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
    };
})()
"@
    $settings = $settingsResult.result.properties
    if ($settings.found -and $settings.mapListFirst -and $settings.gameSecond) {
        break
    }
} while ((Get-Date) -lt $deadline)

Assert-True $settings.found "Map List settings canary failed: SettingManager was not found."
Assert-True ($settings.mapListFirst -and $settings.gameSecond) "Map List settings canary failed: order was $($settings.order -join ', ')"
$settingsShot = Capture-ProductionScreenshot "latest-map-list-settings-production" "Production Map List settings group before Game."
Add-Step $steps "map-list-settings" ([ordered]@{ startup = $collectionReady; settings = $settings }) $settingsShot

Switch-Scene "List"
Start-Sleep -Seconds 5

Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    return new { grouping = "Default" };
})()
"@ | Out-Null

Start-Sleep -Seconds 2

$metadataPrepareResult = Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    var collections = MajdataPlay.SongStorage.Collections;
    int index = Array.FindIndex(collections, c => c != null && c.Count > 0 && c.Name != "Random Recommended");
    if (index < 0) {
        throw new Exception("No nonempty collection was available for metadata screenshot.");
    }

    MajdataPlay.SongStorage.CollectionIndex = index;
    collections[index].Index = 0;
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (coverList == null) {
        throw new Exception("Active CoverListDisplayer was not found.");
    }

    coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
    var slide = coverListType.GetMethod("SlideListInternal", Flags);
    if (slide != null) {
        slide.Invoke(coverList, new object[] { 0 });
    }

    return new { collection = collections[index].Name, song = collections[index].Current.Title };
})()
"@

Start-Sleep -Seconds 2

$metadataResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bigType = Type.GetType("MajdataPlay.Scenes.List.CoverBigDisplayer, Assembly-CSharp", true);
    UnityEngine.Component big = UnityEngine.Resources.FindObjectsOfTypeAll(bigType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (big == null) {
        return new { found = false, text = "", listStillPresent = false, collection = "", song = "" };
    }

    TMPro.TMP_Text line = big.GetComponentsInChildren<TMPro.TMP_Text>(true)
        .FirstOrDefault(t => t != null && t.gameObject != null && t.gameObject.name == "QoLSelectedSongMetadataLine");
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    bool listStillPresent = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .Any(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);

    return new {
        found = line != null && line.gameObject.activeInHierarchy,
        text = line == null ? "" : line.text,
        listStillPresent = listStillPresent,
        collection = MajdataPlay.SongStorage.Collections[MajdataPlay.SongStorage.CollectionIndex].Name,
        song = MajdataPlay.SongStorage.Collections[MajdataPlay.SongStorage.CollectionIndex].Current.Title
    };
})()
"@
$metadata = $metadataResult.result.properties
Assert-True $metadata.found "Selected-song metadata canary failed: metadata line was not found."
Assert-True $metadata.listStillPresent "Selected-song metadata canary failed: list UI was not present."
Assert-True (($metadata.text -like "* | *") -and ($metadata.text -like "* diffs | *")) "Selected-song metadata canary failed: unexpected text '$($metadata.text)'."
$metadataShot = Capture-ProductionScreenshot "latest-song-info-line-production" "Production selected-song metadata line."
Add-Step $steps "selected-song-metadata" ([ordered]@{ prepare = $metadataPrepareResult.result.properties; metadata = $metadata }) $metadataShot

$scorePrepareResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    var collections = MajdataPlay.SongStorage.Collections;
    Type scoreManagerType = Type.GetType("MajdataPlay.ScoreManager, Assembly-CSharp", true);
    Type chartLevelType = Type.GetType("MajdataPlay.ChartLevel, Assembly-CSharp", true);
    var getScore = scoreManagerType.GetMethod("GetScore", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

    int bestCollectionIndex = -1;
    int bestSongIndex = -1;
    int bestDiff = -1;
    double bestDx = -1.0;
    long bestPlayCount = 0;
    bool preferredSRange = false;
    object bestSong = null;

    for (int collectionIndex = 0; collectionIndex < collections.Length; collectionIndex++) {
        var collection = collections[collectionIndex];
        if (collection == null || collection.Count <= 0 || collection.Name == "Random Recommended") {
            continue;
        }

        var songs = collection.ToArray();
        for (int songIndex = 0; songIndex < songs.Length; songIndex++) {
            for (int diff = 0; diff <= 4; diff++) {
                object level = Enum.ToObject(chartLevelType, diff);
                object score = getScore.Invoke(null, new object[] { songs[songIndex], level });
                long playCount = (long)score.GetType().GetProperty("PlayCount").GetValue(score, null);
                if (playCount <= 0) {
                    continue;
                }

                object acc = score.GetType().GetProperty("Acc").GetValue(score, null);
                double dx = (double)acc.GetType().GetProperty("DX").GetValue(acc, null);
                bool isSRange = dx >= 97.0;
                if (bestCollectionIndex < 0 || (isSRange && !preferredSRange) || (isSRange == preferredSRange && dx > bestDx)) {
                    bestCollectionIndex = collectionIndex;
                    bestSongIndex = songIndex;
                    bestDiff = diff;
                    bestDx = dx;
                    bestPlayCount = playCount;
                    preferredSRange = isSRange;
                    bestSong = songs[songIndex];
                    if (dx >= 97.0 && dx < 99.0) {
                        goto Found;
                    }
                }
            }
        }
    }

Found:
    if (bestCollectionIndex < 0) {
        throw new Exception("No played chart was found for score/rank screenshot.");
    }

    MajdataPlay.SongStorage.CollectionIndex = bestCollectionIndex;
    collections[bestCollectionIndex].Index = bestSongIndex;

    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (coverList == null) {
        throw new Exception("Active CoverListDisplayer was not found.");
    }

    coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
    coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { bestDiff });
    var slide = coverListType.GetMethod("SlideListInternal", Flags);
    if (slide != null) {
        slide.Invoke(coverList, new object[] { bestSongIndex });
    }

    return new {
        ok = true,
        collection = collections[bestCollectionIndex].Name,
        song = bestSong.GetType().GetProperty("Title").GetValue(bestSong, null),
        songIndex = bestSongIndex,
        difficulty = bestDiff,
        dx = bestDx,
        playCount = bestPlayCount,
        preferredSRange = preferredSRange,
        error = ""
    };
    } catch (Exception ex) {
        return new { ok = false, collection = "", song = "", songIndex = -1, difficulty = -1, dx = -1.0, playCount = 0L, preferredSRange = false, error = ex.ToString() };
    }
})()
"@
$scorePrepare = $scorePrepareResult.result.properties
Assert-True $scorePrepare.ok "Score/rank screenshot canary failed: $($scorePrepare.error)"

Start-Sleep -Seconds 2

$scoreUiResult = Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;
    Type bigType = Type.GetType("MajdataPlay.Scenes.List.CoverBigDisplayer, Assembly-CSharp", true);
    UnityEngine.Component big = UnityEngine.Resources.FindObjectsOfTypeAll(bigType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (big == null) {
        return new { found = false, metadata = "", archieveRate = "", rank = "", scoreVisible = false, rankVisible = false };
    }

    TMPro.TMP_Text line = big.GetComponentsInChildren<TMPro.TMP_Text>(true)
        .FirstOrDefault(t => t != null && t.gameObject != null && t.gameObject.name == "QoLSelectedSongMetadataLine");
    var archieveRate = (TMPro.TMP_Text)bigType.GetField("_archieveRate", Flags).GetValue(big);
    var rank = (TMPro.TMP_Text)bigType.GetField("_rank", Flags).GetValue(big);
    return new {
        found = line != null && line.gameObject.activeInHierarchy,
        metadata = line == null ? "" : line.text,
        archieveRate = archieveRate == null ? "" : archieveRate.text,
        rank = rank == null ? "" : rank.text,
        scoreVisible = archieveRate != null && archieveRate.enabled && archieveRate.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(archieveRate.text),
        rankVisible = rank != null && rank.enabled && rank.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(rank.text)
    };
})()
"@
$scoreUi = $scoreUiResult.result.properties
Assert-True $scoreUi.found "Score/rank screenshot canary failed: metadata line was not visible."
Assert-True ($scoreUi.scoreVisible -and $scoreUi.rankVisible) "Score/rank screenshot canary failed: score/rank text was not visible. score='$($scoreUi.archieveRate)' rank='$($scoreUi.rank)'"
$scoreShot = Capture-ProductionScreenshot "latest-song-info-line-score-rank-production" "Production selected-song metadata with visible score/rank."
Add-Step $steps "selected-song-metadata-score-rank" ([ordered]@{ target = $scorePrepare; ui = $scoreUi }) $scoreShot

$statusShownResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    bool shown = (bool)bridgeType.GetMethod("ShowStatusForDiagnostics").Invoke(null, new object[] { "Calculating BPM for 4/18 songs..." });
    string snapshot = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    return new { shown = shown, snapshot = snapshot };
})()
"@
Start-Sleep -Seconds 1
$statusVisibleResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    return new { snapshot = snapshot };
})()
"@
$statusVisible = $statusVisibleResult.result.properties
Assert-True (($statusVisible.snapshot -like "*statusVisible=True*") -and ($statusVisible.snapshot -like "*Calculating BPM for 4/18 songs...*")) "Hydration status screenshot canary failed: $($statusVisible.snapshot)"
$hydrationShot = Capture-ProductionScreenshot "latest-hydration-progress-production" "Production hydration progress/status overlay."
Start-Sleep -Seconds 4
$statusHiddenResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    return new { snapshot = snapshot };
})()
"@
$statusHidden = $statusHiddenResult.result.properties
Assert-True ($statusHidden.snapshot -like "*statusVisible=False*") "Hydration status screenshot canary failed: status did not hide after idle. $($statusHidden.snapshot)"
Add-Step $steps "hydration-status" ([ordered]@{ show = $statusShownResult.result.properties; visible = $statusVisible; hidden = $statusHidden }) $hydrationShot

$randomPrepareResult = Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;
    var collections = MajdataPlay.SongStorage.Collections;
    int randomIndex = Array.FindIndex(collections, c => c != null && c.Name == "Random Recommended");
    if (randomIndex < 0) {
        throw new Exception("Random Recommended collection was not found.");
    }

    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);

    return new { randomIndex = randomIndex, collectionCount = collections.Length };
})()
"@
$randomPrepare = $randomPrepareResult.result.properties

$randomTile = $null
for ($position = 0; $position -lt ([int]$randomPrepare.collectionCount + 12); $position++) {
    Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    var slide = coverListType.GetMethod("SlideListInternal", Flags);
    if (slide != null) {
        slide.Invoke(coverList, new object[] { $position });
    }

    return new { position = $position };
})()
"@ | Out-Null

    Start-Sleep -Milliseconds 350

    $randomTileResult = Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;
    var collections = MajdataPlay.SongStorage.Collections;
    int randomIndex = Array.FindIndex(collections, c => c != null && c.Name == "Random Recommended");
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    var coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<MajdataPlay.Scenes.List.CoverListDisplayer>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    string selectedName = coverList == null || coverList.SelectedCollection == null ? "" : coverList.SelectedCollection.Name;
    Type folderCoverType = typeof(MajdataPlay.Scenes.List.CoverListDisplayer).Assembly.GetType("MajdataPlay.Scenes.List.FolderCoverSmallDisplayer");
    var tiles = UnityEngine.Resources.FindObjectsOfTypeAll(folderCoverType).Cast<object>().ToArray();
    object targetTile = null;
    foreach (object tile in tiles) {
        var bound = folderCoverType.GetField("_boundCollection", Flags).GetValue(tile);
        if (bound != null && ((MajdataPlay.Collections.SongCollection)bound).Name == "Random Recommended") {
            targetTile = tile;
            break;
        }
    }

    if (targetTile == null) {
        return new { found = false, randomIndex = randomIndex, probePosition = $position, text = "", iconActive = false, selectedCollection = selectedName };
    }

    TMPro.TextMeshProUGUI folderText = (TMPro.TextMeshProUGUI)folderCoverType.GetField("_folderText", Flags).GetValue(targetTile);
    UnityEngine.GameObject icon = (UnityEngine.GameObject)folderCoverType.GetField("_icon", Flags).GetValue(targetTile);
    return new {
        found = true,
        randomIndex = randomIndex,
        probePosition = $position,
        text = folderText == null ? "" : folderText.text,
        iconActive = icon != null && icon.activeSelf,
        selectedCollection = selectedName
    };
})()
"@
    $randomTile = $randomTileResult.result.properties
    if ($randomTile.found -and $randomTile.selectedCollection -eq "Random Recommended") {
        break
    }
}

Assert-True $randomTile.found "Random Recommended tile canary failed: tile was not found."
Assert-True ($randomTile.selectedCollection -eq "Random Recommended") "Random Recommended tile canary failed: selected collection was '$($randomTile.selectedCollection)'."
Assert-True ($randomTile.text -eq "Random`nRecommended") "Random Recommended tile canary failed: tile text was '$($randomTile.text)'."
Assert-True $randomTile.iconActive "Random Recommended tile canary failed: online icon was not active."
$randomTileShot = Capture-ProductionScreenshot "latest-random-recommended-folder-production" "Production Random Recommended folder tile."
Add-Step $steps "random-recommended-folder-tile" ([ordered]@{ prepare = $randomPrepare; tile = $randomTile }) $randomTileShot

Start-Sleep -Seconds 1
$refreshResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    return new { snapshot = snapshot };
})()
"@
$refresh = $refreshResult.result.properties
Assert-True ($refresh.snapshot -like "*Long press refresh to get new recommendations*") "Random Recommended refresh status canary failed: $($refresh.snapshot)"
$refreshShot = Capture-ProductionScreenshot "latest-random-recommended-refresh-production" "Production Random Recommended refresh instruction/status."
Add-Step $steps "random-recommended-refresh-status" $refresh $refreshShot

$gitCommit = (& git -C $ProjectRoot rev-parse HEAD).Trim()
$artifact = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    gitCommit = $gitCommit
    projectRoot = $ProjectRoot.Path
    gameRoot = $GameRoot
    hookPort = $HookPort
    runtime = $runtimeInfo
    screenshotDirectory = (Resolve-Path $ScreenshotDir).Path
    steps = $steps
}

$artifact | ConvertTo-Json -Depth 20 | Set-Content -Path $ArtifactPath -Encoding UTF8

Write-Host "Production UI screenshots passed. Artifact: $ArtifactPath"
$artifact | ConvertTo-Json -Depth 20
