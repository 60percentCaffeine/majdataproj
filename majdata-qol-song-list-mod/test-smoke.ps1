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

$hydratedMetadataPrepareResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string beforeCollections = string.Join("|", MajdataPlay.SongStorage.Collections.Select(c => c == null ? "<null>" : c.Name));
    int beforeCollectionIndex = MajdataPlay.SongStorage.CollectionIndex;
    string beforeHash = MajdataPlay.SongStorage.WorkingCollection.Current == null ? "" : MajdataPlay.SongStorage.WorkingCollection.Current.Hash;
    bool set = (bool)bridgeType.GetMethod("SetSelectedSongMetadataForDiagnostics").Invoke(null, new object[] { "02:34", "145" });
    return new {
        set = set,
        beforeCollections = beforeCollections,
        beforeCollectionIndex = beforeCollectionIndex,
        beforeHash = beforeHash
    };
})()
"@
$hydratedMetadataPrepare = $hydratedMetadataPrepareResult.result.properties
if (-not $hydratedMetadataPrepare.set) {
    throw "Smoke failed: selected-song hydrated metadata diagnostic value could not be set."
}

Start-Sleep -Seconds 1

$hydratedMetadataResult = Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    string snapshot = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    string afterCollections = string.Join("|", MajdataPlay.SongStorage.Collections.Select(c => c == null ? "<null>" : c.Name));
    int afterCollectionIndex = MajdataPlay.SongStorage.CollectionIndex;
    string afterHash = MajdataPlay.SongStorage.WorkingCollection.Current == null ? "" : MajdataPlay.SongStorage.WorkingCollection.Current.Hash;
    return new {
        snapshot = snapshot,
        afterCollections = afterCollections,
        afterCollectionIndex = afterCollectionIndex,
        afterHash = afterHash
    };
})()
"@
$hydratedMetadata = $hydratedMetadataResult.result.properties
if (-not (($hydratedMetadata.snapshot -like "*02:34*") -and ($hydratedMetadata.snapshot -like "*145BPM*"))) {
    throw "Smoke failed: hydrated selected-song metadata did not display duration and BPM. Snapshot: $($hydratedMetadata.snapshot)"
}
if ($hydratedMetadata.afterCollections -ne $hydratedMetadataPrepare.beforeCollections -or $hydratedMetadata.afterCollectionIndex -ne $hydratedMetadataPrepare.beforeCollectionIndex -or $hydratedMetadata.afterHash -ne $hydratedMetadataPrepare.beforeHash) {
    throw "Smoke failed: hydrated metadata changed list collections or cursor. Before=$($hydratedMetadataPrepare.beforeCollections) After=$($hydratedMetadata.afterCollections) BeforeIndex=$($hydratedMetadataPrepare.beforeCollectionIndex) AfterIndex=$($hydratedMetadata.afterCollectionIndex) BeforeHash=$($hydratedMetadataPrepare.beforeHash) AfterHash=$($hydratedMetadata.afterHash)"
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

$randomRecommendedResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    Func<string> randomSnapshot = () => (string)bridgeType.GetMethod("RandomRecommendedDiagnosticsSnapshot").Invoke(null, null);
    string initialSnapshot = randomSnapshot();
    var initial = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "Random Recommended");
    int initialCount = initial == null ? 0 : initial.Count;
    string firstRefresh = (string)bridgeType.GetMethod("RefreshRandomRecommendedForDiagnostics").Invoke(null, new object[] { true });
    string firstSnapshot = randomSnapshot();
    var first = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "Random Recommended");
    string firstHashes = first == null ? "" : string.Join("|", first.ToArray().Select(song => song.Hash).Take(8));
    string secondRefresh = (string)bridgeType.GetMethod("RefreshRandomRecommendedForDiagnostics").Invoke(null, new object[] { true });
    string secondSnapshot = randomSnapshot();
    var second = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "Random Recommended");
    string secondHashes = second == null ? "" : string.Join("|", second.ToArray().Select(song => song.Hash).Take(8));

    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "Rank" });
    bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
    var groupedRandom = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "Random Recommended");

    return new {
        ok = true,
        initialSnapshot = initialSnapshot,
        initialCount = initialCount,
        firstRefresh = firstRefresh,
        firstSnapshot = firstSnapshot,
        firstCount = first == null ? 0 : first.Count,
        firstHashes = firstHashes,
        secondRefresh = secondRefresh,
        secondSnapshot = secondSnapshot,
        secondCount = second == null ? 0 : second.Count,
        secondHashes = secondHashes,
        varied = firstHashes != secondHashes,
        groupedPresent = groupedRandom != null,
        groupedCount = groupedRandom == null ? 0 : groupedRandom.Count,
        error = ""
    };
    } catch (Exception ex) {
        return new { ok = false, initialSnapshot = "", initialCount = 0, firstRefresh = "", firstSnapshot = "", firstCount = 0, firstHashes = "", secondRefresh = "", secondSnapshot = "", secondCount = 0, secondHashes = "", varied = false, groupedPresent = false, groupedCount = 0, error = ex.ToString() };
    }
})()
"@
$randomRecommended = $randomRecommendedResult.result.properties
if (-not $randomRecommended.ok -or $randomRecommended.initialCount -lt 1 -or $randomRecommended.firstCount -lt 1 -or $randomRecommended.secondCount -lt 1 -or -not $randomRecommended.varied -or -not $randomRecommended.groupedPresent -or $randomRecommended.groupedCount -lt 1 -or -not ($randomRecommended.firstRefresh -like "*fallback*")) {
    throw "Smoke failed: Random Recommended did not populate/refresh/fallback correctly. Initial=$($randomRecommended.initialSnapshot) First=$($randomRecommended.firstRefresh) FirstHashes=$($randomRecommended.firstHashes) Second=$($randomRecommended.secondRefresh) SecondHashes=$($randomRecommended.secondHashes) GroupedPresent=$($randomRecommended.groupedPresent) GroupedCount=$($randomRecommended.groupedCount) Error=$($randomRecommended.error)"
}

$websiteCollectionResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    Action<string> setScope = value => bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { value });
    Action apply = () => bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "No" });
    setScope("Mixed");
    apply();

    var all = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "All");
    var rows = all == null ? new MajdataPlay.ISongDetail[0] : all.ToArray().Where(song => song != null && !string.IsNullOrWhiteSpace(song.Hash)).ToArray();
    var local = rows.FirstOrDefault(song => !song.IsOnline);
    var online = rows.FirstOrDefault(song => song.IsOnline);
    if (local == null || online == null) {
        throw new Exception("Website collection canary requires both local and online rows.");
    }

    string collectionName = "QoL Website Smoke";
    string install = (string)bridgeType.GetMethod("InstallWebsiteCollectionForDiagnostics").Invoke(null, new object[] { collectionName, local.Hash + "|" + online.Hash + "|missing-website-hash", 4 });
    Func<string, int> collectionCount = name => {
        var collection = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == name);
        return collection == null ? -1 : collection.Count;
    };
    int mixedCount = collectionCount(collectionName);

    int index = Array.FindIndex(MajdataPlay.SongStorage.Collections, c => c != null && c.Name == collectionName);
    MajdataPlay.SongStorage.CollectionIndex = index;
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (coverList != null) {
        coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
        var slide = coverListType.GetMethod("SlideListInternal", Flags);
        if (slide != null) {
            slide.Invoke(coverList, new object[] { index });
        }
    }

    object bridge = bridgeType.GetProperty("Active").GetValue(null, null);
    bridgeType.GetMethod("PatchStatusOverlay", Flags).Invoke(bridge, new object[0]);
    string statusBefore = (string)bridgeType.GetMethod("UiDiagnosticsSnapshot").Invoke(null, null);
    setScope("DownloadedOnly");
    apply();
    int downloadedCount = collectionCount(collectionName);
    setScope("OnlineOnly");
    apply();
    int onlineCount = collectionCount(collectionName);
    string failure = (string)bridgeType.GetMethod("SimulateWebsiteCollectionFailureForDiagnostics").Invoke(null, null);
    int retainedCount = collectionCount(collectionName);
    string diagnostics = (string)bridgeType.GetMethod("WebsiteCollectionDiagnosticsSnapshot").Invoke(null, null);

    return new {
        ok = true,
        install = install,
        localHash = local.Hash,
        onlineHash = online.Hash,
        mixedCount = mixedCount,
        downloadedCount = downloadedCount,
        onlineCount = onlineCount,
        retainedCount = retainedCount,
        failure = failure,
        diagnostics = diagnostics,
        statusBefore = statusBefore,
        error = ""
    };
    } catch (Exception ex) {
        return new { ok = false, install = "", localHash = "", onlineHash = "", mixedCount = 0, downloadedCount = 0, onlineCount = 0, retainedCount = 0, failure = "", diagnostics = "", statusBefore = "", error = ex.ToString() };
    }
})()
"@
$websiteCollection = $websiteCollectionResult.result.properties
if (-not $websiteCollection.ok -or $websiteCollection.mixedCount -lt 2 -or $websiteCollection.downloadedCount -ne 1 -or $websiteCollection.onlineCount -ne 1 -or $websiteCollection.retainedCount -ne 1 -or -not ($websiteCollection.failure -like "*retained cached*") -or -not ($websiteCollection.diagnostics -like "*QoL Website Smoke*") -or -not ($websiteCollection.statusBefore -like "*Count:2/4 resolved*")) {
    throw "Smoke failed: website collection canary failed. Install=$($websiteCollection.install) Mixed=$($websiteCollection.mixedCount) Downloaded=$($websiteCollection.downloadedCount) Online=$($websiteCollection.onlineCount) Retained=$($websiteCollection.retainedCount) Failure=$($websiteCollection.failure) Diagnostics=$($websiteCollection.diagnostics) Status=$($websiteCollection.statusBefore) Error=$($websiteCollection.error)"
}

$levelGroupingResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "DifficultyBracket" });
    var collections = MajdataPlay.SongStorage.Collections;
    var song = collections.SelectMany(c => c.ToArray()).FirstOrDefault(s => s != null && s.Levels.Length > 0 && !string.IsNullOrWhiteSpace(s.Levels[0]));
    if (song == null) {
        throw new Exception("No chart with an Easy level was available for level grouping canary.");
    }

    string expectedBucket = MajdataQolSongListMod.Core.LevelBucketizer.Bucketize(song.Levels[0]);
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (coverList != null) {
        coverListType.GetField("selectedDifficulty").SetValue(coverList, 0);
    }

    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "DifficultyLevel" });
    return new { ok = true, hash = song.Hash, title = song.Title, expectedBucket = expectedBucket, error = "" };
    } catch (Exception ex) {
        return new { ok = false, hash = "", title = "", expectedBucket = "", error = ex.ToString() };
    }
})()
"@
$levelGrouping = $levelGroupingResult.result.properties
if (-not $levelGrouping.ok) {
    throw "Smoke failed: could not prepare level grouping canary. $($levelGrouping.error)"
}

$levelBucketObserved = $false
$deadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Seconds 1
    $bucketResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    string expectedBucket = "$($levelGrouping.expectedBucket)";
    string hash = "$($levelGrouping.hash)";
    var bucket = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == expectedBucket);
    return new {
        ok = true,
        found = bucket != null,
        containsKnownChart = bucket != null && bucket.ToArray().Any(s => s.Hash == hash),
        error = ""
    };
    } catch (Exception ex) {
        return new { ok = false, found = false, containsKnownChart = false, error = ex.ToString() };
    }
})()
"@
    $bucket = $bucketResult.result.properties
    if (-not $bucket.ok) {
        throw "Smoke failed: level bucket observation failed. $($bucket.error)"
    }
    if ($bucket.found -and $bucket.containsKnownChart) {
        $levelBucketObserved = $true
        break
    }
} while ((Get-Date) -lt $deadline)

if (-not $levelBucketObserved) {
    throw "Smoke failed: level grouping did not place known chart '$($levelGrouping.title)' into expected bucket '$($levelGrouping.expectedBucket)'."
}

$sortCanaryResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    Action apply = () => bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "No" });
    bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { "Mixed" });
    apply();

    Func<MajdataPlay.Collections.SongCollection, string> firstFive = collection =>
        string.Join("|", collection.ToArray().Take(5).Select(song => song.Hash));

    var candidate = MajdataPlay.SongStorage.Collections
        .Where(collection => collection != null && collection.Name != "Random Recommended" && collection.Count >= 5)
        .Select(collection => new {
            collection,
            original = firstFive(collection),
            expected = string.Join("|", collection.ToArray()
                .OrderBy(song => string.IsNullOrWhiteSpace(song.Title) ? "\uffff" : song.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(song => song.Hash))
        })
        .FirstOrDefault(item => item.original != item.expected);

    if (candidate == null) {
        throw new Exception("No collection with a title-sort-visible first page was found.");
    }

    bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { "Title" });
    apply();

    var sortedCollection = MajdataPlay.SongStorage.Collections.FirstOrDefault(collection => collection != null && collection.Name == candidate.collection.Name);
    string observed = sortedCollection == null ? "" : firstFive(sortedCollection);
    return new {
        ok = true,
        collection = candidate.collection.Name,
        original = candidate.original,
        expected = candidate.expected,
        observed = observed,
        changed = candidate.original != observed,
        sorted = candidate.expected == observed,
        error = ""
    };
    } catch (Exception ex) {
        return new { ok = false, collection = "", original = "", expected = "", observed = "", changed = false, sorted = false, error = ex.ToString() };
    }
})()
"@
$sortCanary = $sortCanaryResult.result.properties
if (-not $sortCanary.ok -or -not $sortCanary.changed -or -not $sortCanary.sorted) {
    throw "Smoke failed: title sorting did not visibly reorder a collection. Collection=$($sortCanary.collection) Original=$($sortCanary.original) Expected=$($sortCanary.expected) Observed=$($sortCanary.observed) Error=$($sortCanary.error)"
}

$difficultyFilterResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    Action apply = () => bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { "Mixed" });

    Func<MajdataPlay.ISongDetail, int> diffCount = song => {
        int count = 0;
        var levels = song.Levels;
        for (int i = 0; i < levels.Length; i++) {
            if (!string.IsNullOrWhiteSpace(levels[i])) {
                count++;
            }
        }
        return count;
    };
    Func<int> allCount = () => {
        var all = MajdataPlay.SongStorage.Collections.FirstOrDefault(collection => collection != null && collection.Name == "All");
        var rows = all == null ? new MajdataPlay.ISongDetail[0] : all.ToArray();
        return rows.Length;
    };
    Func<int, bool> allPass = minimum => {
        var all = MajdataPlay.SongStorage.Collections.FirstOrDefault(collection => collection != null && collection.Name == "All");
        var rows = all == null ? new MajdataPlay.ISongDetail[0] : all.ToArray();
        return rows.All(song => diffCount(song) > minimum);
    };

    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "No" });
    apply();
    int noCount = allCount();
    bool noPass = allPass(-1);

    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "MoreThan1" });
    apply();
    int gt1Count = allCount();
    bool gt1Pass = allPass(1);

    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "MoreThan2" });
    apply();
    int gt2Count = allCount();
    bool gt2Pass = allPass(2);

    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "MoreThan3" });
    apply();
    int gt3Count = allCount();
    bool gt3Pass = allPass(3);

    return new {
        ok = true,
        noCount = noCount,
        moreThan1Count = gt1Count,
        moreThan2Count = gt2Count,
        moreThan3Count = gt3Count,
        allPass = noPass && gt1Pass && gt2Pass && gt3Pass,
        monotonic = noCount >= gt1Count && gt1Count >= gt2Count && gt2Count >= gt3Count,
        narrowed = noCount > gt1Count || gt1Count > gt2Count || gt2Count > gt3Count,
        error = ""
    };
    } catch (Exception ex) {
        return new { ok = false, noCount = 0, moreThan1Count = 0, moreThan2Count = 0, moreThan3Count = 0, allPass = false, monotonic = false, narrowed = false, error = ex.ToString() };
    }
})()
"@
$difficultyFilter = $difficultyFilterResult.result.properties
if (-not $difficultyFilter.ok -or -not $difficultyFilter.allPass -or -not $difficultyFilter.monotonic -or -not $difficultyFilter.narrowed) {
    throw "Smoke failed: difficulty filters did not apply. Counts No=$($difficultyFilter.noCount) >1=$($difficultyFilter.moreThan1Count) >2=$($difficultyFilter.moreThan2Count) >3=$($difficultyFilter.moreThan3Count) Error=$($difficultyFilter.error)"
}

$downloadedFilterResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    Action apply = () => bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "No" });

    Func<MajdataPlay.ISongDetail[]> allRows = () => {
        var all = MajdataPlay.SongStorage.Collections.FirstOrDefault(collection => collection != null && collection.Name == "All");
        return all == null ? new MajdataPlay.ISongDetail[0] : all.ToArray();
    };

    bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { "Mixed" });
    apply();
    var mixedRows = allRows();
    int mixedCount = mixedRows.Length;
    int mixedLocalCount = mixedRows.Count(song => !song.IsOnline);
    int mixedOnlineCount = mixedRows.Count(song => song.IsOnline);

    bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { "DownloadedOnly" });
    apply();
    var downloadedRows = allRows();
    int downloadedCount = downloadedRows.Length;
    bool downloadedAllLocal = downloadedRows.All(song => !song.IsOnline);

    bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { "OnlineOnly" });
    apply();
    var onlineRows = allRows();
    int onlineCount = onlineRows.Length;
    bool onlineAllOnline = onlineRows.All(song => song.IsOnline);

    return new {
        ok = true,
        mixedCount = mixedCount,
        mixedLocalCount = mixedLocalCount,
        mixedOnlineCount = mixedOnlineCount,
        downloadedCount = downloadedCount,
        downloadedAllLocal = downloadedAllLocal,
        onlineCount = onlineCount,
        onlineAllOnline = onlineAllOnline,
        error = ""
    };
    } catch (Exception ex) {
        return new { ok = false, mixedCount = 0, mixedLocalCount = 0, mixedOnlineCount = 0, downloadedCount = 0, downloadedAllLocal = false, onlineCount = 0, onlineAllOnline = false, error = ex.ToString() };
    }
})()
"@
$downloadedFilter = $downloadedFilterResult.result.properties
if (-not $downloadedFilter.ok -or $downloadedFilter.mixedLocalCount -lt 1 -or $downloadedFilter.mixedOnlineCount -lt 1 -or $downloadedFilter.downloadedCount -lt 1 -or -not $downloadedFilter.downloadedAllLocal -or $downloadedFilter.onlineCount -lt 1 -or -not $downloadedFilter.onlineAllOnline) {
    throw "Smoke failed: downloaded/online filters did not apply. Mixed=$($downloadedFilter.mixedCount) local=$($downloadedFilter.mixedLocalCount) online=$($downloadedFilter.mixedOnlineCount) downloaded=$($downloadedFilter.downloadedCount) onlineOnly=$($downloadedFilter.onlineCount) Error=$($downloadedFilter.error)"
}

$scoreFacetResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    Action<string> setGrouping = value => bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { value });
    Action<string> setSorting = value => bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { value });
    Action apply = () => bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "No" });
    bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { "Mixed" });
    setGrouping("Default");
    setSorting("Default");
    apply();

    var all = MajdataPlay.SongStorage.Collections.FirstOrDefault(collection => collection != null && collection.Name == "All");
    var rows = all == null ? new MajdataPlay.ISongDetail[0] : all.ToArray().Where(song => song != null && !string.IsNullOrWhiteSpace(song.Hash)).Take(3).ToArray();
    if (rows.Length < 3) {
        throw new Exception("At least three songs are required for score facet canary.");
    }

    var high = rows[0];
    var low = rows[1];
    var noPlay = rows[2];
    var setScore = bridgeType.GetMethod("SetSongScoreForDiagnostics");
    setScore.Invoke(null, new object[] { high.Hash, 100.6d, 9, "APPlus", 900000L });
    setScore.Invoke(null, new object[] { low.Hash, 12.3d, 1, "None", 100L });
    setScore.Invoke(null, new object[] { noPlay.Hash, 0.0d, 0, "None", 0L });

    setGrouping("Rank");
    setSorting("Default");
    apply();
    var rankCollections = MajdataPlay.SongStorage.Collections;
    Func<string, string, bool> containsHash = (name, hash) => {
        var collection = rankCollections.FirstOrDefault(item => item != null && item.Name == name);
        return collection != null && collection.ToArray().Any(song => song != null && song.Hash == hash);
    };
    bool highInRank = containsHash("SSS+", high.Hash);
    bool lowInRank = containsHash("C", low.Hash);
    bool noPlayInRank = containsHash("No Play", noPlay.Hash);

    Func<string, bool> pairOrdered = sortMode => {
        setGrouping("Default");
        setSorting(sortMode);
        apply();
        var sortedAll = MajdataPlay.SongStorage.Collections.FirstOrDefault(collection => collection != null && collection.Name == "All");
        var sortedRows = sortedAll == null ? new MajdataPlay.ISongDetail[0] : sortedAll.ToArray();
        int highIndex = Array.FindIndex(sortedRows, song => song != null && song.Hash == high.Hash);
        int lowIndex = Array.FindIndex(sortedRows, song => song != null && song.Hash == low.Hash);
        return highIndex >= 0 && lowIndex >= 0 && highIndex < lowIndex;
    };

    bool rankSort = pairOrdered("Rank");
    bool playCountSort = pairOrdered("PlayCount");
    bool apFcSort = pairOrdered("ApFcRank");
    bool dxScoreSort = pairOrdered("DxScore");

    return new {
        ok = true,
        highTitle = high.Title,
        lowTitle = low.Title,
        noPlayTitle = noPlay.Title,
        highInRank = highInRank,
        lowInRank = lowInRank,
        noPlayInRank = noPlayInRank,
        rankSort = rankSort,
        playCountSort = playCountSort,
        apFcSort = apFcSort,
        dxScoreSort = dxScoreSort,
        error = ""
    };
    } catch (Exception ex) {
        return new { ok = false, highTitle = "", lowTitle = "", noPlayTitle = "", highInRank = false, lowInRank = false, noPlayInRank = false, rankSort = false, playCountSort = false, apFcSort = false, dxScoreSort = false, error = ex.ToString() };
    }
})()
"@
$scoreFacet = $scoreFacetResult.result.properties
if (-not $scoreFacet.ok -or -not $scoreFacet.highInRank -or -not $scoreFacet.lowInRank -or -not $scoreFacet.noPlayInRank -or -not $scoreFacet.rankSort -or -not $scoreFacet.playCountSort -or -not $scoreFacet.apFcSort -or -not $scoreFacet.dxScoreSort) {
    throw "Smoke failed: score facets did not drive rank grouping/sorting. High=$($scoreFacet.highTitle) Low=$($scoreFacet.lowTitle) NoPlay=$($scoreFacet.noPlayTitle) highInRank=$($scoreFacet.highInRank) lowInRank=$($scoreFacet.lowInRank) noPlay=$($scoreFacet.noPlayInRank) rankSort=$($scoreFacet.rankSort) playCountSort=$($scoreFacet.playCountSort) apFcSort=$($scoreFacet.apFcSort) dxScoreSort=$($scoreFacet.dxScoreSort) Error=$($scoreFacet.error)"
}

$hydrationCanaryResult = Invoke-GameEval @"
new Func<object>(() => {
    var scheduler = new MajdataQolSongListMod.Core.HydrationScheduler();
    return new {
        gameplayAllowed = scheduler.AllowsHydration(MajdataQolSongListMod.Core.HydrationSceneState.Gameplay),
        practiceAllowed = scheduler.AllowsHydration(MajdataQolSongListMod.Core.HydrationSceneState.Practice),
        listAllowed = scheduler.AllowsHydration(MajdataQolSongListMod.Core.HydrationSceneState.List)
    };
})()
"@
$hydrationCanary = $hydrationCanaryResult.result.properties
if ($hydrationCanary.gameplayAllowed -or $hydrationCanary.practiceAllowed -or -not $hydrationCanary.listAllowed) {
    throw "Smoke failed: hydration pause/resume policy was wrong. gameplayAllowed=$($hydrationCanary.gameplayAllowed) practiceAllowed=$($hydrationCanary.practiceAllowed) listAllowed=$($hydrationCanary.listAllowed)"
}

$cacheCanaryResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    string root = System.IO.Path.Combine(System.Environment.CurrentDirectory, "UserData", "QolSmokeCache");
    if (System.IO.Directory.Exists(root)) {
        System.IO.Directory.Delete(root, true);
    }

    var store = new MajdataQolSongListMod.Core.HydrationStore(root);
    store.Write(new MajdataQolSongListMod.Core.HydrationCacheValue("smoke/hash", MajdataQolSongListMod.Core.HydrationDataKind.Bpm, "120BPM", new DateTimeOffset(DateTime.UtcNow)));
    string expectedRoot = System.IO.Path.Combine(root, MajdataQolSongListMod.Core.HydrationStore.ModCacheDirectoryName);
    string[] files = System.IO.Directory.GetFiles(expectedRoot);
    return new {
        expectedRoot = expectedRoot,
        fileCount = files.Length,
        allUnderModRoot = files.All(path => path.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase)),
        error = ""
    };
    } catch (Exception ex) {
        return new { expectedRoot = "", fileCount = 0, allUnderModRoot = false, error = ex.ToString() };
    }
})()
"@
$cacheCanary = $cacheCanaryResult.result.properties
if (-not $cacheCanary.allUnderModRoot -or $cacheCanary.fileCount -lt 1) {
    throw "Smoke failed: cache files were not confined to the mod cache root. Root: $($cacheCanary.expectedRoot) Count=$($cacheCanary.fileCount) Error=$($cacheCanary.error)"
}

Invoke-GameEval @"
new Func<object>(() => {
    Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", true);
    bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { "Default" });
    bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { "Title" });
    bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { "No" });
    bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { "Mixed" });
    bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
    return new { reset = true };
})()
"@ | Out-Null

Start-Sleep -Seconds 1

$enterGameResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;
    var collections = MajdataPlay.SongStorage.Collections;
    int index = Array.FindIndex(collections, c => c != null && c.Count > 0 && c.Name == "QoL Website Smoke");
    if (index < 0) {
        throw new Exception("No nonempty website collection was available for gameplay canary.");
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
    coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
    var slide = coverListType.GetMethod("SlideListInternal", Flags);
    if (slide != null) {
        slide.Invoke(coverList, new object[] { index });
    }
    coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
    if (slide != null) {
        slide.Invoke(coverList, new object[] { 0 });
    }

    Type listManagerType = Type.GetType("MajdataPlay.Scenes.List.ListManager, Assembly-CSharp", true);
    object listManager = UnityEngine.Resources.FindObjectsOfTypeAll(listManagerType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (listManager == null) {
        throw new Exception("Active ListManager was not found.");
    }

    listManagerType.GetMethod("EnterGame", Flags).Invoke(listManager, new object[0]);
    return new { requested = true, collection = collections[index].Name, song = collections[index].Current.Title, error = "" };
    } catch (Exception ex) {
        return new { requested = false, collection = "", song = "", error = ex.ToString() };
    }
})()
"@
$enterGame = $enterGameResult.result.properties
if (-not $enterGame.requested) {
    throw "Smoke failed: could not request list-to-gameplay flow. $($enterGame.error)"
}

$gameplayCanary = $null
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    Start-Sleep -Seconds 1
    $gameplayCanaryResult = Invoke-GameEval @"
new Func<object>(() => {
    try {
    string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    return new {
        scene = scene,
        enteredGame = scene == "Game",
        error = ""
    };
    } catch (Exception ex) {
        return new { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, enteredGame = false, error = ex.ToString() };
    }
})()
"@
    $gameplayCanary = $gameplayCanaryResult.result.properties
    if ($gameplayCanary.enteredGame) {
        break
    }
}
if (-not $gameplayCanary.enteredGame) {
    throw "Smoke failed: list-to-gameplay flow did not enter Game scene. RequestedCollection=$($enterGame.collection) RequestedSong=$($enterGame.song) Scene=$($gameplayCanary.scene) Error=$($gameplayCanary.error)"
}

Write-Host "Smoke passed: default folders, grouping, settings order, selected-song metadata, hydrated duration/BPM metadata, hydration status overlay, Random Recommended populated refresh/fallback status, website collection folders/scope/fallback, level bucket grouping, live sorting/filter/scope settings, live score/rank facets, hydration gameplay pause, mod cache path, and website collection list-to-gameplay flow all passed."
