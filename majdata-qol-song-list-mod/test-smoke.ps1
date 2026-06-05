$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$HookPort = 17444
$HookUrl = "http://127.0.0.1:$HookPort/eval-isolated"

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

Push-Location $GameRoot
try {
    powershell.exe -Command "Start-Process '.\start-controller.bat'"
} finally {
    Pop-Location
}

Wait-ForHook

function Read-CollectionState {
    $result = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("DiagnosticsSnapshot").Invoke(null, null);
    var names = MajdataPlay.SongStorage.Collections.Select(c => c.Name).ToArray();
    return new {
        snapshot = snapshot,
        collectionCount = names.Length,
        hasAll = names.Any(n => n == "All"),
        hasFavorites = names.Any(n => n == "MyFavorites"),
        hasRandomRecommended = names.Any(n => n == "Random Recommended"),
        hasDifficultyBracketFolder = names.Any(n => n == "Easy" || n == "Basic" || n == "Advance" || n == "Expert" || n == "Master" || n == "ReMaster" || n == "UTAGE" || n == "Other")
    };
})()
"@
    return $result.result.properties
}

$collection = $null
$deadline = (Get-Date).AddSeconds(90)
do {
    Start-Sleep -Seconds 1
    $collection = Read-CollectionState
    if ($collection.hasAll -and $collection.hasFavorites -and $collection.hasRandomRecommended) {
        break
    }
} while ((Get-Date) -lt $deadline)

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
        sceneSwitcherType.GetMethod("SwitchScene", InstanceFlags).Invoke(switcher, new object[] { "Setting", true });
    }

    return new { requested = switcher != null };
})()
"@ | Out-Null

Start-Sleep -Seconds 3

$settingResult = Invoke-GameEval @"
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
        return new { found = false, order = new string[0], mapListFirst = false, gameSecond = false };
    }

    var menus = (Array)managerType.GetField("menus", InstanceFlags).GetValue(manager);
    string[] order = menus.Cast<object>().Select(menu => (string)menu.GetType().GetProperty("Name").GetValue(menu, null)).ToArray();
    return new {
        found = true,
        order = order,
        mapListFirst = order.Length > 0 && order[0] == "Map List",
        gameSecond = order.Length > 1 && order[1] == "Game"
    };
})()
"@

$setting = $settingResult.result.properties

if (-not $collection.hasAll) {
    throw "Smoke failed: All collection was not present."
}
if (-not $collection.hasFavorites) {
    throw "Smoke failed: MyFavorites collection was not present."
}
if (-not $collection.hasRandomRecommended) {
    throw "Smoke failed: Random Recommended collection was not present."
}

$groupingSetResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    bool set = (bool)bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "DifficultyBracket" });
    return new { groupingSet = set, error = set ? "" : "SetGroupingModeForDiagnostics returned false." };
    } catch (Exception ex) {
        return new { groupingSet = false, error = ex.ToString() };
    }
})()
"@
$groupingSet = $groupingSetResult.result.properties
if (-not $groupingSet.groupingSet) {
    throw "Smoke failed: could not set representative grouping mode. $($groupingSet.error)"
}

$groupedCollection = $null
$deadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Seconds 1
    $groupedCollection = Read-CollectionState
    if ($groupedCollection.hasDifficultyBracketFolder -and $groupedCollection.hasRandomRecommended) {
        break
    }
} while ((Get-Date) -lt $deadline)

if (-not $groupedCollection.hasDifficultyBracketFolder) {
    throw "Smoke failed: representative difficulty grouping folder was not present."
}
if (-not $groupedCollection.hasRandomRecommended) {
    throw "Smoke failed: Random Recommended was not present in representative grouping mode."
}

if (-not $setting.found) {
    throw "Smoke failed: SettingManager was not found."
}
if (-not $setting.mapListFirst -or -not $setting.gameSecond) {
    throw "Smoke failed: Map List did not appear before Game. Order: $($setting.order -join ', ')"
}

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
        sceneSwitcherType.GetMethod("SwitchScene", InstanceFlags).Invoke(switcher, new object[] { "List", true });
    }

    return new { requested = switcher != null };
})()
"@ | Out-Null

Start-Sleep -Seconds 5

Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    var collections = MajdataPlay.SongStorage.Collections;
    int index = Array.FindIndex(collections, c => c != null && c.Count > 0 && c.Name != "Random Recommended");
    if (index < 0) {
        throw new Exception("No nonempty collection was available for metadata smoke.");
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
"@ | Out-Null

Start-Sleep -Seconds 2

$metadataResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bigType = Type.GetType("MajdataPlay.Scenes.List.CoverBigDisplayer, Assembly-CSharp", true);
    UnityEngine.Component big = UnityEngine.Resources.FindObjectsOfTypeAll(bigType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (big == null) {
        return new { found = false, text = "", listStillPresent = false };
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
        listStillPresent = listStillPresent
    };
})()
"@
$metadata = $metadataResult.result.properties
if (-not $metadata.found) {
    throw "Smoke failed: selected-song metadata line was not found."
}
if (-not $metadata.listStillPresent) {
    throw "Smoke failed: list UI was replaced or missing after metadata patch."
}
if (-not (($metadata.text -like "* | *") -and ($metadata.text -like "* diffs | *"))) {
    throw "Smoke failed: metadata line did not match expected format. Text: $($metadata.text)"
}

Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    bool shown = (bool)bridgeType.GetMethod("ShowStatusForDiagnostics").Invoke(null, new object[] { "Calculating BPM for 4/18 songs..." });
    return new { shown = shown };
})()
"@ | Out-Null

Start-Sleep -Seconds 1

$statusVisibleResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    return new { snapshot = snapshot };
})()
"@
$statusVisibleSnapshot = $statusVisibleResult.result.properties.snapshot
if (-not ($statusVisibleSnapshot -like "*statusVisible=True*" -and $statusVisibleSnapshot -like "*Calculating BPM for 4/18 songs...*")) {
    throw "Smoke failed: hydration status overlay was not visible. Snapshot: $statusVisibleSnapshot"
}

Start-Sleep -Seconds 4

$statusHiddenResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    return new { snapshot = snapshot };
})()
"@
$statusHiddenSnapshot = $statusHiddenResult.result.properties.snapshot
if (-not ($statusHiddenSnapshot -like "*statusVisible=False*")) {
    throw "Smoke failed: hydration status overlay did not hide after idle. Snapshot: $statusHiddenSnapshot"
}

Invoke-GameEval @"
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

    MajdataPlay.SongStorage.CollectionIndex = randomIndex;
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
    var slide = coverListType.GetMethod("SlideListInternal", Flags);
    if (slide != null) {
        slide.Invoke(coverList, new object[] { randomIndex });
    }

    return new { randomIndex = randomIndex };
})()
"@ | Out-Null

Start-Sleep -Seconds 2

$refreshStatusResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    return new { snapshot = snapshot };
})()
"@
$refreshSnapshot = $refreshStatusResult.result.properties.snapshot
if (-not ($refreshSnapshot -like "*Long press refresh to get new recommendations*")) {
    throw "Smoke failed: Random Recommended refresh instruction was not shown. Snapshot: $refreshSnapshot"
}

Write-Host "Smoke passed: default folders, grouping, settings order, selected-song metadata, hydration status overlay, and Random Recommended refresh status all render."
