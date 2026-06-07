param(
    [ValidateSet("Compare", "Vanilla", "Mod", "KnownBugs")]
    [string]$Mode = "Compare"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
$ModsDir = Join-Path $GameRoot "Mods"
$QolDll = Join-Path $ModsDir "MajdataQolSongListMod.dll"
$QolLibDir = Join-Path $ModsDir "MajdataQolSongListModLib"
$DisabledRoot = Join-Path $GameRoot "UserData\QolEquivalenceDisabled"
$HookPort = 17444
$HookUrl = "http://127.0.0.1:$HookPort/eval-isolated"
$ScreenshotRoot = Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\screenshots\jportal-ui-regressions"
$RunStartedAt = (Get-Date).ToUniversalTime().ToString("o")
$RunId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$RunScreenshotDir = Join-Path $ScreenshotRoot $RunId
$ArtifactPath = Join-Path $ScreenshotRoot "latest-jportal-ui-regressions.json"
$PublishedSnapshots = New-Object 'System.Collections.Generic.List[object]'

New-Item -ItemType Directory -Force -Path $RunScreenshotDir | Out-Null

function Stop-Game {
    Get-Process -Name "MajdataPlay" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

function Invoke-GameEval {
    param([string]$Code)

    $body = @{
        code = $Code
        timeoutMs = 120000
        maxDepth = 14
        maxResponseBytes = 3000000
    } | ConvertTo-Json -Compress

    Invoke-RestMethod -Uri $HookUrl -Method Post -ContentType application/json -Body $body -TimeoutSec 150
}

function Convert-ToGamePathLiteral {
    param([string]$Path)

    return $Path.Replace("\", "\\")
}

function Convert-ToCSharpStringLiteral {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    $escaped = $Value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '\r').Replace("`n", '\n').Replace("`t", '\t')
    return '"' + $escaped + '"'
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

function Capture-JportalScreenshot {
    param(
        [string]$Label,
        [string]$Name,
        [string]$Description,
        [object]$State,
        [string]$ExpectedBehavior = ""
    )

    $path = Join-Path $RunScreenshotDir "$Label-$Name.png"
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
        label = $Label
        description = $Description
        expectedBehavior = $ExpectedBehavior
        path = $item.FullName
        length = $item.Length
        lastWriteTime = $item.LastWriteTime.ToString("o")
        capture = $captureResult.result.properties
        state = $State
    }
}

function Publish-JportalArtifact {
    param([object]$Snapshot)

    $PublishedSnapshots.Add($Snapshot) | Out-Null
    $artifact = [ordered]@{
        runId = $RunId
        runStartedAt = $RunStartedAt
        screenshotDirectory = (Resolve-Path $RunScreenshotDir).Path
        snapshots = $PublishedSnapshots
    }

    $runManifest = Join-Path $RunScreenshotDir "manifest.json"
    $artifact | ConvertTo-Json -Depth 16 | Set-Content -Path $runManifest -Encoding UTF8
    $artifact | ConvertTo-Json -Depth 16 | Set-Content -Path $ArtifactPath -Encoding UTF8
    Write-Host "JPORTAL UI regression screenshots: $runManifest"
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

function Disable-QolMod {
    New-Item -ItemType Directory -Force -Path $DisabledRoot | Out-Null
    if (Test-Path $QolDll) {
        Move-Item -Force $QolDll (Join-Path $DisabledRoot "MajdataQolSongListMod.dll")
    }
    if (Test-Path $QolLibDir) {
        if (Test-Path (Join-Path $DisabledRoot "MajdataQolSongListModLib")) {
            Remove-Item -Recurse -Force (Join-Path $DisabledRoot "MajdataQolSongListModLib")
        }
        Move-Item -Force $QolLibDir (Join-Path $DisabledRoot "MajdataQolSongListModLib")
    }
}

function Install-Hook {
    & (Join-Path $ProjectRoot "mod-test-tools\test-hook-mod\install.ps1")

    $HookConfigDir = Join-Path $GameRoot "UserData\TestHookMod"
    New-Item -ItemType Directory -Force -Path $HookConfigDir | Out-Null
    @{
        host = "127.0.0.1"
        port = $HookPort
        replEnabled = $false
    } | ConvertTo-Json -Compress | Set-Content -Path (Join-Path $HookConfigDir "config.json") -Encoding UTF8
}

function Start-GameAndOpenList {
    Push-Location $GameRoot
    try {
        powershell.exe -Command "Start-Process '.\start-controller.bat'"
    } finally {
        Pop-Location
    }

    Wait-ForHook

    $bootDeadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Seconds 1
        $ready = Invoke-GameEval @"
new Func<object>(() => {
    bool ready = MajdataPlay.SongStorage.Collections != null &&
        MajdataPlay.SongStorage.Collections.Any(c => c != null && c.Count > 0);
    return new { ready = ready };
})()
"@
        if ($ready.result.properties.ready) {
            break
        }
    } while ((Get-Date) -lt $bootDeadline)

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

    $listDeadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Seconds 1
        $listReady = Invoke-GameEval @"
new Func<object>(() => {
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", false);
    bool ready = coverListType != null &&
        UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .Any(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    return new {
        ready = ready,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
    };
})()
"@
        if ($listReady.result.properties.ready) {
            return
        }
    } while ((Get-Date) -lt $listDeadline)

    throw "Timed out waiting for List scene."
}

function Ensure-ListScene {
    $check = Invoke-GameEval @"
new Func<object>(() => {
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", false);
    bool ready = coverListType != null &&
        UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .Any(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (ready) {
        return new {
            ready = true,
            requested = false,
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        };
    }

    const System.Reflection.BindingFlags InstanceFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;
    Type sceneSwitcherType = Type.GetType("MajdataPlay.SceneSwitcher, Assembly-CSharp", false);
    object switcher = sceneSwitcherType == null ? null : UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (switcher != null) {
        sceneSwitcherType.GetMethod("SwitchScene", InstanceFlags).Invoke(switcher, new object[] { "List", true });
    }
    return new {
        ready = false,
        requested = switcher != null,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
    };
})()
"@

    if ($check.result.properties.ready) {
        return
    }

    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Seconds 1
        $listReady = Invoke-GameEval @"
new Func<object>(() => {
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", false);
    bool ready = coverListType != null &&
        UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .Any(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    return new {
        ready = ready,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
    };
})()
"@
        if ($listReady.result.properties.ready) {
            return
        }
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for List scene after requesting it from screenshot driver."
}

function Invoke-JportalAction {
    param([ValidateSet("OpenDefault", "Scroll", "Difficulty", "PrepareRank", "OpenRank", "DifficultyToBasic")]
          [string]$Action)

    Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;
        const System.Reflection.BindingFlags StaticFlags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;

        Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
        object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (coverList == null) {
            return new { ok = false, error = "CoverListDisplayer not found" };
        }

        Action fixedTicks = () => {
            var fixedUpdate = coverListType.GetMethod("FixedUpdate", Flags);
            if (fixedUpdate != null) {
                for (int i = 0; i < 40; i++) fixedUpdate.Invoke(coverList, new object[0]);
            }
        };

        var collections = MajdataPlay.SongStorage.Collections;
        int targetIndex = Array.FindIndex(collections, c => c != null && string.Equals(c.Name, "JPORTAL", StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0) {
            return new { ok = false, error = "JPORTAL collection not found" };
        }

        if ("$Action" == "PrepareRank") {
            var songs = collections[targetIndex].ToArray().Take(5).ToArray();
            if (songs.Length < 5) {
                return new { ok = false, error = "Need at least five JPORTAL songs for rank carousel test" };
            }

            Type scoreManagerType = Type.GetType("MajdataPlay.ScoreManager, Assembly-CSharp", true);
            var getSongScores = scoreManagerType.GetMethod("GetSongScores", StaticFlags);
            for (int i = 0; i < songs.Length; i++) {
                object scores = getSongScores.Invoke(null, new object[] { songs[i] });
                object easy = scores.GetType().GetProperty("Easy").GetValue(scores, null);
                object basic = scores.GetType().GetProperty("Basic").GetValue(scores, null);
                double easyDx = 1000.0 - i;
                double basicDx = 1000.0 + i;
                ((MajdataPlay.MaiScore)easy).Acc = new MajdataPlay.Accurate { DX = easyDx, Classic = easyDx };
                ((MajdataPlay.MaiScore)easy).PlayCount = 1;
                ((MajdataPlay.MaiScore)basic).Acc = new MajdataPlay.Accurate { DX = basicDx, Classic = basicDx };
                ((MajdataPlay.MaiScore)basic).PlayCount = 1;
            }

            MajdataPlay.SongStorage.OrderBy.Keyword = "";
            MajdataPlay.SongStorage.OrderBy.SortBy = MajdataPlay.SortType.ByRank;
            Type sceneSwitcherType = Type.GetType("MajdataPlay.SceneSwitcher, Assembly-CSharp", true);
            object switcher = UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
                .OfType<UnityEngine.Component>()
                .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
            if (switcher != null) {
                sceneSwitcherType.GetMethod("SwitchScene", Flags).Invoke(switcher, new object[] { "List", true });
            }
            return new { ok = true, action = "$Action", firstHash = songs[0].Hash, expectedBasicIndex = 4 };
        }

        if ("$Action" == "OpenDefault") {
            MajdataPlay.SongStorage.OrderBy.Keyword = "";
            MajdataPlay.SongStorage.OrderBy.SortBy = MajdataPlay.SortType.Default;
        }

        if ("$Action" == "OpenDefault" || "$Action" == "OpenRank") {
            int songIndex = 0;
            int difficultyIndex = 0;

            MajdataPlay.SongStorage.CollectionIndex = targetIndex;
            collections[targetIndex].Index = songIndex;
            coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
            coverListType.GetMethod("SlideListInternal", Flags).Invoke(coverList, new object[] { targetIndex });
            coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
            coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { difficultyIndex });
            coverListType.GetMethod("SlideListInternal", Flags).Invoke(coverList, new object[] { songIndex });
            fixedTicks();
            return new {
                ok = true,
                action = "$Action",
                songIndex = songIndex,
                difficultyIndex = difficultyIndex,
                song = collections[targetIndex][songIndex].Title
            };
        }

        if ("$Action" == "Scroll") {
            coverListType.GetMethod("SlideList", Flags).Invoke(coverList, new object[] { 1 });
            fixedTicks();
            return new { ok = true, action = "$Action" };
        }

        if ("$Action" == "Difficulty") {
            coverListType.GetMethod("SlideDifficulty", Flags).Invoke(coverList, new object[] { 1 });
            fixedTicks();
            return new { ok = true, action = "$Action" };
        }

        if ("$Action" == "DifficultyToBasic") {
            coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { 1 });
            fixedTicks();
            return new { ok = true, action = "$Action" };
        }

        return new { ok = false, error = "Unknown action $Action" };
    } catch (Exception ex) {
        return new { ok = false, action = "$Action", error = ex.ToString() };
    }
})()
"@
}

function Convert-SerializedGameStateValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [string] -or $Value.GetType().IsPrimitive -or $Value -is [decimal]) {
        return $Value
    }

    if ($Value -is [System.Array]) {
        return @($Value | ForEach-Object { Convert-SerializedGameStateValue $_ })
    }

    $propertiesProperty = $Value.PSObject.Properties["properties"]
    if ($null -ne $propertiesProperty) {
        return Convert-SerializedGameStateValue $propertiesProperty.Value
    }

    $itemsProperty = $Value.PSObject.Properties["items"]
    if ($null -ne $itemsProperty) {
        $items = $itemsProperty.Value
        if ($items -is [System.Array]) {
            return @($items | ForEach-Object { Convert-SerializedGameStateValue $_ })
        }

        if ($null -eq $items) {
            return @()
        }

        return @(Convert-SerializedGameStateValue $items)
    }

    if ($null -ne $Value.PSObject.Properties["`$ref"]) {
        return $null
    }

    $object = [ordered]@{}
    foreach ($property in $Value.PSObject.Properties) {
        if ($property.Name -eq "type" -or $property.Name -eq "`$id") {
            continue
        }

        $object[$property.Name] = Convert-SerializedGameStateValue $property.Value
    }

    return [pscustomobject]$object
}

function Read-VisibleGameState {
    $result = Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
        Type bigType = Type.GetType("MajdataPlay.Scenes.List.CoverBigDisplayer, Assembly-CSharp", true);
        Type smallCoverType = Type.GetType("MajdataPlay.Scenes.List.SongCoverSmallDisplayer, Assembly-CSharp", true);
        Type subInfoType = Type.GetType("MajdataPlay.Scenes.List.SubInfoDisplayer, Assembly-CSharp", false);
        Type chartAnalyzerType = Type.GetType("MajdataPlay.Scenes.Game.ChartAnalyzer, Assembly-CSharp", true);
        Type qolBridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", false);
        bool qolLoaded = qolBridgeType != null && qolBridgeType.GetProperty("Active").GetValue(null, null) != null;

        object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (coverList == null) {
            return new { ok = false, error = "CoverListDisplayer not found" };
        }

        var selectedSong = coverListType.GetProperty("SelectedSong", Flags).GetValue(coverList, null) as MajdataPlay.ISongDetail;
        var selectedCollection = coverListType.GetProperty("SelectedCollection", Flags).GetValue(coverList, null) as MajdataPlay.Collections.SongCollection;
        int desired = Convert.ToInt32(coverListType.GetField("desiredListPos", Flags).GetValue(coverList));
        float listPosReal = Convert.ToSingle(coverListType.GetField("listPosReal", Flags).GetValue(coverList));
        int selectedDifficulty = Convert.ToInt32(coverListType.GetField("selectedDifficulty", Flags).GetValue(coverList));
        string mode = coverListType.GetProperty("Mode", Flags).GetValue(coverList, null).ToString();
        bool isChartList = mode == "Chart";

        UnityEngine.Component big = UnityEngine.Resources.FindObjectsOfTypeAll(bigType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        TMPro.TMP_Text title = big == null ? null : bigType.GetField("_title", Flags).GetValue(big) as TMPro.TMP_Text;
        TMPro.TMP_Text artist = big == null ? null : bigType.GetField("_artist", Flags).GetValue(big) as TMPro.TMP_Text;
        TMPro.TMP_Text charter = big == null ? null : bigType.GetField("_charter", Flags).GetValue(big) as TMPro.TMP_Text;
        TMPro.TMP_Text level = big == null ? null : bigType.GetField("_level", Flags).GetValue(big) as TMPro.TMP_Text;
        TMPro.TMP_Text achievementRate = big == null ? null : bigType.GetField("_archieveRate", Flags).GetValue(big) as TMPro.TMP_Text;
        TMPro.TMP_Text rank = big == null ? null : bigType.GetField("_rank", Flags).GetValue(big) as TMPro.TMP_Text;
        TMPro.TMP_Text clearMark = big == null ? null : bigType.GetField("_clearMark", Flags).GetValue(big) as TMPro.TMP_Text;
        UnityEngine.UI.Image centerCover = big == null ? null : bigType.GetField("_cover", Flags).GetValue(big) as UnityEngine.UI.Image;
        TMPro.TMP_Text metadataLine = big == null ? null : big.GetComponentsInChildren<TMPro.TMP_Text>(true)
            .FirstOrDefault(t => t != null && t.gameObject != null && t.gameObject.name == "QoLSelectedSongMetadataLine");

        UnityEngine.Component analyzer = UnityEngine.Resources.FindObjectsOfTypeAll(chartAnalyzerType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        UnityEngine.UI.Text analyzerText = analyzer == null ? null : chartAnalyzerType.GetField("anaText", Flags).GetValue(analyzer) as UnityEngine.UI.Text;
        UnityEngine.Component subInfo = subInfoType == null ? null : UnityEngine.Resources.FindObjectsOfTypeAll(subInfoType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        Func<string, string> subInfoText = fieldName => {
            if (subInfo == null || subInfoType == null) return "";
            var field = subInfoType.GetField(fieldName, Flags);
            TMPro.TMP_Text text = field == null ? null : field.GetValue(subInfo) as TMPro.TMP_Text;
            return text == null ? "" : text.text;
        };

        Func<UnityEngine.Sprite, object> spriteState = sprite => sprite == null ? null : new {
            instanceId = sprite.GetInstanceID(),
            name = sprite.name ?? "",
            textureInstanceId = sprite.texture == null ? 0 : sprite.texture.GetInstanceID(),
            textureName = sprite.texture == null ? "" : (sprite.texture.name ?? "")
        };

        Func<MajdataPlay.ISongDetail, object> songState = song => {
            if (song == null) return null;

            string[] levels;
            string[] designers;
            try {
                levels = song.Levels == null ? Array.Empty<string>() : song.Levels.ToArray();
            } catch {
                levels = Array.Empty<string>();
            }
            try {
                designers = song.Designers == null ? Array.Empty<string>() : song.Designers.ToArray();
            } catch {
                designers = Array.Empty<string>();
            }

            return new {
                title = song.Title ?? "",
                hash = song.Hash ?? "",
                artist = song.Artist ?? "",
                isOnline = song.IsOnline,
                levels = levels,
                designers = designers,
                selectedLevel = selectedDifficulty >= 0 && selectedDifficulty < levels.Length ? levels[selectedDifficulty] ?? "" : "",
                selectedDesigner = selectedDifficulty >= 0 && selectedDifficulty < designers.Length ? designers[selectedDifficulty] ?? "" : ""
            };
        };

        Func<MajdataPlay.Collections.SongCollection, object> collectionState = collection => {
            if (collection == null) return null;

            MajdataPlay.ISongDetail current = null;
            try {
                current = collection.Count > 0 ? collection.Current : null;
            } catch {
                current = null;
            }

            return new {
                name = collection.Name ?? "",
                count = collection.Count,
                index = collection.Index,
                type = collection.Type.ToString(),
                isOnline = collection.IsOnline,
                currentSong = songState(current)
            };
        };

        Func<object, Array> memoryToArray = memory => {
            if (memory == null) return Array.Empty<object>();
            var toArray = memory.GetType().GetMethod("ToArray");
            return toArray == null ? Array.Empty<object>() : (Array)toArray.Invoke(memory, null);
        };

        Func<MajdataPlay.ISongDetail, UnityEngine.Sprite> cachedCover = song => {
            if (song == null) return null;
            var coverRefField = song.GetType().GetField("_coverRef", Flags);
            if (coverRefField == null) return null;
            object weak = coverRefField.GetValue(song);
            if (weak == null) return null;
            var tryGetTarget = weak.GetType().GetMethod("TryGetTarget");
            if (tryGetTarget == null) return null;
            object[] args = new object[] { null };
            bool hasTarget = (bool)tryGetTarget.Invoke(weak, args);
            return hasTarget ? args[0] as UnityEngine.Sprite : null;
        };

        object memory = coverListType.GetField("_songDetailBindings", Flags).GetValue(coverList);
        Array bindings = memoryToArray(memory);
        MajdataPlay.ISongDetail focusedBinding = null;
        MajdataPlay.ISongDetail focusedDisplay = null;
        int activeSmallCovers = 0;
        int staleVisibleSprites = 0;
        int duplicateVisibleSpriteDifferentSongs = 0;
        int duplicateVisibleBoundHashes = 0;
        var spriteOwners = new Dictionary<int, string>();
        var boundHashes = new HashSet<string>(StringComparer.Ordinal);
        var assignedDisplayerIds = new HashSet<int>();
        var visibleSongRows = new List<string>();
        var visibleSongElements = new List<object>();

        for (int i = 0; i < bindings.Length; i++) {
            object binding = bindings.GetValue(i);
            if (binding == null) continue;

            var bindingSong = binding.GetType().GetProperty("SongDetail").GetValue(binding, null) as MajdataPlay.ISongDetail;
            object displayer = binding.GetType().GetProperty("Displayer").GetValue(binding, null);
            if (i == desired) {
                focusedBinding = bindingSong;
            }
            if (displayer == null) continue;

            var component = displayer as UnityEngine.Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) continue;
            assignedDisplayerIds.Add(component.GetInstanceID());

            var boundSongField = displayer.GetType().GetField("_boundSong", Flags);
            var boundSong = boundSongField == null ? null : boundSongField.GetValue(displayer) as MajdataPlay.ISongDetail;
            if (i == desired) {
                focusedDisplay = boundSong;
            }

            var imageField = displayer.GetType().GetField("_songCover", Flags);
            var image = imageField == null ? null : imageField.GetValue(displayer) as UnityEngine.UI.Image;
            var sprite = image == null ? null : image.sprite;
            var cached = cachedCover(boundSong);
            bool stale = sprite != null && cached != null && !object.ReferenceEquals(sprite, cached);
            if (stale) staleVisibleSprites++;

            string boundHash = boundSong == null ? "" : (boundSong.Hash ?? "");
            if (!string.IsNullOrWhiteSpace(boundHash) && !boundHashes.Add(boundHash)) {
                duplicateVisibleBoundHashes++;
            }

            int spriteId = sprite == null ? 0 : sprite.GetInstanceID();
            if (spriteId != 0) {
                string owner;
                if (spriteOwners.TryGetValue(spriteId, out owner) && !string.Equals(owner, boundHash, StringComparison.Ordinal)) {
                    duplicateVisibleSpriteDifferentSongs++;
                } else if (!spriteOwners.ContainsKey(spriteId)) {
                    spriteOwners.Add(spriteId, boundHash);
                }
            }

            activeSmallCovers++;
            float distance = i - listPosReal;
            visibleSongRows.Add(i.ToString() + ":" + (bindingSong == null ? "" : bindingSong.Hash) + "/" + boundHash + "/sprite:" + spriteId.ToString() + "/stale:" + stale.ToString());
            visibleSongElements.Add(new {
                kind = "song",
                index = i,
                isSelected = i == desired,
                distance = distance,
                displayerInstanceId = component.GetInstanceID(),
                binding = new { song = songState(bindingSong) },
                display = new {
                    song = songState(boundSong),
                    image = new {
                        sprite = spriteState(sprite),
                        cachedSprite = spriteState(cached),
                        isStale = stale
                    }
                }
            });
        }

        object collectionMemory = coverListType.GetField("_songCollectionBindings", Flags).GetValue(coverList);
        Array collectionBindings = memoryToArray(collectionMemory);
        MajdataPlay.Collections.SongCollection focusedCollectionBinding = null;
        MajdataPlay.Collections.SongCollection focusedCollectionDisplay = null;
        var visibleFolderRows = new List<string>();
        var visibleFolderElements = new List<object>();
        var allFolders = new List<object>();
        for (int i = 0; i < collectionBindings.Length; i++) {
            object binding = collectionBindings.GetValue(i);
            if (binding == null) continue;

            var collection = binding.GetType().GetProperty("Collection").GetValue(binding, null) as MajdataPlay.Collections.SongCollection;
            allFolders.Add(collectionState(collection));
            object displayer = binding.GetType().GetProperty("Displayer").GetValue(binding, null);
            if (i == desired) {
                focusedCollectionBinding = collection;
            }
            if (displayer == null) continue;

            var component = displayer as UnityEngine.Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) continue;

            var boundCollectionField = displayer.GetType().GetField("_boundCollection", Flags);
            var boundCollection = boundCollectionField == null ? null : boundCollectionField.GetValue(displayer) as MajdataPlay.Collections.SongCollection;
            if (i == desired) {
                focusedCollectionDisplay = boundCollection;
            }

            var folderTextField = displayer.GetType().GetField("_folderText", Flags);
            TMPro.TextMeshProUGUI folderText = folderTextField == null ? null : folderTextField.GetValue(displayer) as TMPro.TextMeshProUGUI;
            float distance = i - listPosReal;
            visibleFolderRows.Add(i.ToString() + ":" + (collection == null ? "" : collection.Name) + "/" + (boundCollection == null ? "" : boundCollection.Name));
            visibleFolderElements.Add(new {
                kind = "folder",
                index = i,
                isSelected = i == desired,
                distance = distance,
                displayerInstanceId = component.GetInstanceID(),
                binding = new { folder = collectionState(collection) },
                display = new {
                    folder = collectionState(boundCollection),
                    text = folderText == null ? "" : folderText.text
                }
            });
        }

        int activeSmallCoverDisplayers = 0;
        int orphanedActiveSmallCovers = 0;
        int duplicateAllActiveBoundHashes = 0;
        int duplicateAllActiveSpriteDifferentSongs = 0;
        int staleAllActiveSprites = 0;
        var allActiveBoundHashes = new HashSet<string>(StringComparer.Ordinal);
        var allActiveSpriteOwners = new Dictionary<int, string>();
        var allRows = new List<string>();
        foreach (UnityEngine.Component component in UnityEngine.Resources.FindObjectsOfTypeAll(smallCoverType).OfType<UnityEngine.Component>()) {
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) {
                continue;
            }

            int componentId = component.GetInstanceID();
            if (!assignedDisplayerIds.Contains(componentId)) {
                orphanedActiveSmallCovers++;
            }

            var boundSongField = component.GetType().GetField("_boundSong", Flags);
            var boundSong = boundSongField == null ? null : boundSongField.GetValue(component) as MajdataPlay.ISongDetail;
            string boundHash = boundSong == null ? "" : (boundSong.Hash ?? "");
            if (!string.IsNullOrWhiteSpace(boundHash) && !allActiveBoundHashes.Add(boundHash)) {
                duplicateAllActiveBoundHashes++;
            }

            var imageField = component.GetType().GetField("_songCover", Flags);
            var image = imageField == null ? null : imageField.GetValue(component) as UnityEngine.UI.Image;
            var sprite = image == null ? null : image.sprite;
            var cached = cachedCover(boundSong);
            bool stale = sprite != null && cached != null && !object.ReferenceEquals(sprite, cached);
            if (stale) staleAllActiveSprites++;

            int spriteId = sprite == null ? 0 : sprite.GetInstanceID();
            if (spriteId != 0) {
                string owner;
                if (allActiveSpriteOwners.TryGetValue(spriteId, out owner) && !string.Equals(owner, boundHash, StringComparison.Ordinal)) {
                    duplicateAllActiveSpriteDifferentSongs++;
                } else if (!allActiveSpriteOwners.ContainsKey(spriteId)) {
                    allActiveSpriteOwners.Add(spriteId, boundHash);
                }
            }

            activeSmallCoverDisplayers++;
            allRows.Add("obj:" + componentId.ToString() + "/bound:" + boundHash + "/sprite:" + spriteId.ToString() + "/orphan:" + (!assignedDisplayerIds.Contains(componentId)).ToString() + "/stale:" + stale.ToString());
        }

        object selectedCarouselElement;
        if (isChartList) {
            selectedCarouselElement = new {
                kind = "song",
                index = desired,
                binding = new { song = songState(focusedBinding) },
                display = new { song = songState(focusedDisplay) }
            };
        } else {
            selectedCarouselElement = new {
                kind = "folder",
                index = desired,
                binding = new { folder = collectionState(focusedCollectionBinding) },
                display = new { folder = collectionState(focusedCollectionDisplay) }
            };
        }
        UnityEngine.Sprite selectedCachedCover = cachedCover(selectedSong);
        UnityEngine.Sprite centerSprite = centerCover == null ? null : centerCover.sprite;
        bool centerCoverMatchesSelectedSong = centerSprite != null && selectedCachedCover != null && object.ReferenceEquals(centerSprite, selectedCachedCover);
        var carouselDiagnostics = new {
            activeSmallCovers = activeSmallCovers,
            staleVisibleSprites = staleVisibleSprites,
            duplicateVisibleSpriteDifferentSongs = duplicateVisibleSpriteDifferentSongs,
            duplicateVisibleBoundHashes = duplicateVisibleBoundHashes,
            activeSmallCoverDisplayers = activeSmallCoverDisplayers,
            orphanedActiveSmallCovers = orphanedActiveSmallCovers,
            duplicateAllActiveBoundHashes = duplicateAllActiveBoundHashes,
            duplicateAllActiveSpriteDifferentSongs = duplicateAllActiveSpriteDifferentSongs,
            staleAllActiveSprites = staleAllActiveSprites
        };

        return new {
            ok = true,
            schema = "majdata.visibleGameState.v1",
            capturedFrame = UnityEngine.Time.frameCount,
            screen = new {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                logicalScreen = "songSelect",
                selecting = isChartList ? "songs" : "folders",
                mode = mode,
                width = UnityEngine.Screen.width,
                height = UnityEngine.Screen.height
            },
            upperScreen = new {
                qolLoaded = qolLoaded,
                selectedDifficulty = selectedDifficulty,
                selectedDifficultyName = ((MajdataPlay.ChartLevel)selectedDifficulty).ToString(),
                sortMode = MajdataPlay.SongStorage.OrderBy.SortBy.ToString(),
                sortKeyword = MajdataPlay.SongStorage.OrderBy.Keyword ?? "",
                collectionIndex = MajdataPlay.SongStorage.CollectionIndex
            },
            songSelect = new {
                selecting = isChartList ? "songs" : "folders",
                selected = new {
                    kind = isChartList ? "song" : "folder",
                    index = desired,
                    collection = collectionState(selectedCollection),
                    song = songState(selectedSong),
                    folder = isChartList ? null : collectionState(selectedCollection)
                },
                folders = allFolders.ToArray(),
                visibleFolders = visibleFolderElements.ToArray(),
                songs = visibleSongElements.Select(e => e).ToArray(),
                carousel = new {
                    mode = mode,
                    selectedIndex = desired,
                    realPosition = listPosReal,
                    selectedElement = selectedCarouselElement,
                    visibleElements = isChartList ? visibleSongElements.ToArray() : visibleFolderElements.ToArray(),
                    visibleSongElements = visibleSongElements.ToArray(),
                    visibleFolderElements = visibleFolderElements.ToArray(),
                    diagnostics = carouselDiagnostics,
                    debug = new {
                        visibleSongRows = string.Join(";", visibleSongRows.ToArray()),
                        visibleFolderRows = string.Join(";", visibleFolderRows.ToArray()),
                        allActiveSongCoverRows = string.Join(";", allRows.ToArray())
                    }
                },
                center = new {
                    title = title == null ? "" : title.text,
                    image = new {
                        sprite = spriteState(centerSprite),
                        selectedSongCachedSprite = spriteState(selectedCachedCover),
                        matchesSelectedSongCachedCover = centerCoverMatchesSelectedSong
                    }
                },
                rightInfo = new {
                    title = title == null ? "" : title.text,
                    artist = artist == null ? "" : artist.text,
                    charter = charter == null ? "" : charter.text,
                    level = level == null ? "" : level.text,
                    achievementRate = achievementRate == null || !achievementRate.enabled ? "" : achievementRate.text,
                    rank = rank == null ? "" : rank.text,
                    clearMark = clearMark == null ? "" : clearMark.text,
                    metadataLine = new {
                        found = metadataLine != null && metadataLine.gameObject.activeInHierarchy,
                        text = metadataLine == null ? "" : metadataLine.text
                    },
                    subInfo = new {
                        found = subInfo != null,
                        id = subInfoText("id_text"),
                        likeCount = subInfoText("LikeCount"),
                        playCount = subInfoText("PlayCount"),
                        commentCount = subInfoText("CommentCount"),
                        commentText = subInfoText("CommentText")
                    },
                    analyzerText = analyzerText == null ? "" : analyzerText.text
                }
            },
            diagnostics = new {
                carousel = carouselDiagnostics
            }
        };
    } catch (Exception ex) {
        return new { ok = false, error = ex.ToString() };
    }
})()
"@

    return Convert-SerializedGameStateValue $result.result.properties
}

function Add-Case {
    param(
        [System.Collections.Generic.List[object]]$Cases,
        [string]$Name,
        [bool]$Consistent,
        [string]$Details,
        [string]$ExpectedBehavior = "",
        [object]$Screenshot = $null
    )

    $expected = if ([string]::IsNullOrWhiteSpace($ExpectedBehavior)) { $Name } else { $ExpectedBehavior }
    if (-not $expected.TrimEnd().EndsWith(".")) {
        $expected = $expected + "."
    }

    $screenshotName = ""
    $screenshotPath = ""
    $screenshotDescription = ""
    $screenshotCheckDetails = "No screenshot was attached to this test case."
    $screenshotMatchesExpected = $false
    if ($null -ne $Screenshot) {
        $screenshotName = [string]$Screenshot["name"]
        $screenshotPath = [string]$Screenshot["path"]
        $screenshotDescription = [string]$Screenshot["description"]
        $capture = $Screenshot["capture"]
        $state = $Screenshot["state"]
        $expectation = $Screenshot["expectation"]
        $fileExists = -not [string]::IsNullOrWhiteSpace($screenshotPath) -and (Test-Path $screenshotPath)
        $hasBytes = $fileExists -and ((Get-Item $screenshotPath).Length -gt 0)
        $captureScene = [string]$capture.scene
        $stateOk = $null -eq $state -or [bool]$state.ok
        $screenshotMatchesExpected = $hasBytes -and $captureScene -eq "List" -and $stateOk
        $screenshotCheckDetails = "fileExists=$fileExists; hasBytes=$hasBytes; scene=$captureScene; stateOk=$stateOk"
        if ($expectation -and $null -ne $expectation.consistent) {
            $screenshotMatchesExpected = $screenshotMatchesExpected -and [bool]$expectation.consistent
            $screenshotCheckDetails = "$screenshotCheckDetails; expectation=$($expectation.details)"
        }
    }

    $Cases.Add([pscustomobject]@{
        Name = $Name
        ExpectedBehavior = $expected
        Consistent = $Consistent
        Details = $Details
        ScreenshotName = $screenshotName
        ScreenshotPath = $screenshotPath
        ScreenshotDescription = $screenshotDescription
        ScreenshotMatchesExpected = $screenshotMatchesExpected
        ScreenshotCheckDetails = $screenshotCheckDetails
    }) | Out-Null
}

function Set-ScreenshotExpectation {
    param(
        [object]$Screenshot,
        [bool]$Consistent,
        [string]$Details
    )

    $Screenshot["expectation"] = [ordered]@{
        consistent = $Consistent
        details = $Details
    }
    return $Screenshot
}

function Capture-QolFeatureScenarioScreenshot {
    param(
        [System.Collections.Generic.List[object]]$Screenshots,
        [string]$Label,
        [string]$ScreenshotName,
        [string]$ExpectedBehavior,
        [string]$Feature,
        [string]$Grouping,
        [string]$Sorting,
        [string]$DifficultyFilter,
        [string]$DownloadedFilter,
        [string]$TargetCollection,
        [int]$SelectedDifficulty = 0,
        [bool]$InstallWebsiteCollection = $false
    )

    $action = $null
    $state = $null
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        Ensure-ListScene
        $action = Invoke-QolFeatureProbe -Feature $Feature -Grouping $Grouping -Sorting $Sorting -DifficultyFilter $DifficultyFilter -DownloadedFilter $DownloadedFilter -TargetCollection $TargetCollection -SelectedDifficulty $SelectedDifficulty -InstallWebsiteCollection:$InstallWebsiteCollection
        Start-Sleep -Seconds 5
        $state = Read-VisibleGameState
        if ($state.ok -and $state.screen.scene -eq "List" -and $state.screen.selecting -eq "songs") {
            break
        }

        if ($attempt -lt 2) {
            Ensure-ListScene
        }
    }

    $shot = Capture-JportalScreenshot $Label $ScreenshotName $ExpectedBehavior $state $ExpectedBehavior

    $actionOk = [bool]$action.result.properties.ok
    $selectedSong = $state.songSelect.selected.song
    $selectedElement = $state.songSelect.carousel.selectedElement
    $bindingSong = $selectedElement.binding.song
    $displaySong = $selectedElement.display.song
    $collectionName = [string]$action.result.properties.collection
    $stateCollectionName = [string]$state.songSelect.selected.collection.name
    $coherentSongState = $state.ok -and
        $state.screen.selecting -eq "songs" -and
        $state.songSelect.center.title -eq $selectedSong.title -and
        $state.songSelect.rightInfo.title -eq $selectedSong.title -and
        $selectedSong.hash -eq $bindingSong.hash -and
        $selectedSong.hash -eq $displaySong.hash
    $matchesScenario = $actionOk -and $coherentSongState -and (
        [string]::IsNullOrWhiteSpace($collectionName) -or
        $stateCollectionName -eq $collectionName
    )

    Set-ScreenshotExpectation $shot $matchesScenario "actionOk=$actionOk; collection=$collectionName; stateCollection=$stateCollectionName; selected=$($selectedSong.title)/$($selectedSong.hash); center=$($state.songSelect.center.title); right=$($state.songSelect.rightInfo.title); binding=$($bindingSong.title)/$($bindingSong.hash); display=$($displaySong.title)/$($displaySong.hash)" | Out-Null
    $Screenshots.Add($shot) | Out-Null
    return $shot
}

function Get-QolFeatureSpecScreenshotKey {
    param([string]$Name)

    switch -Regex ($Name) {
        "^feature specs have enough base data" { return "spec-base-data" }
        "^sorting title" { return "spec-sorting-title" }
        "^sorting artist" { return "spec-sorting-artist" }
        "^sorting note designer" { return "spec-sorting-note-designer" }
        "^sorting date added" { return "spec-sorting-date-added" }
        "^sorting difficulty" { return "spec-sorting-difficulty" }
        "^sorting rank" { return "spec-sorting-rank" }
        "^sorting play count" { return "spec-sorting-play-count" }
        "^sorting AP/FC rank" { return "spec-sorting-ap-fc-rank" }
        "^sorting DX score" { return "spec-sorting-dx-score" }
        "^grouping difficulty bracket" { return "spec-grouping-difficulty-bracket" }
        "^grouping difficulty level" { return "spec-grouping-difficulty-level" }
        "^grouping title" { return "spec-grouping-title" }
        "^grouping artist" { return "spec-grouping-artist" }
        "^grouping rank" { return "spec-grouping-rank" }
        "^difficulty filter MoreThan1" { return "spec-difficulty-filter-morethan1" }
        "^difficulty filter MoreThan2" { return "spec-difficulty-filter-morethan2" }
        "^difficulty filter MoreThan3" { return "spec-difficulty-filter-morethan3" }
        "^difficulty filter counts" { return "spec-difficulty-filter-morethan1" }
        "^downloaded filter downloaded-only" { return "spec-downloaded-only" }
        "^downloaded filter online-only" { return "spec-online-only" }
        "^downloaded filter mixed" { return "spec-mixed" }
        "^random recommended" { return "spec-random-recommended" }
        "^website collection downloaded-only" { return "spec-website-downloaded" }
        "^website collection online-only" { return "spec-website-online" }
        "^website collection" { return "spec-website-mixed" }
        default { return "spec-base-data" }
    }
}

function Capture-QolFeatureSpecScreenshots {
    param(
        [object[]]$SpecCases,
        [System.Collections.Generic.List[object]]$Screenshots,
        [string]$Label
    )

    $needed = @{}
    foreach ($case in @($SpecCases)) {
        $needed[(Get-QolFeatureSpecScreenshotKey $case.name)] = $true
    }

    $plans = @(
        [pscustomobject]@{ Key = "spec-base-data"; Screenshot = "13-spec-base-data"; Expected = "The base song data used by the feature specs should be visible and coherent in the All collection."; Feature = "spec base data"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "All"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-title"; Screenshot = "14-spec-sorting-title"; Expected = "Title sorting should display JPORTAL songs in title order while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting title"; Grouping = "Default"; Sorting = "Title"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-artist"; Screenshot = "15-spec-sorting-artist"; Expected = "Artist sorting should display JPORTAL songs in artist order while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting artist"; Grouping = "Default"; Sorting = "Artist"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-note-designer"; Screenshot = "16-spec-sorting-note-designer"; Expected = "Note designer sorting should display JPORTAL songs in designer order while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting note designer"; Grouping = "Default"; Sorting = "NoteDesigner"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-date-added"; Screenshot = "17-spec-sorting-date-added"; Expected = "Date-added sorting should display the newest JPORTAL songs first while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting date added"; Grouping = "Default"; Sorting = "DateAdded"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-difficulty"; Screenshot = "18-spec-sorting-difficulty"; Expected = "Difficulty sorting should use the selected Master difficulty while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting difficulty"; Grouping = "Default"; Sorting = "Difficulty"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 4; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-rank"; Screenshot = "19-spec-sorting-rank"; Expected = "Rank sorting should use diagnostic score facets while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting rank"; Grouping = "Default"; Sorting = "Rank"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-play-count"; Screenshot = "20-spec-sorting-play-count"; Expected = "Play-count sorting should use diagnostic play-count facets while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting play count"; Grouping = "Default"; Sorting = "PlayCount"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-ap-fc-rank"; Screenshot = "21-spec-sorting-ap-fc-rank"; Expected = "AP/FC rank sorting should place AP rows before FC rows before clear-only rows while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting AP FC rank"; Grouping = "Default"; Sorting = "ApFcRank"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-sorting-dx-score"; Screenshot = "22-spec-sorting-dx-score"; Expected = "DX-score sorting should use diagnostic DX score facets while the selected song, center cover, and right info stay synchronized."; Feature = "spec sorting DX score"; Grouping = "Default"; Sorting = "DxScore"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-grouping-difficulty-bracket"; Screenshot = "23-spec-grouping-difficulty-bracket"; Expected = "Difficulty-bracket grouping should show difficulty folders and keep the opened folder's selected song, center cover, and right info synchronized."; Feature = "spec grouping difficulty bracket"; Grouping = "DifficultyBracket"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "Master"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-grouping-difficulty-level"; Screenshot = "24-spec-grouping-difficulty-level"; Expected = "Difficulty-level grouping should show selected-difficulty level buckets and keep the opened folder's selected song, center cover, and right info synchronized."; Feature = "spec grouping difficulty level"; Grouping = "DifficultyLevel"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "13+"; SelectedDifficulty = 4; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-grouping-title"; Screenshot = "25-spec-grouping-title"; Expected = "Title grouping should show title buckets and keep the opened folder's selected song, center cover, and right info synchronized."; Feature = "spec grouping title"; Grouping = "Title"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "A"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-grouping-artist"; Screenshot = "26-spec-grouping-artist"; Expected = "Artist grouping should show one folder per case-insensitive artist key and keep the opened folder's selected song, center cover, and right info synchronized."; Feature = "spec grouping artist"; Grouping = "Artist"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "Unknown Artist"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-grouping-rank"; Screenshot = "27-spec-grouping-rank"; Expected = "Rank grouping should show score-rank folders including the diagnostic SSS+ row and keep the selected song, center cover, and right info synchronized."; Feature = "spec grouping rank"; Grouping = "Rank"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "SSS+"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-difficulty-filter-morethan1"; Screenshot = "28-spec-difficulty-filter-morethan1"; Expected = "The MoreThan1 difficulty filter should show only songs with more than one usable difficulty and keep the selected song, center cover, and right info synchronized."; Feature = "spec difficulty filter more than one"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "MoreThan1"; DownloadedFilter = "Mixed"; Target = "All"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-difficulty-filter-morethan2"; Screenshot = "29-spec-difficulty-filter-morethan2"; Expected = "The MoreThan2 difficulty filter should show only songs with more than two usable difficulties and keep the selected song, center cover, and right info synchronized."; Feature = "spec difficulty filter more than two"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "MoreThan2"; DownloadedFilter = "Mixed"; Target = "All"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-difficulty-filter-morethan3"; Screenshot = "30-spec-difficulty-filter-morethan3"; Expected = "The MoreThan3 difficulty filter should show only songs with more than three usable difficulties and keep the selected song, center cover, and right info synchronized."; Feature = "spec difficulty filter more than three"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "MoreThan3"; DownloadedFilter = "Mixed"; Target = "All"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-downloaded-only"; Screenshot = "31-spec-downloaded-only"; Expected = "Downloaded-only filtering should hide online rows and keep the selected song, center cover, and right info synchronized."; Feature = "spec downloaded only"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "DownloadedOnly"; Target = "JPORTAL"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-online-only"; Screenshot = "32-spec-online-only"; Expected = "Online-only filtering should show only online rows and keep the selected song, center cover, and right info synchronized."; Feature = "spec online only"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "OnlineOnly"; Target = "All"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-mixed"; Screenshot = "33-spec-mixed"; Expected = "Mixed filtering should show both downloaded and online rows and keep the selected song, center cover, and right info synchronized."; Feature = "spec mixed filter"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "All"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-random-recommended"; Screenshot = "34-spec-random-recommended"; Expected = "Random Recommended should appear after favorites and open to unique available songs with coherent selected-song visuals."; Feature = "spec random recommended"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "Random Recommended"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-website-mixed"; Screenshot = "35-spec-website-mixed"; Expected = "The diagnostic website collection should open resolved requested rows in order with coherent selected-song visuals."; Feature = "spec website mixed"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "QoL Extensive Website"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-website-downloaded"; Screenshot = "36-spec-website-downloaded"; Expected = "The diagnostic website collection in downloaded-only scope should show its downloaded resolved rows with coherent selected-song visuals."; Feature = "spec website downloaded"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "DownloadedOnly"; Target = "QoL Extensive Website"; SelectedDifficulty = 0; InstallWebsite = $false },
        [pscustomobject]@{ Key = "spec-website-online"; Screenshot = "37-spec-website-online"; Expected = "The online-only website-collection scenario should remain visually coherent while the state assertion verifies local-only website rows are dropped."; Feature = "spec website online"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "OnlineOnly"; Target = "All"; SelectedDifficulty = 0; InstallWebsite = $false }
    )

    $map = @{}
    foreach ($plan in $plans) {
        if (-not $needed.ContainsKey($plan.Key)) {
            continue
        }

        $map[$plan.Key] = Capture-QolFeatureScenarioScreenshot -Screenshots $Screenshots -Label $Label -ScreenshotName $plan.Screenshot -ExpectedBehavior $plan.Expected -Feature $plan.Feature -Grouping $plan.Grouping -Sorting $plan.Sorting -DifficultyFilter $plan.DifficultyFilter -DownloadedFilter $plan.DownloadedFilter -TargetCollection $plan.Target -SelectedDifficulty $plan.SelectedDifficulty -InstallWebsiteCollection:$plan.InstallWebsite
    }

    return $map
}

function Invoke-QolFeatureProbe {
    param(
        [string]$Feature,
        [string]$Grouping,
        [string]$Sorting,
        [string]$DifficultyFilter,
        [string]$DownloadedFilter,
        [string]$TargetCollection,
        [int]$SelectedDifficulty = 0,
        [bool]$InstallWebsiteCollection = $false
    )

    $featureLiteral = Convert-ToCSharpStringLiteral $Feature
    $groupingLiteral = Convert-ToCSharpStringLiteral $Grouping
    $sortingLiteral = Convert-ToCSharpStringLiteral $Sorting
    $difficultyFilterLiteral = Convert-ToCSharpStringLiteral $DifficultyFilter
    $downloadedFilterLiteral = Convert-ToCSharpStringLiteral $DownloadedFilter
    $targetCollectionLiteral = Convert-ToCSharpStringLiteral $TargetCollection
    $installWebsiteLiteral = if ($InstallWebsiteCollection) { "true" } else { "false" }
    $selectedDifficultyLiteral = [int]$SelectedDifficulty

    Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", false);
        if (bridgeType == null || bridgeType.GetProperty("Active").GetValue(null, null) == null) {
            return new { ok = false, skipped = true, feature = $featureLiteral, error = "QoL bridge is not loaded." };
        }

        Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
        object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (coverList == null) {
            return new { ok = false, skipped = false, feature = $featureLiteral, error = "CoverListDisplayer not found." };
        }

        Action fixedTicks = () => {
            var fixedUpdate = coverListType.GetMethod("FixedUpdate", Flags);
            if (fixedUpdate != null) {
                for (int i = 0; i < 60; i++) fixedUpdate.Invoke(coverList, new object[0]);
            }
        };

        coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { $selectedDifficultyLiteral });
        fixedTicks();

        string targetCollection = $targetCollectionLiteral;
        if ($installWebsiteLiteral) {
            var all = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "All");
            var rows = all == null
                ? new MajdataPlay.ISongDetail[0]
                : all.ToArray().Where(song => song != null && !song.IsOnline && !string.IsNullOrWhiteSpace(song.Hash)).Take(3).ToArray();
            if (rows.Length < 2) {
                return new { ok = false, skipped = false, feature = $featureLiteral, error = "Need at least two local rows for website collection probe." };
            }

            string hashes = string.Join("|", rows.Select(song => song.Hash).ToArray());
            bridgeType.GetMethod("InstallWebsiteCollectionForDiagnostics").Invoke(null, new object[] { targetCollection, hashes, rows.Length });
        }

        bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { $groupingLiteral });
        bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { $sortingLiteral });
        bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { $difficultyFilterLiteral });
        bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { $downloadedFilterLiteral });
        bool applied = (bool)bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
        if (!applied) {
            return new { ok = false, skipped = false, feature = $featureLiteral, error = "ApplySettingsForDiagnostics returned false." };
        }

        var collections = MajdataPlay.SongStorage.Collections;
        int targetIndex = Array.FindIndex(collections, c => c != null && c.Count > 0 && string.Equals(c.Name, targetCollection, StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0) {
            targetIndex = Array.FindIndex(collections, c => c != null && c.Count > 0 && c.Name != "MyFavorites");
        }
        if (targetIndex < 0) {
            return new { ok = false, skipped = false, feature = $featureLiteral, error = "No nonempty target collection was available." };
        }

        MajdataPlay.SongStorage.CollectionIndex = targetIndex;
        collections[targetIndex].Index = 0;
        coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
        var slide = coverListType.GetMethod("SlideListInternal", Flags);
        if (slide != null) {
            slide.Invoke(coverList, new object[] { targetIndex });
        }
        fixedTicks();
        coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
        coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { $selectedDifficultyLiteral });
        if (slide != null) {
            slide.Invoke(coverList, new object[] { 0 });
        }
        fixedTicks();

        var selected = coverListType.GetProperty("SelectedSong", Flags).GetValue(coverList, null) as MajdataPlay.ISongDetail;
        return new {
            ok = true,
            skipped = false,
            feature = $featureLiteral,
            collection = collections[targetIndex].Name,
            collectionIndex = targetIndex,
            collectionCount = collections[targetIndex].Count,
            selectedTitle = selected == null ? "" : selected.Title,
            selectedHash = selected == null ? "" : selected.Hash,
            error = ""
        };
    } catch (Exception ex) {
        return new { ok = false, skipped = false, feature = $featureLiteral, error = ex.ToString() };
    }
})()
"@
}

function Add-QolFeatureProbeCases {
    param(
        [System.Collections.Generic.List[object]]$Cases,
        [System.Collections.Generic.List[object]]$Screenshots,
        [string]$Label,
        [object]$ReferenceState,
        [object]$ReferenceScreenshot
    )

    $probes = @(
        [pscustomobject]@{ Feature = "sorting title"; Grouping = "Default"; Sorting = "Title"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "JPORTAL"; Screenshot = "07-feature-sorting-title"; InstallWebsite = $false },
        [pscustomobject]@{ Feature = "grouping difficulty bracket"; Grouping = "DifficultyBracket"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "Master"; Screenshot = "08-feature-grouping-difficulty-bracket"; InstallWebsite = $false },
        [pscustomobject]@{ Feature = "difficulty filter more than one"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "MoreThan1"; DownloadedFilter = "Mixed"; Target = "All"; Screenshot = "09-feature-difficulty-filter-more-than-one"; InstallWebsite = $false },
        [pscustomobject]@{ Feature = "downloaded songs filter"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "DownloadedOnly"; Target = "JPORTAL"; Screenshot = "10-feature-downloaded-filter"; InstallWebsite = $false },
        [pscustomobject]@{ Feature = "random recommended folder"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "Random Recommended"; Screenshot = "11-feature-random-recommended"; InstallWebsite = $false },
        [pscustomobject]@{ Feature = "website collection"; Grouping = "Default"; Sorting = "Default"; DifficultyFilter = "No"; DownloadedFilter = "Mixed"; Target = "QoL Feature Website"; Screenshot = "12-feature-website-collection"; InstallWebsite = $true }
    )

    if (-not $ReferenceState.upperScreen.qolLoaded) {
        foreach ($probe in $probes) {
            Add-Case $Cases "feature probe $($probe.Feature) keeps visible state coherent" $true "skipped: QoL bridge is not loaded in $Label." -Screenshot $ReferenceScreenshot
        }
        return
    }

    foreach ($probe in $probes) {
        $action = Invoke-QolFeatureProbe -Feature $probe.Feature -Grouping $probe.Grouping -Sorting $probe.Sorting -DifficultyFilter $probe.DifficultyFilter -DownloadedFilter $probe.DownloadedFilter -TargetCollection $probe.Target -InstallWebsiteCollection:$probe.InstallWebsite
        Start-Sleep -Seconds 5
        $state = Read-VisibleGameState
        $expectedBehavior = "The $($probe.Feature) feature should open $($probe.Target) with a coherent selected song, matching center cover, right song info, and carousel binding."
        $probeShot = Capture-JportalScreenshot $Label $probe.Screenshot "QoL feature probe: $($probe.Feature)." $state $expectedBehavior
        $screenshots.Add($probeShot) | Out-Null

        $actionOk = [bool]$action.result.properties.ok
        $selectedSong = $state.songSelect.selected.song
        $carousel = $state.songSelect.carousel
        $selectedElement = $carousel.selectedElement
        $bindingSong = $selectedElement.binding.song
        $displaySong = $selectedElement.display.song
        $diagnostics = $carousel.diagnostics
        $caseConsistent = (
            $actionOk -and
            $state.ok -and
            $state.screen.selecting -eq "songs" -and
            $state.songSelect.center.title -eq $selectedSong.title -and
            $state.songSelect.rightInfo.title -eq $selectedSong.title -and
            $selectedSong.hash -eq $bindingSong.hash -and
            $selectedSong.hash -eq $displaySong.hash -and
            $diagnostics.orphanedActiveSmallCovers -eq 0 -and
            $diagnostics.duplicateAllActiveBoundHashes -eq 0 -and
            $diagnostics.staleAllActiveSprites -eq 0 -and
            $diagnostics.duplicateAllActiveSpriteDifferentSongs -eq 0
        )
        Set-ScreenshotExpectation $probeShot $caseConsistent "feature=$($probe.Feature); actionOk=$actionOk; collection=$($action.result.properties.collection); selected=$($selectedSong.title)/$($selectedSong.hash); center=$($state.songSelect.center.title); right=$($state.songSelect.rightInfo.title); binding=$($bindingSong.title)/$($bindingSong.hash); display=$($displaySong.title)/$($displaySong.hash)" | Out-Null
        Add-Case $Cases "feature probe $($probe.Feature) keeps visible state coherent" $caseConsistent "feature=$($probe.Feature); actionOk=$actionOk; collection=$($action.result.properties.collection); selected=$($selectedSong.title)/$($selectedSong.hash); center=$($state.songSelect.center.title); right=$($state.songSelect.rightInfo.title); binding=$($bindingSong.title)/$($bindingSong.hash); display=$($displaySong.title)/$($displaySong.hash); orphaned=$($diagnostics.orphanedActiveSmallCovers); duplicateAllBound=$($diagnostics.duplicateAllActiveBoundHashes); staleAll=$($diagnostics.staleAllActiveSprites); duplicateAllSprites=$($diagnostics.duplicateAllActiveSpriteDifferentSongs); error=$($action.result.properties.error)" -ExpectedBehavior $expectedBehavior -Screenshot $probeShot
    }
}

function Invoke-QolFeatureSpecSuite {
    Invoke-GameEval @"
new Func<object>(() => {
    var cases = new List<object>();
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Action<string, bool, string> addCase = (name, consistent, details) => cases.Add(new { name = name, consistent = consistent, details = details });

        Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", false);
        object bridge = bridgeType == null ? null : bridgeType.GetProperty("Active").GetValue(null, null);
        if (bridge == null) {
            return new { ok = true, skipped = true, cases = new object[0], error = "QoL bridge is not loaded." };
        }

        Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
        object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (coverList == null) {
            return new { ok = false, skipped = false, cases = cases.ToArray(), error = "CoverListDisplayer not found." };
        }

        Action fixedTicks = () => {
            var fixedUpdate = coverListType.GetMethod("FixedUpdate", Flags);
            if (fixedUpdate != null) {
                for (int i = 0; i < 80; i++) fixedUpdate.Invoke(coverList, new object[0]);
            }
        };

        Action<string, string, string, string, int> applySettings = (grouping, sorting, difficultyFilter, downloadedFilter, selectedDifficulty) => {
            MajdataPlay.SongStorage.OrderBy.Keyword = "";
            MajdataPlay.SongStorage.OrderBy.SortBy = MajdataPlay.SortType.Default;
            coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { selectedDifficulty });
            fixedTicks();
            bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { grouping });
            bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { sorting });
            bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { difficultyFilter });
            bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { downloadedFilter });
            bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
            fixedTicks();
        };

        Func<string, string, string, string, int, string, MajdataPlay.Collections.SongCollection> applyAndOpen = (grouping, sorting, difficultyFilter, downloadedFilter, selectedDifficulty, targetCollection) => {
            applySettings(grouping, sorting, difficultyFilter, downloadedFilter, selectedDifficulty);
            var collections = MajdataPlay.SongStorage.Collections;
            int targetIndex = Array.FindIndex(collections, c => c != null && c.Count > 0 && string.Equals(c.Name, targetCollection, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0) {
                return null;
            }

            MajdataPlay.SongStorage.CollectionIndex = targetIndex;
            collections[targetIndex].Index = 0;
            coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
            var slide = coverListType.GetMethod("SlideListInternal", Flags);
            if (slide != null) slide.Invoke(coverList, new object[] { targetIndex });
            fixedTicks();
            coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
            coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { selectedDifficulty });
            if (slide != null) slide.Invoke(coverList, new object[] { 0 });
            fixedTicks();
            return collections[targetIndex];
        };

        Func<MajdataPlay.ISongDetail, string> hash = song => song == null ? "" : (song.Hash ?? "");
        Func<MajdataPlay.ISongDetail, string> title = song => song == null ? "" : (song.Title ?? "");
        Func<MajdataPlay.ISongDetail, string> artist = song => song == null ? "" : (song.Artist ?? "");
        Func<MajdataPlay.ISongDetail, int> difficultyCount = song => {
            if (song == null || song.Levels == null) return 0;
            var levels = song.Levels.ToArray();
            int count = 0;
            for (int i = 0; i < levels.Length; i++) {
                if (!string.IsNullOrWhiteSpace(levels[i])) count++;
            }
            return count;
        };
        Func<MajdataPlay.ISongDetail, int, string> levelAt = (song, index) => {
            if (song == null || song.Levels == null || index < 0) return "";
            var levels = song.Levels.ToArray();
            return index >= levels.Length ? "" : (levels[index] ?? "");
        };
        Func<MajdataPlay.ISongDetail, int, string> designerAt = (song, index) => {
            if (song == null || song.Designers == null || index < 0) return "";
            var designers = song.Designers.ToArray();
            return index >= designers.Length ? "" : (designers[index] ?? "");
        };
        Func<MajdataPlay.ISongDetail, int, string> designerForDifficulty = (song, index) => {
            if (song == null || song.Designers == null) return "";
            var designers = song.Designers.ToArray();
            if (index >= 0 && index < designers.Length && !string.IsNullOrWhiteSpace(designers[index])) return designers[index] ?? "";
            for (int i = 0; i < designers.Length; i++) {
                if (!string.IsNullOrWhiteSpace(designers[i])) return designers[i] ?? "";
            }
            return "";
        };
        Func<MajdataPlay.ISongDetail[], string> hashes = rows => string.Join("|", (rows ?? new MajdataPlay.ISongDetail[0]).Select(song => hash(song)).ToArray());

        Func<IEnumerable<MajdataPlay.ISongDetail>, MajdataPlay.ISongDetail[]> dedupeRows = rows => {
            var result = new List<MajdataPlay.ISongDetail>();
            var indexByHash = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var song in rows ?? Enumerable.Empty<MajdataPlay.ISongDetail>()) {
                if (song == null) continue;
                string key = hash(song);
                if (string.IsNullOrWhiteSpace(key)) {
                    result.Add(song);
                    continue;
                }

                int existingIndex;
                if (!indexByHash.TryGetValue(key, out existingIndex)) {
                    indexByHash.Add(key, result.Count);
                    result.Add(song);
                    continue;
                }

                if (result[existingIndex] != null && result[existingIndex].IsOnline && !song.IsOnline) {
                    result[existingIndex] = song;
                }
            }
            return result.ToArray();
        };

        Func<IEnumerable<MajdataPlay.ISongDetail>, MajdataPlay.ISongDetail[]> firstRowsByHash = rows => {
            var result = new List<MajdataPlay.ISongDetail>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var song in rows ?? Enumerable.Empty<MajdataPlay.ISongDetail>()) {
                if (song == null || string.IsNullOrWhiteSpace(hash(song))) continue;
                if (seen.Add(hash(song))) {
                    result.Add(song);
                }
            }
            return result.ToArray();
        };

        Func<MajdataPlay.ISongDetail[], bool> uniqueHashes = rows => {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var song in rows ?? new MajdataPlay.ISongDetail[0]) {
                string key = hash(song);
                if (!string.IsNullOrWhiteSpace(key) && !seen.Add(key)) return false;
            }
            return true;
        };

        Func<MajdataPlay.ISongDetail, int, decimal?> levelSortValue = (song, selectedDifficulty) => {
            string level = levelAt(song, selectedDifficulty).Trim();
            if (string.IsNullOrWhiteSpace(level)) return null;
            bool plus = level.EndsWith("+", StringComparison.Ordinal);
            if (plus) level = level.Substring(0, level.Length - 1).Trim();
            decimal value;
            if (!decimal.TryParse(level, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value)) return null;
            return plus ? value + 0.7m : value;
        };

        Func<string, string> titleBucket = value => {
            if (string.IsNullOrWhiteSpace(value)) return "Other";
            char first = char.ToUpperInvariant(value.Trim()[0]);
            if (first >= 'A' && first <= 'Z') return first.ToString();
            if (char.IsDigit(first)) return "#";
            return "Other";
        };

        Func<string, string> levelBucket = value => {
            if (string.IsNullOrWhiteSpace(value)) return "Other";
            string text = value.Trim();
            bool plus = text.EndsWith("+", StringComparison.Ordinal);
            if (plus) text = text.Substring(0, text.Length - 1).Trim();
            decimal number;
            if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out number) || number < 0m) return "Other";
            int floor = (int)number;
            return plus || number != floor ? floor.ToString(System.Globalization.CultureInfo.InvariantCulture) + "+" : floor.ToString(System.Globalization.CultureInfo.InvariantCulture);
        };

        Func<MajdataPlay.ISongDetail[], Func<MajdataPlay.ISongDetail, string>, Dictionary<string, int>> countsBy = (rows, bucket) => {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var song in rows ?? new MajdataPlay.ISongDetail[0]) {
                string key = bucket(song);
                int count;
                counts.TryGetValue(key, out count);
                counts[key] = count + 1;
            }
            return counts;
        };

        Func<Dictionary<string, int>, string> countSummary = counts =>
            string.Join("|", counts.OrderBy(kv => (kv.Key ?? "").ToUpperInvariant()).Select(kv => kv.Key + ":" + kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray());

        Func<Dictionary<string, int>, Dictionary<string, int>, bool> sameCounts = (left, right) =>
            left.Count == right.Count && left.All(kv => right.ContainsKey(kv.Key) && right[kv.Key] == kv.Value);

        Func<MajdataPlay.ISongDetail[], MajdataPlay.ISongDetail[], bool> sameHashes = (left, right) =>
            hashes(left) == hashes(right);

        Func<MajdataPlay.ISongDetail[], string[], string> relativeOrder = (rows, interestingHashes) =>
            string.Join("|", (rows ?? new MajdataPlay.ISongDetail[0]).Select(song => hash(song)).Where(rowHash => interestingHashes.Contains(rowHash)).ToArray());

        Func<IEnumerable<MajdataPlay.Collections.SongCollection>, Dictionary<string, int>, Dictionary<string, int>> matchingCollectionCounts = (collections, expected) => {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var collection in collections ?? Enumerable.Empty<MajdataPlay.Collections.SongCollection>()) {
                if (collection == null || !expected.ContainsKey(collection.Name)) continue;
                int count;
                counts.TryGetValue(collection.Name, out count);
                counts[collection.Name] = count + collection.Count;
            }
            return counts;
        };

        Func<IEnumerable<MajdataPlay.Collections.SongCollection>, Dictionary<string, int>, string> duplicateMatchingCollectionNames = (collections, expected) => {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var collection in collections ?? Enumerable.Empty<MajdataPlay.Collections.SongCollection>()) {
                if (collection == null || !expected.ContainsKey(collection.Name)) continue;
                int count;
                counts.TryGetValue(collection.Name, out count);
                counts[collection.Name] = count + 1;
            }
            return string.Join("|", counts.Where(kv => kv.Value > 1).OrderBy(kv => (kv.Key ?? "").ToUpperInvariant()).Select(kv => kv.Key + ":" + kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        };

        Comparison<MajdataPlay.ISongDetail> songTieBreak = (left, right) => {
            int titleCompare = StringComparer.OrdinalIgnoreCase.Compare(title(left), title(right));
            if (titleCompare != 0) return titleCompare;
            return StringComparer.OrdinalIgnoreCase.Compare(hash(left), hash(right));
        };

        Func<string, string, int, int> compareKnownText = (left, right, tieBreak) => {
            bool leftKnown = !string.IsNullOrWhiteSpace(left);
            bool rightKnown = !string.IsNullOrWhiteSpace(right);
            if (leftKnown && !rightKnown) return -1;
            if (!leftKnown && rightKnown) return 1;
            if (!leftKnown && !rightKnown) return tieBreak;
            int compare = StringComparer.OrdinalIgnoreCase.Compare(left.Trim(), right.Trim());
            return compare != 0 ? compare : tieBreak;
        };

        Func<MajdataPlay.ISongDetail[], Comparison<MajdataPlay.ISongDetail>, MajdataPlay.ISongDetail[]> sorted = (rows, comparison) => {
            var copy = (rows ?? new MajdataPlay.ISongDetail[0]).ToArray();
            Array.Sort(copy, comparison);
            return copy;
        };

        Comparison<MajdataPlay.ISongDetail> titleComparison = (left, right) =>
            compareKnownText(title(left), title(right), songTieBreak(left, right));

        Comparison<MajdataPlay.ISongDetail> artistComparison = (left, right) =>
            compareKnownText(artist(left), artist(right), songTieBreak(left, right));

        Comparison<MajdataPlay.ISongDetail> designerDifficulty0Comparison = (left, right) =>
            compareKnownText(designerForDifficulty(left, 0), designerForDifficulty(right, 0), songTieBreak(left, right));

        Comparison<MajdataPlay.ISongDetail> dateAddedComparison = (left, right) => {
            int compare = right.Timestamp.CompareTo(left.Timestamp);
            return compare != 0 ? compare : songTieBreak(left, right);
        };

        Comparison<MajdataPlay.ISongDetail> difficulty4Comparison = (left, right) => {
            decimal? leftValue = levelSortValue(left, 4);
            decimal? rightValue = levelSortValue(right, 4);
            int tieBreak = songTieBreak(left, right);
            if (leftValue.HasValue && !rightValue.HasValue) return -1;
            if (!leftValue.HasValue && rightValue.HasValue) return 1;
            if (!leftValue.HasValue && !rightValue.HasValue) return tieBreak;
            int compare = leftValue.Value.CompareTo(rightValue.Value);
            return compare != 0 ? compare : tieBreak;
        };

        applySettings("Default", "Default", "No", "Mixed", 0);
        var sourceCollections = MajdataPlay.SongStorage.Collections
            .Where(collection => collection != null &&
                !string.Equals(collection.Name, "Random Recommended", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(collection.Name, "QoL Feature Website", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(collection.Name, "QoL Extensive Website", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var baseAllCollection = sourceCollections.FirstOrDefault(collection => collection != null && string.Equals(collection.Name, "All", StringComparison.OrdinalIgnoreCase));
        var baseJportalCollection = sourceCollections.FirstOrDefault(collection => collection != null && string.Equals(collection.Name, "JPORTAL", StringComparison.OrdinalIgnoreCase));
        var baseAllRaw = baseAllCollection == null ? new MajdataPlay.ISongDetail[0] : baseAllCollection.ToArray();
        var baseJportalRaw = baseJportalCollection == null ? new MajdataPlay.ISongDetail[0] : baseJportalCollection.ToArray();
        var baseAll = firstRowsByHash(baseAllRaw);
        var baseJportal = dedupeRows(baseJportalRaw);
        addCase("feature specs have enough base data", baseAll.Length > 20 && baseJportal.Length >= 6, "all=" + baseAll.Length + "; jportal=" + baseJportal.Length);

        Func<string, int, MajdataPlay.ISongDetail[]> actualRows = (collectionName, selectedDifficulty) => {
            var collection = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && string.Equals(c.Name, collectionName, StringComparison.OrdinalIgnoreCase));
            return collection == null ? new MajdataPlay.ISongDetail[0] : collection.ToArray();
        };

        var expectedTitle = sorted(baseJportal, titleComparison);
        var actualTitle = applyAndOpen("Default", "Title", "No", "Mixed", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
        addCase("sorting title orders the full JPORTAL collection", sameHashes(actualTitle, expectedTitle), "actual=" + hashes(actualTitle.Take(8).ToArray()) + "; expected=" + hashes(expectedTitle.Take(8).ToArray()));

        var expectedArtist = sorted(baseJportal, artistComparison);
        var actualArtist = applyAndOpen("Default", "Artist", "No", "Mixed", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
        addCase("sorting artist orders the full JPORTAL collection", sameHashes(actualArtist, expectedArtist), "actual=" + hashes(actualArtist.Take(8).ToArray()) + "; expected=" + hashes(expectedArtist.Take(8).ToArray()));

        var expectedDesigner = sorted(baseJportal, designerDifficulty0Comparison);
        var actualDesigner = applyAndOpen("Default", "NoteDesigner", "No", "Mixed", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
        addCase("sorting note designer orders the full JPORTAL collection", sameHashes(actualDesigner, expectedDesigner), "actual=" + hashes(actualDesigner.Take(8).ToArray()) + "; expected=" + hashes(expectedDesigner.Take(8).ToArray()));

        var expectedDateAdded = sorted(baseJportal, dateAddedComparison);
        var actualDateAdded = applyAndOpen("Default", "DateAdded", "No", "Mixed", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
        addCase("sorting date added orders newest JPORTAL rows first", sameHashes(actualDateAdded, expectedDateAdded), "actual=" + hashes(actualDateAdded.Take(8).ToArray()) + "; expected=" + hashes(expectedDateAdded.Take(8).ToArray()));

        var expectedDifficulty = sorted(baseJportal, difficulty4Comparison);
        var actualDifficulty = applyAndOpen("Default", "Difficulty", "No", "Mixed", 4, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 4);
        addCase("sorting difficulty uses selected difficulty level", sameHashes(actualDifficulty, expectedDifficulty), "actual=" + hashes(actualDifficulty.Take(8).ToArray()) + "; expected=" + hashes(expectedDifficulty.Take(8).ToArray()));

        var scored = baseJportal.Take(5).ToArray();
        if (scored.Length >= 5) {
            bridgeType.GetMethod("SetSongScoreForDiagnostics").Invoke(null, new object[] { hash(scored[0]), 100.5d, 5, "AP", 1000000L });
            bridgeType.GetMethod("SetSongScoreForDiagnostics").Invoke(null, new object[] { hash(scored[1]), 99.0d, 40, "FC", 900000L });
            bridgeType.GetMethod("SetSongScoreForDiagnostics").Invoke(null, new object[] { hash(scored[2]), 98.0d, 20, "", 950000L });
            bridgeType.GetMethod("SetSongScoreForDiagnostics").Invoke(null, new object[] { hash(scored[3]), 90.0d, 60, "AP", 850000L });
            bridgeType.GetMethod("SetSongScoreForDiagnostics").Invoke(null, new object[] { hash(scored[4]), 80.0d, 10, "", 800000L });
            string[] scoreHashes = scored.Select(song => hash(song)).ToArray();

            var actualRank = applyAndOpen("Default", "Rank", "No", "Mixed", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
            addCase("sorting rank respects diagnostic score facets", relativeOrder(actualRank, scoreHashes) == string.Join("|", scoreHashes), "actualRelative=" + relativeOrder(actualRank, scoreHashes));

            var actualPlayCount = applyAndOpen("Default", "PlayCount", "No", "Mixed", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
            string expectedPlayCount = string.Join("|", new[] { hash(scored[3]), hash(scored[1]), hash(scored[2]), hash(scored[4]), hash(scored[0]) });
            addCase("sorting play count respects diagnostic score facets", relativeOrder(actualPlayCount, scoreHashes) == expectedPlayCount, "actualRelative=" + relativeOrder(actualPlayCount, scoreHashes) + "; expected=" + expectedPlayCount);

            var actualApFc = applyAndOpen("Default", "ApFcRank", "No", "Mixed", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
            string actualApFcRelative = relativeOrder(actualApFc, scoreHashes);
            var actualApFcOrder = actualApFcRelative.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            Func<MajdataPlay.ISongDetail, int> apFcPosition = song => actualApFcOrder.IndexOf(hash(song));
            bool apFcSorted =
                apFcPosition(scored[0]) >= 0 &&
                apFcPosition(scored[1]) >= 0 &&
                apFcPosition(scored[2]) >= 0 &&
                apFcPosition(scored[3]) >= 0 &&
                apFcPosition(scored[4]) >= 0 &&
                apFcPosition(scored[0]) < apFcPosition(scored[1]) &&
                apFcPosition(scored[3]) < apFcPosition(scored[1]) &&
                apFcPosition(scored[1]) < apFcPosition(scored[2]) &&
                apFcPosition(scored[1]) < apFcPosition(scored[4]);
            addCase("sorting AP/FC rank puts AP before FC before clear-only rows", apFcSorted, "actualRelative=" + actualApFcRelative);

            var actualDx = applyAndOpen("Default", "DxScore", "No", "Mixed", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
            string expectedDx = string.Join("|", new[] { hash(scored[0]), hash(scored[2]), hash(scored[1]), hash(scored[3]), hash(scored[4]) });
            addCase("sorting DX score respects diagnostic score facets", relativeOrder(actualDx, scoreHashes) == expectedDx, "actualRelative=" + relativeOrder(actualDx, scoreHashes) + "; expected=" + expectedDx);
        }

        applySettings("DifficultyBracket", "Default", "No", "Mixed", 0);
        var bracketCollections = MajdataPlay.SongStorage.Collections.Where(c => c != null && !string.Equals(c.Name, "MyFavorites", StringComparison.OrdinalIgnoreCase) && !string.Equals(c.Name, "Random Recommended", StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] bracketOrder = new[] { "Easy", "Basic", "Advance", "Expert", "Master", "ReMaster", "UTAGE", "Other" };
        var expectedBracketCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in bracketOrder) expectedBracketCounts[name] = 0;
        foreach (var song in baseAll) {
            bool any = false;
            var levels = song == null || song.Levels == null ? new string[0] : song.Levels.ToArray();
            for (int i = 0; i < Math.Min(7, levels.Length); i++) {
                if (!string.IsNullOrWhiteSpace(levels[i])) {
                    expectedBracketCounts[bracketOrder[i]]++;
                    any = true;
                }
            }
            if (!any) expectedBracketCounts["Other"]++;
        }
        expectedBracketCounts = expectedBracketCounts.Where(kv => kv.Value > 0 || kv.Key == "Other").ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        string bracketDuplicates = duplicateMatchingCollectionNames(bracketCollections, expectedBracketCounts);
        var actualBracketCounts = matchingCollectionCounts(bracketCollections, expectedBracketCounts);
        addCase("grouping difficulty bracket creates unique expected folders", string.IsNullOrWhiteSpace(bracketDuplicates), "duplicates=" + bracketDuplicates);
        addCase("grouping difficulty bracket creates expected folders and counts", sameCounts(actualBracketCounts, expectedBracketCounts), "actual=" + countSummary(actualBracketCounts) + "; expected=" + countSummary(expectedBracketCounts));
        var masterFolder = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "Master");
        addCase("grouping difficulty bracket Master folder contains only Master rows", masterFolder != null && masterFolder.ToArray().All(song => !string.IsNullOrWhiteSpace(levelAt(song, 4))), "count=" + (masterFolder == null ? 0 : masterFolder.Count));

        applySettings("DifficultyLevel", "Default", "No", "Mixed", 4);
        var expectedLevelCounts = baseAll
            .SelectMany(song => {
                var levels = song.Levels == null ? new string[0] : song.Levels.ToArray();
                var buckets = levels.Where(level => !string.IsNullOrWhiteSpace(level)).Select(level => levelBucket(level)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return (buckets.Length == 0 ? new[] { "Other" } : buckets).Select(bucket => new { Bucket = bucket, Song = song });
            })
            .GroupBy(row => row.Bucket.ToUpperInvariant())
            .ToDictionary(group => group.First().Bucket, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        string levelDuplicates = duplicateMatchingCollectionNames(MajdataPlay.SongStorage.Collections, expectedLevelCounts);
        var actualLevelCounts = matchingCollectionCounts(MajdataPlay.SongStorage.Collections, expectedLevelCounts);
        addCase("grouping difficulty level creates unique expected folders", string.IsNullOrWhiteSpace(levelDuplicates), "duplicates=" + levelDuplicates);
        addCase("grouping difficulty level uses selected difficulty buckets", sameCounts(actualLevelCounts, expectedLevelCounts), "actual=" + countSummary(actualLevelCounts) + "; expected=" + countSummary(expectedLevelCounts));

        applySettings("Title", "Default", "No", "Mixed", 0);
        var expectedTitleCounts = countsBy(baseAll, song => titleBucket(title(song)));
        string titleDuplicates = duplicateMatchingCollectionNames(MajdataPlay.SongStorage.Collections, expectedTitleCounts);
        var actualTitleCounts = matchingCollectionCounts(MajdataPlay.SongStorage.Collections, expectedTitleCounts);
        addCase("grouping title creates unique expected folders", string.IsNullOrWhiteSpace(titleDuplicates), "duplicates=" + titleDuplicates);
        addCase("grouping title creates expected title buckets", sameCounts(actualTitleCounts, expectedTitleCounts), "actual=" + countSummary(actualTitleCounts) + "; expected=" + countSummary(expectedTitleCounts));

        applySettings("Artist", "Default", "No", "Mixed", 0);
        var expectedArtistCounts = countsBy(baseAll, song => string.IsNullOrWhiteSpace(artist(song)) ? "Unknown Artist" : artist(song).Trim());
        string artistDuplicates = duplicateMatchingCollectionNames(MajdataPlay.SongStorage.Collections, expectedArtistCounts);
        var actualArtistCounts = matchingCollectionCounts(MajdataPlay.SongStorage.Collections, expectedArtistCounts);
        addCase("grouping artist creates unique expected folders", string.IsNullOrWhiteSpace(artistDuplicates), "duplicates=" + artistDuplicates);
        addCase("grouping artist creates expected artist buckets", sameCounts(actualArtistCounts, expectedArtistCounts), "actualCount=" + actualArtistCounts.Count + "; expectedCount=" + expectedArtistCounts.Count);

        applySettings("Rank", "Default", "No", "Mixed", 0);
        var rankSss = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "SSS+");
        var rankApHashes = rankSss == null ? "" : hashes(rankSss.ToArray());
        addCase("grouping rank includes diagnostic SSS+ row", rankSss != null && rankSss.ToArray().Any(song => hash(song) == hash(scored[0])), "sssPlusHashes=" + rankApHashes);

        var filterCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var filter in new[] { "MoreThan1", "MoreThan2", "MoreThan3" }) {
            int threshold = filter == "MoreThan1" ? 1 : filter == "MoreThan2" ? 2 : 3;
            var expected = dedupeRows(baseAllRaw.Where(song => difficultyCount(song) > threshold));
            var actual = applyAndOpen("Default", "Default", filter, "Mixed", 0, "All") == null ? new MajdataPlay.ISongDetail[0] : actualRows("All", 0);
            filterCounts[filter] = actual.Length;
            addCase("difficulty filter " + filter + " keeps only matching rows", actual.Length == expected.Length && uniqueHashes(actual) && actual.All(song => difficultyCount(song) > threshold), "actual=" + actual.Length + "; expected=" + expected.Length + "; bad=" + string.Join("|", actual.Where(song => difficultyCount(song) <= threshold).Take(5).Select(song => hash(song)).ToArray()));
        }
        addCase("difficulty filter counts are monotonic", filterCounts["MoreThan1"] >= filterCounts["MoreThan2"] && filterCounts["MoreThan2"] >= filterCounts["MoreThan3"], "counts=" + countSummary(filterCounts.ToDictionary(kv => kv.Key, kv => kv.Value)));

        var expectedDownloadedJportal = dedupeRows(baseJportalRaw.Where(song => !song.IsOnline));
        var actualDownloadedJportal = applyAndOpen("Default", "Default", "No", "DownloadedOnly", 0, "JPORTAL") == null ? new MajdataPlay.ISongDetail[0] : actualRows("JPORTAL", 0);
        addCase("downloaded filter downloaded-only hides online JPORTAL rows", actualDownloadedJportal.Length == expectedDownloadedJportal.Length && actualDownloadedJportal.All(song => !song.IsOnline), "actual=" + actualDownloadedJportal.Length + "; expected=" + expectedDownloadedJportal.Length);

        var expectedOnlineAll = dedupeRows(baseAllRaw.Where(song => song.IsOnline));
        var actualOnlineAll = applyAndOpen("Default", "Default", "No", "OnlineOnly", 0, "All") == null ? new MajdataPlay.ISongDetail[0] : actualRows("All", 0);
        addCase("downloaded filter online-only keeps only online rows", actualOnlineAll.Length == expectedOnlineAll.Length && expectedOnlineAll.Length > 0 && actualOnlineAll.All(song => song.IsOnline), "actual=" + actualOnlineAll.Length + "; expected=" + expectedOnlineAll.Length);

        var actualMixedAll = applyAndOpen("Default", "Default", "No", "Mixed", 0, "All") == null ? new MajdataPlay.ISongDetail[0] : actualRows("All", 0);
        addCase("downloaded filter mixed keeps downloaded and online rows", actualMixedAll.Length == baseAllRaw.Length && actualMixedAll.Any(song => song.IsOnline) && actualMixedAll.Any(song => !song.IsOnline), "actual=" + actualMixedAll.Length + "; expected=" + baseAllRaw.Length);

        applySettings("Default", "Default", "No", "Mixed", 0);
        var random = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == "Random Recommended");
        var collectionNames = MajdataPlay.SongStorage.Collections.Where(c => c != null).Select(c => c.Name).ToArray();
        int randomIndex = Array.FindIndex(collectionNames, name => name == "Random Recommended");
        int favoritesIndex = Array.FindIndex(collectionNames, name => name == "MyFavorites");
        var availableHashes = new HashSet<string>(baseAll.Select(song => hash(song)).Where(rowHash => !string.IsNullOrWhiteSpace(rowHash)), StringComparer.Ordinal);
        var randomRows = random == null ? new MajdataPlay.ISongDetail[0] : random.ToArray();
        addCase("random recommended folder exists after favorites", random != null && randomIndex >= 0 && favoritesIndex >= 0 && randomIndex == favoritesIndex + 1, "folders=" + string.Join("|", collectionNames.Take(8).ToArray()));
        addCase("random recommended rows are unique available songs", randomRows.Length > 0 && randomRows.Length <= 12 && uniqueHashes(randomRows) && randomRows.All(song => availableHashes.Contains(hash(song))), "count=" + randomRows.Length + "; hashes=" + hashes(randomRows));

        string websiteName = "QoL Extensive Website";
        var onlineHashes = new HashSet<string>(baseAllRaw.Where(song => song != null && song.IsOnline).Select(song => hash(song)).Where(rowHash => !string.IsNullOrWhiteSpace(rowHash)), StringComparer.Ordinal);
        var websiteSeed = baseAllRaw.Where(song => song != null && !song.IsOnline && !string.IsNullOrWhiteSpace(hash(song)) && !onlineHashes.Contains(hash(song))).Take(3).ToArray();
        if (websiteSeed.Length < 3) {
            websiteSeed = baseAllRaw.Where(song => song != null && !song.IsOnline && !string.IsNullOrWhiteSpace(hash(song))).Take(3).ToArray();
        }
        string missingHash = "missing-qol-extensive-hash";
        string websiteHashes = websiteSeed.Length >= 3
            ? string.Join("|", new[] { hash(websiteSeed[0]), hash(websiteSeed[1]), hash(websiteSeed[1]), missingHash, hash(websiteSeed[2]) })
            : "";
        if (websiteSeed.Length >= 3) {
            bridgeType.GetMethod("InstallWebsiteCollectionForDiagnostics").Invoke(null, new object[] { websiteName, websiteHashes, 5 });
            var websiteCollection = applyAndOpen("Default", "Default", "No", "Mixed", 0, websiteName);
            var websiteRows = websiteCollection == null ? new MajdataPlay.ISongDetail[0] : websiteCollection.ToArray();
            string expectedWebsite = string.Join("|", websiteSeed.Select(song => hash(song)).ToArray());
            addCase("website collection resolves requested hashes once and preserves order", websiteCollection != null && hashes(websiteRows) == expectedWebsite, "actual=" + hashes(websiteRows) + "; expected=" + expectedWebsite);
            addCase("website collection reports resolved subset count", websiteCollection != null && websiteCollection.Count == 3, "count=" + (websiteCollection == null ? 0 : websiteCollection.Count) + "; request=" + websiteHashes);

            var websiteDownloaded = applyAndOpen("Default", "Default", "No", "DownloadedOnly", 0, websiteName);
            var websiteDownloadedRows = websiteDownloaded == null ? new MajdataPlay.ISongDetail[0] : websiteDownloaded.ToArray();
            addCase("website collection downloaded-only scope keeps downloaded resolved rows", hashes(websiteDownloadedRows) == expectedWebsite && websiteDownloadedRows.All(song => !song.IsOnline), "actual=" + hashes(websiteDownloadedRows));

            applySettings("Default", "Default", "No", "OnlineOnly", 0);
            var websiteOnline = MajdataPlay.SongStorage.Collections.FirstOrDefault(c => c != null && c.Name == websiteName);
            var websiteOnlineRows = websiteOnline == null ? new MajdataPlay.ISongDetail[0] : websiteOnline.ToArray();
            addCase("website collection online-only scope drops local-only requested rows", websiteOnlineRows.Length == 0, "onlineRows=" + hashes(websiteOnlineRows));

            string websiteDiagnostics = Convert.ToString(bridgeType.GetMethod("WebsiteCollectionDiagnosticsSnapshot").Invoke(null, null), System.Globalization.CultureInfo.InvariantCulture);
            addCase("website collection diagnostics names installed collection", websiteDiagnostics.Contains(websiteName), websiteDiagnostics);
        } else {
            addCase("website collection specs have enough local seed rows", false, "localSeedCount=" + websiteSeed.Length);
        }

        return new { ok = true, skipped = false, cases = cases.ToArray(), error = "" };
    } catch (Exception ex) {
        return new { ok = false, skipped = false, cases = cases.ToArray(), error = ex.ToString() };
    }
})()
"@
}

function Add-QolFeatureSpecCases {
    param(
        [System.Collections.Generic.List[object]]$Cases,
        [System.Collections.Generic.List[object]]$Screenshots,
        [string]$Label,
        [object]$ReferenceState,
        [object]$ReferenceScreenshot
    )

    if (-not $ReferenceState.upperScreen.qolLoaded) {
        Add-Case $Cases "QoL feature specification suite" $true "skipped: QoL bridge is not loaded." -ExpectedBehavior "QoL feature specs are skipped when the QoL bridge is not loaded, and the reference screenshot should still show a readable vanilla JPORTAL state." -Screenshot $ReferenceScreenshot
        return
    }

    $rawSpec = Invoke-QolFeatureSpecSuite
    $spec = Convert-SerializedGameStateValue $rawSpec.result.properties
    if (-not $spec.ok) {
        $failureState = Read-VisibleGameState
        $failureShot = Capture-JportalScreenshot $Label "13-spec-suite-failure" "QoL feature specification suite failed before returning individual cases." $failureState "The QoL feature specification suite should complete and leave the visible JPORTAL state readable."
        Set-ScreenshotExpectation $failureShot $failureState.ok "stateOk=$($failureState.ok); selecting=$($failureState.screen.selecting); error=$($failureState.error)" | Out-Null
        $Screenshots.Add($failureShot) | Out-Null
        Add-Case $Cases "QoL feature specification suite completes" $false $spec.error -ExpectedBehavior "The QoL feature specification suite should complete and return individual feature cases." -Screenshot $failureShot
        return
    }

    $screenshotMap = Capture-QolFeatureSpecScreenshots -SpecCases @($spec.cases) -Screenshots $Screenshots -Label $Label
    foreach ($case in @($spec.cases)) {
        $key = Get-QolFeatureSpecScreenshotKey $case.name
        $shot = if ($screenshotMap.ContainsKey($key)) { $screenshotMap[$key] } else { $ReferenceScreenshot }
        Add-Case $Cases $case.name ([bool]$case.consistent) $case.details -Screenshot $shot
    }
}

function Wait-ForKnownBugScene {
    param(
        [string]$Scene,
        [switch]$RequireCoverList
    )

    $sceneLiteral = Convert-ToCSharpStringLiteral $Scene
    $requireCoverListLiteral = if ($RequireCoverList) { "true" } else { "false" }
    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Seconds 1
        $state = Invoke-GameEval @"
new Func<object>(() => {
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", false);
    bool coverListReady = coverListType != null &&
        UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .Any(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    return new {
        scene = scene,
        coverListReady = coverListReady,
        ready = scene == $sceneLiteral && (!$requireCoverListLiteral || coverListReady)
    };
})()
"@

        if ($state.result.properties.ready) {
            return $state.result.properties
        }
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for scene '$Scene'."
}

function Invoke-KnownBugPlayCountSortingCanary {
    Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", false);
        object bridge = bridgeType == null ? null : bridgeType.GetProperty("Active").GetValue(null, null);
        if (bridge == null) {
            return new { ok = false, consistent = false, details = "QoL bridge is not loaded.", error = "QoL bridge is not loaded." };
        }

        Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
        object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (coverList == null) {
            return new { ok = false, consistent = false, details = "CoverListDisplayer not found.", error = "CoverListDisplayer not found." };
        }

        Action fixedTicks = () => {
            var fixedUpdate = coverListType.GetMethod("FixedUpdate", Flags);
            if (fixedUpdate != null) {
                for (int i = 0; i < 90; i++) fixedUpdate.Invoke(coverList, new object[0]);
            }
        };

        Func<MajdataPlay.ISongDetail, string> hash = song => song == null ? "" : (song.Hash ?? "");

        Action<string, string, string, string, int> applySettings = (grouping, sorting, difficultyFilter, downloadedFilter, selectedDifficulty) => {
            MajdataPlay.SongStorage.OrderBy.Keyword = "";
            MajdataPlay.SongStorage.OrderBy.SortBy = MajdataPlay.SortType.Default;
            coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { selectedDifficulty });
            fixedTicks();
            bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { grouping });
            bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { sorting });
            bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { difficultyFilter });
            bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { downloadedFilter });
            bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
            fixedTicks();
        };

        Type onlineType = Type.GetType("MajdataPlay.Net.Online, Assembly-CSharp", true);
        var getCachedResponse = onlineType.GetMethod("GetCachedResponse", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Type onlineSongType = getCachedResponse.GetParameters()[0].ParameterType;

        applySettings("Default", "Default", "No", "Mixed", 0);
        var onlineRows = MajdataPlay.SongStorage.Collections
            .Where(collection => collection != null && collection.Count > 0)
            .SelectMany(collection => collection.ToArray())
            .Where(song => song != null && song.IsOnline && onlineSongType.IsAssignableFrom(song.GetType()) && !string.IsNullOrWhiteSpace(hash(song)))
            .GroupBy(song => hash(song))
            .Select(group => group.First())
            .Take(6)
            .ToArray();
        if (onlineRows.Length < 4) {
            return new { ok = false, consistent = false, details = "Need at least four online songs to seed visible online play counts; found " + onlineRows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".", error = "Need at least four online songs to seed visible online play counts." };
        }

        string collectionName = "QoL Known Bug Play Count";
        string requestedHashes = string.Join("|", onlineRows.Select(song => hash(song)).ToArray());
        bridgeType.GetMethod("InstallWebsiteCollectionForDiagnostics").Invoke(null, new object[] { collectionName, requestedHashes, onlineRows.Length });

        int[] onlinePlayCounts = new[] { 10, 60, 20, 100, 40, 80 };
        var expectedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < onlineRows.Length; i++) {
            expectedCounts[hash(onlineRows[i])] = onlinePlayCounts[i];
            object cachedResponse = getCachedResponse.Invoke(null, new object[] { onlineRows[i] });
            object interact = cachedResponse.GetType().GetProperty("Interact").GetValue(cachedResponse, null);
            var response = new MajdataPlay.MajNetSongInteract {
                IsLiked = false,
                Plays = onlinePlayCounts[i],
                Likes = new string[0],
                DisLikeCount = 0,
                Comments = new MajdataPlay.ChartCommentSummary[0]
            };
            interact.GetType().GetProperty("Response").SetValue(interact, response, null);
            interact.GetType().GetProperty("LastActive").SetValue(interact, DateTime.Now, null);
            bridgeType.GetMethod("SetSongOnlinePlayCountForDiagnostics").Invoke(null, new object[] { hash(onlineRows[i]), onlinePlayCounts[i] });
        }

        applySettings("Default", "PlayCount", "No", "Mixed", 0);
        var collections = MajdataPlay.SongStorage.Collections;
        int targetIndex = Array.FindIndex(collections, c => c != null && string.Equals(c.Name, collectionName, StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0) {
            return new { ok = false, consistent = false, details = "Diagnostic online play-count folder disappeared after sorting.", error = "Diagnostic online play-count folder disappeared after sorting." };
        }

        var target = collections[targetIndex];
        string[] expectedFolder = target.ToArray()
            .Where(song => song != null && expectedCounts.ContainsKey(hash(song)))
            .OrderByDescending(song => expectedCounts[hash(song)])
            .Select(song => hash(song))
            .ToArray();
        string[] actualFolder = target.ToArray()
            .Where(song => song != null && expectedCounts.ContainsKey(hash(song)))
            .Select(song => hash(song))
            .ToArray();
        string[] expected = expectedFolder
            .Take(Math.Min(4, expectedCounts.Count))
            .ToArray();
        if (expected.Length < 4) {
            return new { ok = false, consistent = false, details = "Could not find four diagnostic online play-count rows after sorting.", error = "Could not find four diagnostic online play-count rows after sorting." };
        }

        bool folderExact = actualFolder.SequenceEqual(expectedFolder);
        MajdataPlay.SongStorage.CollectionIndex = targetIndex;
        target.Index = 0;
        var slide = coverListType.GetMethod("SlideListInternal", Flags);
        coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
        if (slide != null) slide.Invoke(coverList, new object[] { targetIndex });
        fixedTicks();
        coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
        coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { 0 });
        if (slide != null) slide.Invoke(coverList, new object[] { 0 });
        fixedTicks();

        var rows = new List<object>();
        var observed = new List<string>();
        var observedCounts = new List<int>();
        var slideList = coverListType.GetMethod("SlideList", Flags);
        for (int step = 0; step < expected.Length; step++) {
            var selected = coverListType.GetProperty("SelectedSong", Flags).GetValue(coverList, null) as MajdataPlay.ISongDetail;
            string selectedHash = hash(selected);
            int playCount = expectedCounts.ContainsKey(selectedHash) ? expectedCounts[selectedHash] : -1;
            int selectedIndex = Convert.ToInt32(coverListType.GetField("desiredListPos", Flags).GetValue(coverList));
            observed.Add(selectedHash);
            observedCounts.Add(playCount);
            rows.Add(new {
                step = step,
                selectedIndex = selectedIndex,
                title = selected == null ? "" : selected.Title,
                hash = selectedHash,
                diagnosticPlayCount = playCount
            });

            if (step + 1 < expected.Length && slideList != null) {
                slideList.Invoke(coverList, new object[] { 1 });
                fixedTicks();
            }
        }

        bool nonIncreasing = true;
        for (int i = 1; i < observedCounts.Count; i++) {
            if (observedCounts[i - 1] < observedCounts[i]) {
                nonIncreasing = false;
            }
        }

        string observedText = string.Join("|", observed.ToArray());
        string expectedText = string.Join("|", expected);
        string actualFolderText = string.Join("|", actualFolder);
        string expectedFolderText = string.Join("|", expectedFolder);
        bool observedExact = observedText == expectedText;
        bool cursorStartedAtFirst = rows.Count > 0 && Convert.ToInt32(rows[0].GetType().GetProperty("selectedIndex").GetValue(rows[0], null)) == 0 && observed[0] == expected[0];
        bool consistent = folderExact && cursorStartedAtFirst && observedExact && nonIncreasing;
        return new {
            ok = true,
            consistent = consistent,
            collection = collectionName,
            expected = expectedText,
            observed = observedText,
            actualFolder = actualFolderText,
            expectedFolder = expectedFolderText,
            observations = rows.ToArray(),
            details = "collection=" + collectionName + "; expectedFolder=" + expectedFolderText + "; actualFolder=" + actualFolderText + "; expectedByVisibleOnlinePlayCounts=" + expectedText + "; observedHashes=" + observedText + "; observedVisibleOnlinePlayCounts=" + string.Join("|", observedCounts.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray()) + "; folderExact=" + folderExact + "; cursorStartedAtFirst=" + cursorStartedAtFirst + "; observedExact=" + observedExact + "; nonIncreasing=" + nonIncreasing,
            error = ""
        };
    } catch (Exception ex) {
        return new { ok = false, consistent = false, details = ex.ToString(), error = ex.ToString() };
    }
})()
"@
}

function Invoke-KnownBugLevelGroupingAllDifficultyCanary {
    Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", false);
        object bridge = bridgeType == null ? null : bridgeType.GetProperty("Active").GetValue(null, null);
        if (bridge == null) {
            return new { ok = false, consistent = false, details = "QoL bridge is not loaded.", error = "QoL bridge is not loaded." };
        }

        Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
        object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (coverList == null) {
            return new { ok = false, consistent = false, details = "CoverListDisplayer not found.", error = "CoverListDisplayer not found." };
        }

        Action fixedTicks = () => {
            var fixedUpdate = coverListType.GetMethod("FixedUpdate", Flags);
            if (fixedUpdate != null) {
                for (int i = 0; i < 90; i++) fixedUpdate.Invoke(coverList, new object[0]);
            }
        };

        Action<string, string, string, string, int> applySettings = (grouping, sorting, difficultyFilter, downloadedFilter, selectedDifficulty) => {
            MajdataPlay.SongStorage.OrderBy.Keyword = "";
            MajdataPlay.SongStorage.OrderBy.SortBy = MajdataPlay.SortType.Default;
            coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { selectedDifficulty });
            fixedTicks();
            bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { grouping });
            bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { sorting });
            bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { difficultyFilter });
            bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { downloadedFilter });
            bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
            fixedTicks();
        };

        Func<MajdataPlay.ISongDetail, string> hash = song => song == null ? "" : (song.Hash ?? "");
        Func<string, string> levelBucket = value => {
            if (string.IsNullOrWhiteSpace(value)) return "Other";
            string text = value.Trim();
            bool plus = text.EndsWith("+", StringComparison.Ordinal);
            if (plus) text = text.Substring(0, text.Length - 1).Trim();
            decimal number;
            if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out number) || number < 0m) return "Other";
            int floor = (int)number;
            return plus || number != floor ? floor.ToString(System.Globalization.CultureInfo.InvariantCulture) + "+" : floor.ToString(System.Globalization.CultureInfo.InvariantCulture);
        };

        applySettings("Default", "Default", "No", "Mixed", 4);
        var all = MajdataPlay.SongStorage.Collections.FirstOrDefault(collection => collection != null && string.Equals(collection.Name, "All", StringComparison.OrdinalIgnoreCase));
        if (all == null || all.Count == 0) {
            return new { ok = false, consistent = false, details = "All collection was not available.", error = "All collection was not available." };
        }

        var baseRows = new List<MajdataPlay.ISongDetail>();
        var seenBase = new HashSet<string>(StringComparer.Ordinal);
        foreach (var song in all.ToArray()) {
            string rowHash = hash(song);
            if (song != null && !string.IsNullOrWhiteSpace(rowHash) && seenBase.Add(rowHash)) {
                baseRows.Add(song);
            }
        }

        var expectedRows = new Dictionary<string, List<MajdataPlay.ISongDetail>>(StringComparer.OrdinalIgnoreCase);
        var expectedHashes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var song in baseRows) {
            var levels = song.Levels == null ? new string[0] : song.Levels.ToArray();
            bool added = false;
            for (int i = 0; i < levels.Length; i++) {
                if (string.IsNullOrWhiteSpace(levels[i])) continue;
                string bucket = levelBucket(levels[i]);
                if (!expectedRows.ContainsKey(bucket)) {
                    expectedRows[bucket] = new List<MajdataPlay.ISongDetail>();
                    expectedHashes[bucket] = new HashSet<string>(StringComparer.Ordinal);
                }
                string rowHash = hash(song);
                if (expectedHashes[bucket].Add(rowHash)) {
                    expectedRows[bucket].Add(song);
                }
                added = true;
            }
            if (!added) {
                if (!expectedRows.ContainsKey("Other")) {
                    expectedRows["Other"] = new List<MajdataPlay.ISongDetail>();
                    expectedHashes["Other"] = new HashSet<string>(StringComparer.Ordinal);
                }
                string rowHash = hash(song);
                if (expectedHashes["Other"].Add(rowHash)) {
                    expectedRows["Other"].Add(song);
                }
            }
        }

        applySettings("DifficultyLevel", "Default", "No", "Mixed", 4);
        var collections = MajdataPlay.SongStorage.Collections;
        string chosenBucket = "";
        MajdataPlay.Collections.SongCollection chosenCollection = null;
        string[] chosenExpected = new string[0];
        string[] chosenActual = new string[0];
        string[] chosenMissing = new string[0];
        foreach (var entry in expectedRows) {
            var collection = collections.FirstOrDefault(c => c != null && string.Equals(c.Name, entry.Key, StringComparison.OrdinalIgnoreCase));
            if (collection == null || collection.Count == 0) {
                continue;
            }
            var actualSet = new HashSet<string>(collection.ToArray().Select(song => hash(song)).Where(rowHash => !string.IsNullOrWhiteSpace(rowHash)), StringComparer.Ordinal);
            string[] missing = entry.Value.Select(song => hash(song)).Where(rowHash => !actualSet.Contains(rowHash)).ToArray();
            if (missing.Length > 0) {
                chosenBucket = entry.Key;
                chosenCollection = collection;
                chosenExpected = entry.Value.Select(song => hash(song)).ToArray();
                chosenActual = collection.ToArray().Select(song => hash(song)).Where(rowHash => !string.IsNullOrWhiteSpace(rowHash)).ToArray();
                chosenMissing = missing;
                break;
            }
        }

        if (chosenCollection == null) {
            return new {
                ok = true,
                consistent = true,
                bucket = "",
                details = "No level bucket with missing cross-difficulty songs was found.",
                error = ""
            };
        }

        int targetIndex = Array.FindIndex(collections, c => object.ReferenceEquals(c, chosenCollection));
        if (targetIndex < 0) {
            targetIndex = Array.FindIndex(collections, c => c != null && string.Equals(c.Name, chosenBucket, StringComparison.OrdinalIgnoreCase));
        }
        MajdataPlay.SongStorage.CollectionIndex = targetIndex;
        chosenCollection.Index = 0;
        var slide = coverListType.GetMethod("SlideListInternal", Flags);
        coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
        if (slide != null) slide.Invoke(coverList, new object[] { targetIndex });
        fixedTicks();
        coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
        coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { 4 });
        if (slide != null) slide.Invoke(coverList, new object[] { 0 });
        fixedTicks();

        bool consistent = chosenMissing.Length == 0 && chosenActual.Length == chosenExpected.Length;
        return new {
            ok = true,
            consistent = consistent,
            bucket = chosenBucket,
            expectedCount = chosenExpected.Length,
            actualCount = chosenActual.Length,
            missingCount = chosenMissing.Length,
            missing = string.Join("|", chosenMissing.Take(12).ToArray()),
            details = "bucket=" + chosenBucket + "; expectedAcrossAllDifficulties=" + chosenExpected.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; actualFolderCount=" + chosenActual.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; missingCount=" + chosenMissing.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; missingHashes=" + string.Join("|", chosenMissing.Take(12).ToArray()),
            error = ""
        };
    } catch (Exception ex) {
        return new { ok = false, consistent = false, details = ex.ToString(), error = ex.ToString() };
    }
})()
"@
}

function Invoke-KnownBugSortPersistencePrepareCanary {
    Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", false);
        object bridge = bridgeType == null ? null : bridgeType.GetProperty("Active").GetValue(null, null);
        if (bridge == null) {
            return new { ok = false, requested = false, collection = "", selectedHash = "", selectedDifficulty = -1, details = "QoL bridge is not loaded.", error = "QoL bridge is not loaded." };
        }
        object websiteCollections = bridgeType.GetField("_websiteCollections", Flags).GetValue(bridge);
        var clearWebsiteCollections = websiteCollections == null ? null : websiteCollections.GetType().GetMethod("Clear");
        if (clearWebsiteCollections != null) {
            clearWebsiteCollections.Invoke(websiteCollections, null);
        }

        Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
        object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (coverList == null) {
            return new { ok = false, requested = false, collection = "", selectedHash = "", selectedDifficulty = -1, details = "CoverListDisplayer not found.", error = "CoverListDisplayer not found." };
        }

        Action fixedTicks = () => {
            var fixedUpdate = coverListType.GetMethod("FixedUpdate", Flags);
            if (fixedUpdate != null) {
                for (int i = 0; i < 90; i++) fixedUpdate.Invoke(coverList, new object[0]);
            }
        };

        Action<string, string, string, string, int> applySettings = (grouping, sorting, difficultyFilter, downloadedFilter, selectedDifficulty) => {
            MajdataPlay.SongStorage.OrderBy.Keyword = "";
            MajdataPlay.SongStorage.OrderBy.SortBy = MajdataPlay.SortType.Default;
            coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { selectedDifficulty });
            fixedTicks();
            bridgeType.GetMethod("SetGroupingModeForDiagnostics").Invoke(null, new object[] { grouping });
            bridgeType.GetMethod("SetSortingModeForDiagnostics").Invoke(null, new object[] { sorting });
            bridgeType.GetMethod("SetDifficultyFilterForDiagnostics").Invoke(null, new object[] { difficultyFilter });
            bridgeType.GetMethod("SetDownloadedSongsFilterForDiagnostics").Invoke(null, new object[] { downloadedFilter });
            bridgeType.GetMethod("ApplySettingsForDiagnostics").Invoke(null, null);
            fixedTicks();
        };

        Func<MajdataPlay.ISongDetail, string> hash = song => song == null ? "" : (song.Hash ?? "");
        Func<MajdataPlay.ISongDetail, int, bool> hasLevel = (song, difficulty) => {
            if (song == null || difficulty < 0 || song.Levels == null) return false;
            var levels = song.Levels.ToArray();
            return difficulty < levels.Length && !string.IsNullOrWhiteSpace(levels[difficulty]);
        };
        Func<MajdataPlay.Collections.SongCollection, int, string> carouselWindow = (collection, centerIndex) => {
            if (collection == null || collection.Count == 0) return "";
            var rows = collection.ToArray();
            int start = Math.Max(0, centerIndex - 3);
            int end = Math.Min(rows.Length - 1, centerIndex + 3);
            return string.Join("|", Enumerable.Range(start, end - start + 1).Select(i => hash(rows[i])).ToArray());
        };

        int[] difficultyPreference = new[] { 4, 3, 2, 1, 0 };
        string targetName = "";
        int selectedDifficulty = -1;
        foreach (int difficulty in difficultyPreference) {
            applySettings("DifficultyLevel", "Difficulty", "No", "Mixed", difficulty);
            MajdataPlay.Collections.SongCollection groupedFolder = null;
            foreach (var candidate in MajdataPlay.SongStorage.Collections) {
                if (candidate == null || candidate.Count < 3 || candidate.Name == "All" || candidate.Name == "MyFavorites" || candidate.Name == "Random Recommended" || candidate.Name == "Other") {
                    continue;
                }
                if (!candidate.ToArray().Any(song => hasLevel(song, difficulty))) {
                    continue;
                }
                if (groupedFolder == null || candidate.Count > groupedFolder.Count) {
                    groupedFolder = candidate;
                }
            }
            if (groupedFolder != null) {
                targetName = groupedFolder.Name;
                selectedDifficulty = difficulty;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(targetName) || selectedDifficulty < 0) {
            return new { ok = false, requested = false, collection = "", selectedHash = "", selectedDifficulty = -1, details = "No playable grouped level folder was available for difficulty-sort gameplay persistence.", error = "No playable grouped level folder was available for difficulty-sort gameplay persistence." };
        }

        applySettings("DifficultyLevel", "Difficulty", "No", "Mixed", selectedDifficulty);
        var collections = MajdataPlay.SongStorage.Collections;
        int targetIndex = Array.FindIndex(collections, c => c != null && string.Equals(c.Name, targetName, StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0) {
            return new { ok = false, requested = false, collection = targetName, selectedHash = "", selectedDifficulty = selectedDifficulty, details = "Target folder disappeared after applying difficulty sorting.", error = "Target folder disappeared after applying difficulty sorting." };
        }

        var target = collections[targetIndex];
        int songIndex = Array.FindIndex(target.ToArray(), song => hasLevel(song, selectedDifficulty));
        if (songIndex < 0) {
            return new { ok = false, requested = false, collection = targetName, selectedHash = "", selectedDifficulty = selectedDifficulty, details = "Sorted folder has no playable selected-difficulty rows.", error = "Sorted folder has no playable selected-difficulty rows." };
        }

        MajdataPlay.SongStorage.CollectionIndex = targetIndex;
        target.Index = songIndex;
        var slide = coverListType.GetMethod("SlideListInternal", Flags);
        coverListType.GetMethod("SwitchToDirList", Flags).Invoke(coverList, new object[0]);
        if (slide != null) slide.Invoke(coverList, new object[] { targetIndex });
        fixedTicks();
        coverListType.GetMethod("SwitchToSongList", Flags).Invoke(coverList, new object[0]);
        coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { selectedDifficulty });
        if (slide != null) slide.Invoke(coverList, new object[] { songIndex });
        fixedTicks();

        var selected = coverListType.GetProperty("SelectedSong", Flags).GetValue(coverList, null) as MajdataPlay.ISongDetail;
        int selectedIndexBefore = Convert.ToInt32(coverListType.GetField("desiredListPos", Flags).GetValue(coverList));
        string carouselBefore = carouselWindow(target, selectedIndexBefore);
        Type listManagerType = Type.GetType("MajdataPlay.Scenes.List.ListManager, Assembly-CSharp", true);
        object listManager = UnityEngine.Resources.FindObjectsOfTypeAll(listManagerType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (listManager == null) {
            return new { ok = false, requested = false, collection = targetName, selectedHash = hash(selected), selectedDifficulty = selectedDifficulty, details = "ListManager not found.", error = "ListManager not found." };
        }

        listManagerType.GetMethod("EnterGame", Flags).Invoke(listManager, new object[0]);
        return new {
            ok = true,
            requested = true,
            collection = targetName,
            collectionIndex = targetIndex,
            selectedHash = hash(selected),
            selectedTitle = selected == null ? "" : selected.Title,
            selectedDifficulty = selectedDifficulty,
            carouselWindow = carouselBefore,
            details = "beforeCollection=" + targetName + "; beforeSong=" + (selected == null ? "" : selected.Title) + "/" + hash(selected) + "; selectedDifficulty=" + selectedDifficulty.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; carouselWindow=" + carouselBefore,
            error = ""
        };
    } catch (Exception ex) {
        return new { ok = false, requested = false, collection = "", selectedHash = "", selectedDifficulty = -1, details = ex.ToString(), error = ex.ToString() };
    }
})()
"@
}

function Invoke-KnownBugSortPersistenceLeaveCanary {
    Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Type gamePlayManagerType = Type.GetType("MajdataPlay.Scenes.Game.GamePlayManager, Assembly-CSharp", true);
        object gamePlayManager = UnityEngine.Resources.FindObjectsOfTypeAll(gamePlayManagerType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (gamePlayManager == null) {
            return new { ok = false, requested = false, details = "GamePlayManager not found.", error = "GamePlayManager not found." };
        }

        var endGame = gamePlayManagerType.GetMethod("EndGame", Flags);
        if (endGame == null) {
            return new { ok = false, requested = false, details = "EndGame method not found.", error = "EndGame method not found." };
        }

        endGame.Invoke(gamePlayManager, new object[] { 0, "Result" });
        return new { ok = true, requested = true, details = "EndGame(0, Result) requested.", error = "" };
    } catch (Exception ex) {
        return new { ok = false, requested = false, details = ex.ToString(), error = ex.ToString() };
    }
})()
"@
}

function Invoke-KnownBugResultReturnToListCanary {
    Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Type sceneSwitcherType = Type.GetType("MajdataPlay.SceneSwitcher, Assembly-CSharp", true);
        object switcher = UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (switcher == null) {
            return new { ok = false, requested = false, details = "SceneSwitcher not found on Result scene.", error = "SceneSwitcher not found on Result scene." };
        }

        sceneSwitcherType.GetMethod("SwitchScene", Flags).Invoke(switcher, new object[] { "List", false });
        return new { ok = true, requested = true, details = "Result scene requested SwitchScene(List, false).", error = "" };
    } catch (Exception ex) {
        return new { ok = false, requested = false, details = ex.ToString(), error = ex.ToString() };
    }
})()
"@
}

function Invoke-KnownBugSortPersistenceObserveCanary {
    param(
        [string]$BeforeCollection,
        [string]$BeforeSongHash,
        [int]$BeforeDifficulty,
        [string]$BeforeCarouselWindow = ""
    )

    $beforeCollectionLiteral = Convert-ToCSharpStringLiteral $BeforeCollection
    $beforeSongHashLiteral = Convert-ToCSharpStringLiteral $BeforeSongHash
    $beforeDifficultyLiteral = [int]$BeforeDifficulty
    $beforeCarouselWindowLiteral = Convert-ToCSharpStringLiteral $BeforeCarouselWindow

    Invoke-GameEval @"
new Func<object>(() => {
    try {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        Type bridgeType = Type.GetType("MajdataQolSongListMod.QolRuntimeBridge, MajdataQolSongListMod", false);
        object bridge = bridgeType == null ? null : bridgeType.GetProperty("Active").GetValue(null, null);
        string grouping = "";
        string sorting = "";
        if (bridge != null) {
            object runtimeSettings = bridgeType.GetField("_runtimeSettings", Flags).GetValue(bridge);
            grouping = Convert.ToString(runtimeSettings.GetType().GetProperty("Grouping").GetValue(runtimeSettings, null), System.Globalization.CultureInfo.InvariantCulture);
            sorting = Convert.ToString(runtimeSettings.GetType().GetProperty("Sorting").GetValue(runtimeSettings, null), System.Globalization.CultureInfo.InvariantCulture);
        }

        Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
        object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        if (coverList == null) {
            return new { ok = false, consistent = false, details = "CoverListDisplayer not found after returning from gameplay.", error = "CoverListDisplayer not found after returning from gameplay." };
        }

        Func<MajdataPlay.ISongDetail, string> hash = song => song == null ? "" : (song.Hash ?? "");
        Func<MajdataPlay.Collections.SongCollection, int, string> carouselWindow = (collection, centerIndex) => {
            if (collection == null || collection.Count == 0) return "";
            var rows = collection.ToArray();
            int start = Math.Max(0, centerIndex - 3);
            int end = Math.Min(rows.Length - 1, centerIndex + 3);
            return string.Join("|", Enumerable.Range(start, end - start + 1).Select(i => hash(rows[i])).ToArray());
        };
        var selectedCollection = coverListType.GetProperty("SelectedCollection", Flags).GetValue(coverList, null) as MajdataPlay.Collections.SongCollection;
        var selectedSong = coverListType.GetProperty("SelectedSong", Flags).GetValue(coverList, null) as MajdataPlay.ISongDetail;
        string mode = coverListType.GetProperty("Mode", Flags).GetValue(coverList, null).ToString();
        int selectedDifficulty = Convert.ToInt32(coverListType.GetField("selectedDifficulty", Flags).GetValue(coverList));
        int selectedIndexAfter = Convert.ToInt32(coverListType.GetField("desiredListPos", Flags).GetValue(coverList));
        string afterCarouselWindow = carouselWindow(selectedCollection, selectedIndexAfter);
        string afterCollection = selectedCollection == null ? "" : (selectedCollection.Name ?? "");
        string afterSongHash = selectedSong == null ? "" : (selectedSong.Hash ?? "");
        bool folderUnchanged = string.Equals(afterCollection, $beforeCollectionLiteral, StringComparison.OrdinalIgnoreCase);
        bool groupingUnchanged = string.Equals(grouping, "DifficultyLevel", StringComparison.OrdinalIgnoreCase);
        bool sortUnchanged = string.Equals(sorting, "Difficulty", StringComparison.OrdinalIgnoreCase);
        bool difficultyUnchanged = selectedDifficulty == $beforeDifficultyLiteral;
        bool songUnchanged = string.Equals(afterSongHash, $beforeSongHashLiteral, StringComparison.Ordinal);
        bool carouselUnchanged = string.Equals(afterCarouselWindow, $beforeCarouselWindowLiteral, StringComparison.Ordinal);
        bool stillInSongList = mode == "Chart";
        bool consistent = folderUnchanged && groupingUnchanged && sortUnchanged && difficultyUnchanged && songUnchanged && carouselUnchanged && stillInSongList;

        return new {
            ok = true,
            consistent = consistent,
            beforeCollection = $beforeCollectionLiteral,
            afterCollection = afterCollection,
            beforeSongHash = $beforeSongHashLiteral,
            afterSongHash = afterSongHash,
            beforeDifficulty = $beforeDifficultyLiteral,
            afterDifficulty = selectedDifficulty,
            grouping = grouping,
            sorting = sorting,
            mode = mode,
            beforeCarouselWindow = $beforeCarouselWindowLiteral,
            afterCarouselWindow = afterCarouselWindow,
            details = "beforeCollection=" + $beforeCollectionLiteral + "; afterCollection=" + afterCollection + "; beforeSongHash=" + $beforeSongHashLiteral + "; afterSongHash=" + afterSongHash + "; sorting=" + sorting + "; grouping=" + grouping + "; beforeDifficulty=" + $beforeDifficultyLiteral.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; afterDifficulty=" + selectedDifficulty.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; beforeCarouselWindow=" + $beforeCarouselWindowLiteral + "; afterCarouselWindow=" + afterCarouselWindow + "; mode=" + mode + "; folderUnchanged=" + folderUnchanged + "; groupingUnchanged=" + groupingUnchanged + "; sortUnchanged=" + sortUnchanged + "; difficultyUnchanged=" + difficultyUnchanged + "; songUnchanged=" + songUnchanged + "; carouselUnchanged=" + carouselUnchanged + "; stillInSongList=" + stillInSongList,
            error = ""
        };
    } catch (Exception ex) {
        return new { ok = false, consistent = false, details = ex.ToString(), error = ex.ToString() };
    }
})()
"@
}

function Add-KnownBugCase {
    param(
        [System.Collections.Generic.List[object]]$Cases,
        [System.Collections.Generic.List[object]]$Screenshots,
        [string]$Label,
        [string]$Name,
        [object]$Probe,
        [string]$ScreenshotName,
        [string]$ScreenshotDescription,
        [string]$ExpectedBehavior
    )

    Start-Sleep -Seconds 4
    $state = Read-VisibleGameState
    $shot = Capture-JportalScreenshot $Label $ScreenshotName $ScreenshotDescription $state $ExpectedBehavior
    $screenshots.Add($shot) | Out-Null
    $selected = $state.songSelect.selected.song
    $scenarioCaptured = $state.ok -and
        $state.screen.scene -eq "List" -and
        $state.screen.selecting -eq "songs" -and
        $state.songSelect.center.title -eq $selected.title
    Set-ScreenshotExpectation $shot $scenarioCaptured "stateOk=$($state.ok); scene=$($state.screen.scene); selecting=$($state.screen.selecting); selected=$($selected.title)/$($selected.hash); center=$($state.songSelect.center.title); probeOk=$($Probe.ok)" | Out-Null

    $consistent = [bool]$Probe.consistent
    if (-not [bool]$Probe.ok) {
        $consistent = $false
    }

    Add-Case $Cases $Name $consistent $Probe.details -ExpectedBehavior $ExpectedBehavior -Screenshot $shot
}

function Run-KnownBugSnapshot {
    param([string]$Label)

    $cases = New-Object 'System.Collections.Generic.List[object]'
    $screenshots = New-Object 'System.Collections.Generic.List[object]'

    Ensure-ListScene

    $playCountProbe = Convert-SerializedGameStateValue (Invoke-KnownBugPlayCountSortingCanary).result.properties
    Add-KnownBugCase -Cases $cases -Screenshots $screenshots -Label $Label `
        -Name "known bug play-count sorting orders visible next-song navigation" `
        -Probe $playCountProbe `
        -ScreenshotName "01-known-bug-play-count-sorting" `
        -ScreenshotDescription "Play-count sorting after advancing through the visible song carousel." `
        -ExpectedBehavior "When Play Count sorting is enabled on an online folder, advancing to the next song should visit songs in descending visible online play-count order for the opened folder."

    Ensure-ListScene
    $levelGroupingProbe = Convert-SerializedGameStateValue (Invoke-KnownBugLevelGroupingAllDifficultyCanary).result.properties
    Add-KnownBugCase -Cases $cases -Screenshots $screenshots -Label $Label `
        -Name "known bug level grouping includes songs from every difficulty bracket" `
        -Probe $levelGroupingProbe `
        -ScreenshotName "02-known-bug-level-grouping-all-brackets" `
        -ScreenshotDescription "A difficulty-level folder opened after building level groups." `
        -ExpectedBehavior "A level folder should include every song that has any chart in that level bucket, regardless of whether the matching chart is Easy, Basic, Advanced, Expert, Master, ReMaster, or UTAGE."

    Ensure-ListScene
    $prepare = Convert-SerializedGameStateValue (Invoke-KnownBugSortPersistencePrepareCanary).result.properties
    $postPlayProbe = $prepare
    if ([bool]$prepare.ok -and [bool]$prepare.requested) {
        Wait-ForKnownBugScene -Scene "Game" | Out-Null
        Start-Sleep -Seconds 2
        $leave = Convert-SerializedGameStateValue (Invoke-KnownBugSortPersistenceLeaveCanary).result.properties
        if ([bool]$leave.ok -and [bool]$leave.requested) {
            Wait-ForKnownBugScene -Scene "Result" | Out-Null
            Start-Sleep -Seconds 2
            $returnToList = Convert-SerializedGameStateValue (Invoke-KnownBugResultReturnToListCanary).result.properties
            if (-not ([bool]$returnToList.ok -and [bool]$returnToList.requested)) {
                $postPlayProbe = [pscustomobject]@{
                    ok = $false
                    consistent = $false
                    details = "Could not request Result-to-List return. prepare=[$($prepare.details)] leave=[$($leave.details)] return=[$($returnToList.details)]"
                }
                Ensure-ListScene
            } else {
                Wait-ForKnownBugScene -Scene "List" -RequireCoverList | Out-Null
                Start-Sleep -Milliseconds 750
                $postPlayProbe = Convert-SerializedGameStateValue (Invoke-KnownBugSortPersistenceObserveCanary -BeforeCollection $prepare.collection -BeforeSongHash $prepare.selectedHash -BeforeDifficulty ([int]$prepare.selectedDifficulty) -BeforeCarouselWindow $prepare.carouselWindow).result.properties
            }
        } else {
            $postPlayProbe = [pscustomobject]@{
                ok = $false
                consistent = $false
                details = "Could not request gameplay exit. prepare=[$($prepare.details)] leave=[$($leave.details)]"
            }
            Ensure-ListScene
        }
    } else {
        $postPlayProbe = [pscustomobject]@{
            ok = $false
            consistent = $false
            details = "Could not prepare gameplay persistence scenario. $($prepare.details)"
        }
    }
    Add-KnownBugCase -Cases $cases -Screenshots $screenshots -Label $Label `
        -Name "known bug grouping and sorting persist after entering and leaving gameplay" `
        -Probe $postPlayProbe `
        -ScreenshotName "03-known-bug-sort-persists-after-gameplay" `
        -ScreenshotDescription "Song select after entering a difficulty-sorted folder, starting gameplay, and leaving back to song select." `
        -ExpectedBehavior "After starting a song from a Difficulty Level grouped folder with Difficulty sorting and returning through the post-song result flow, song select should return to the same opened folder, same selected song, same visible carousel song window, same grouping, same sorting, and same selected difficulty."

    return [pscustomobject]@{
        Label = $Label
        Cases = $cases
        Screenshots = $screenshots
        ScreenshotDirectory = (Resolve-Path $RunScreenshotDir).Path
    }
}

function Run-KnownBugSnapshotWithSetup {
    param([string]$Label)

    Stop-Game
    & (Join-Path $PSScriptRoot "install.ps1")
    Install-Hook
    Start-GameAndOpenList
    Run-KnownBugSnapshot -Label $Label
}

function Run-JportalUiSnapshot {
    param([string]$Label)

    $cases = New-Object 'System.Collections.Generic.List[object]'
    $screenshots = New-Object 'System.Collections.Generic.List[object]'

    Invoke-JportalAction -Action OpenDefault | Out-Null
    Start-Sleep -Seconds 8
    $openDefaultState = Read-VisibleGameState
    $openDefaultShot = Capture-JportalScreenshot $Label "01-open-default" "JPORTAL opened on the default first song." $openDefaultState "JPORTAL should open to a readable song-selection state with the selected song, center cover, and song info available."
    $screenshots.Add($openDefaultShot) | Out-Null
    $scroll = Invoke-JportalAction -Action Scroll
    Start-Sleep -Seconds 8
    $afterSimpleScroll = Read-VisibleGameState
    $afterSimpleScrollShot = Capture-JportalScreenshot $Label "02-scroll-once" "JPORTAL after one song-list scroll." $afterSimpleScroll "After one song-list scroll, JPORTAL should remain readable and focused on a coherent selected song."
    $screenshots.Add($afterSimpleScrollShot) | Out-Null
    $scrollOk = [bool]$scroll.result.properties.ok
    if (-not ($afterSimpleScroll.ok -and $scrollOk)) {
        Add-Case $cases "simple JPORTAL scroll reaches readable state before difficulty regression" $false "song=$($afterSimpleScroll.songSelect.selected.song.title)/$($afterSimpleScroll.songSelect.selected.song.hash); selecting=$($afterSimpleScroll.screen.selecting); scrollOk=$scrollOk; error=$($afterSimpleScroll.error)" -Screenshot $afterSimpleScrollShot
    }

    $difficulty = Invoke-JportalAction -Action Difficulty
    Start-Sleep -Seconds 3
    $afterDifficulty = Read-VisibleGameState
    $afterDifficultyShot = Capture-JportalScreenshot $Label "03-difficulty-change" "JPORTAL after changing difficulty." $afterDifficulty "After changing difficulty, the selected carousel song, center cover, and right song info should all still describe the same song."
    $screenshots.Add($afterDifficultyShot) | Out-Null
    $afterDifficultySelectedSong = $afterDifficulty.songSelect.selected.song
    $afterDifficultyCarousel = $afterDifficulty.songSelect.carousel
    $afterDifficultySelectedElement = $afterDifficultyCarousel.selectedElement
    $afterDifficultyDisplaySong = $afterDifficultySelectedElement.display.song
    Add-Case $cases "selected carousel song stays in sync after difficulty change" (
        $afterDifficulty.ok -and $difficulty.result.properties.ok -and
        $afterDifficulty.screen.selecting -eq "songs" -and
        $afterDifficulty.songSelect.center.title -eq $afterDifficultySelectedSong.title -and
        $afterDifficulty.songSelect.center.image.matchesSelectedSongCachedCover -and
        $afterDifficultySelectedSong.hash -eq $afterDifficultySelectedElement.binding.song.hash -and
        $afterDifficultySelectedSong.hash -eq $afterDifficultyDisplaySong.hash
    ) "selected=$($afterDifficultySelectedSong.title)/$($afterDifficultySelectedSong.hash); centerTitle=$($afterDifficulty.songSelect.center.title); centerImageMatchesSelected=$($afterDifficulty.songSelect.center.image.matchesSelectedSongCachedCover); carouselIndex=$($afterDifficultyCarousel.selectedIndex); binding=$($afterDifficultySelectedElement.binding.song.title)/$($afterDifficultySelectedElement.binding.song.hash); display=$($afterDifficultyDisplaySong.title)/$($afterDifficultyDisplaySong.hash); visibleSongs=$($afterDifficultyCarousel.debug.visibleSongRows)" -Screenshot $afterDifficultyShot
    Add-Case $cases "right song info matches selected carousel song after difficulty change" (
        $afterDifficulty.ok -and
        $afterDifficulty.screen.selecting -eq "songs" -and
        $afterDifficulty.songSelect.rightInfo.title -eq $afterDifficultySelectedSong.title -and
        $afterDifficulty.songSelect.rightInfo.title -eq $afterDifficultyDisplaySong.title -and
        $afterDifficulty.songSelect.center.title -eq $afterDifficulty.songSelect.rightInfo.title
    ) "rightTitle=$($afterDifficulty.songSelect.rightInfo.title); selected=$($afterDifficultySelectedSong.title)/$($afterDifficultySelectedSong.hash); centerTitle=$($afterDifficulty.songSelect.center.title); carouselDisplay=$($afterDifficultyDisplaySong.title)/$($afterDifficultyDisplaySong.hash)" -Screenshot $afterDifficultyShot
    $scrollAfterDifficulty = Invoke-JportalAction -Action Scroll
    Start-Sleep -Seconds 8
    $afterDifficultyScroll = Read-VisibleGameState
    $afterDifficultyScrollShot = Capture-JportalScreenshot $Label "04-difficulty-change-scroll-once" "JPORTAL after difficulty change and one additional scroll." $afterDifficultyScroll "After changing difficulty and scrolling again, visible carousel covers should remain fresh, unique, and synchronized with the selected song."
    $screenshots.Add($afterDifficultyScrollShot) | Out-Null
    $difficultyOk = [bool]$difficulty.result.properties.ok
    $scrollAfterDifficultyOk = [bool]$scrollAfterDifficulty.result.properties.ok
    $afterDifficultyScrollCarousel = $afterDifficultyScroll.songSelect.carousel
    $afterDifficultyScrollSelectedSong = $afterDifficultyScroll.songSelect.selected.song
    $afterDifficultyScrollSelectedElement = $afterDifficultyScrollCarousel.selectedElement
    $afterDifficultyScrollDisplaySong = $afterDifficultyScrollSelectedElement.display.song
    $afterDifficultyScrollDiagnostics = $afterDifficultyScrollCarousel.diagnostics
    Add-Case $cases "selected carousel song stays in sync after difficulty change and scroll" (
        $afterDifficultyScroll.ok -and $scrollAfterDifficultyOk -and
        $afterDifficultyScroll.screen.selecting -eq "songs" -and
        $afterDifficultyScroll.songSelect.center.title -eq $afterDifficultyScrollSelectedSong.title -and
        $afterDifficultyScroll.songSelect.center.image.matchesSelectedSongCachedCover -and
        $afterDifficultyScrollSelectedSong.hash -eq $afterDifficultyScrollSelectedElement.binding.song.hash -and
        $afterDifficultyScrollSelectedSong.hash -eq $afterDifficultyScrollDisplaySong.hash
    ) "selected=$($afterDifficultyScrollSelectedSong.title)/$($afterDifficultyScrollSelectedSong.hash); centerTitle=$($afterDifficultyScroll.songSelect.center.title); centerImageMatchesSelected=$($afterDifficultyScroll.songSelect.center.image.matchesSelectedSongCachedCover); carouselIndex=$($afterDifficultyScrollCarousel.selectedIndex); binding=$($afterDifficultyScrollSelectedElement.binding.song.title)/$($afterDifficultyScrollSelectedElement.binding.song.hash); display=$($afterDifficultyScrollDisplaySong.title)/$($afterDifficultyScrollDisplaySong.hash); visibleSongs=$($afterDifficultyScrollCarousel.debug.visibleSongRows)" -Screenshot $afterDifficultyScrollShot
    Add-Case $cases "right song info matches selected carousel song after difficulty change and scroll" (
        $afterDifficultyScroll.ok -and
        $afterDifficultyScroll.screen.selecting -eq "songs" -and
        $afterDifficultyScroll.songSelect.rightInfo.title -eq $afterDifficultyScrollSelectedSong.title -and
        $afterDifficultyScroll.songSelect.rightInfo.title -eq $afterDifficultyScrollDisplaySong.title -and
        $afterDifficultyScroll.songSelect.center.title -eq $afterDifficultyScroll.songSelect.rightInfo.title
    ) "rightTitle=$($afterDifficultyScroll.songSelect.rightInfo.title); selected=$($afterDifficultyScrollSelectedSong.title)/$($afterDifficultyScrollSelectedSong.hash); centerTitle=$($afterDifficultyScroll.songSelect.center.title); carouselDisplay=$($afterDifficultyScrollDisplaySong.title)/$($afterDifficultyScrollDisplaySong.hash)" -Screenshot $afterDifficultyScrollShot
    Add-Case $cases "visible carousel covers are not stale or duplicated after difficulty change and scroll" (
        $afterDifficultyScroll.ok -and $difficultyOk -and $scrollAfterDifficultyOk -and
        $afterDifficultyScroll.screen.selecting -eq "songs" -and
        $afterDifficultyScrollDiagnostics.staleVisibleSprites -eq 0 -and
        $afterDifficultyScrollDiagnostics.duplicateVisibleSpriteDifferentSongs -eq 0 -and
        $afterDifficultyScrollDiagnostics.duplicateVisibleBoundHashes -eq 0 -and
        $afterDifficultyScrollDiagnostics.orphanedActiveSmallCovers -eq 0 -and
        $afterDifficultyScrollDiagnostics.staleAllActiveSprites -eq 0 -and
        $afterDifficultyScrollDiagnostics.duplicateAllActiveSpriteDifferentSongs -eq 0 -and
        $afterDifficultyScrollDiagnostics.duplicateAllActiveBoundHashes -eq 0
    ) "song=$($afterDifficultyScroll.songSelect.selected.song.title)/$($afterDifficultyScroll.songSelect.selected.song.hash); selecting=$($afterDifficultyScroll.screen.selecting); staleVisibleSprites=$($afterDifficultyScrollDiagnostics.staleVisibleSprites); duplicateVisibleSpriteDifferentSongs=$($afterDifficultyScrollDiagnostics.duplicateVisibleSpriteDifferentSongs); duplicateVisibleBoundHashes=$($afterDifficultyScrollDiagnostics.duplicateVisibleBoundHashes); orphanedActiveSmallCovers=$($afterDifficultyScrollDiagnostics.orphanedActiveSmallCovers); staleAllActiveSprites=$($afterDifficultyScrollDiagnostics.staleAllActiveSprites); duplicateAllActiveSpriteDifferentSongs=$($afterDifficultyScrollDiagnostics.duplicateAllActiveSpriteDifferentSongs); duplicateAllActiveBoundHashes=$($afterDifficultyScrollDiagnostics.duplicateAllActiveBoundHashes); difficultyOk=$difficultyOk; scrollOk=$scrollAfterDifficultyOk; visibleSongs=$($afterDifficultyScrollCarousel.debug.visibleSongRows); allActiveSongs=$($afterDifficultyScrollCarousel.debug.allActiveSongCoverRows)" -Screenshot $afterDifficultyScrollShot

    $rankPrepare = Invoke-JportalAction -Action PrepareRank
    $rankPrepareOk = [bool]$rankPrepare.result.properties.ok
    Start-Sleep -Seconds 4
    $rankOpen = Invoke-JportalAction -Action OpenRank
    Start-Sleep -Seconds 2
    $rankOpenState = Read-VisibleGameState
    $rankOpenShot = Capture-JportalScreenshot $Label "05-rank-open" "JPORTAL rank-sorted setup before changing difficulty." $rankOpenState "In a rank-sorted setup before changing difficulty, JPORTAL should remain readable and selected-song visuals should be synchronized."
    $screenshots.Add($rankOpenShot) | Out-Null
    $rankDifficulty = Invoke-JportalAction -Action DifficultyToBasic
    Start-Sleep -Seconds 4
    $rankState = Read-VisibleGameState
    $rankStateShot = Capture-JportalScreenshot $Label "06-rank-difficulty-change" "JPORTAL rank-sorted setup after changing difficulty." $rankState "In a rank-sorted setup after changing to Basic difficulty, the carousel should move focus to the expected Basic item and keep visuals synchronized."
    $screenshots.Add($rankStateShot) | Out-Null
    $rankOpenOk = [bool]$rankOpen.result.properties.ok
    $rankDifficultyOk = [bool]$rankDifficulty.result.properties.ok
    $expectedBasicIndex = if ($rankPrepare.result.properties.expectedBasicIndex -ne $null) { [int]$rankPrepare.result.properties.expectedBasicIndex } else { -1 }
    $rankSelectedSong = $rankState.songSelect.selected.song
    $rankCarousel = $rankState.songSelect.carousel
    $rankSelectedElement = $rankCarousel.selectedElement
    $rankFocusedBindingSong = $rankSelectedElement.binding.song
    $rankFocusedDisplaySong = $rankSelectedElement.display.song
    $rankCenter = $rankState.songSelect.center
    Add-Case $cases "rank-sorted carousel moves focused item after difficulty change" (
        $rankState.ok -and $rankPrepareOk -and $rankOpenOk -and $rankDifficultyOk -and
        $rankState.screen.selecting -eq "songs" -and
        $rankState.upperScreen.selectedDifficulty -eq 1 -and
        $rankCarousel.selectedIndex -eq $expectedBasicIndex -and
        $rankSelectedSong.hash -eq $rankFocusedBindingSong.hash -and
        $rankSelectedSong.hash -eq $rankFocusedDisplaySong.hash -and
        $rankCenter.title -eq $rankSelectedSong.title
    ) "expectedBasicIndex=$expectedBasicIndex; selected=$($rankSelectedSong.title)/$($rankSelectedSong.hash); selectedIndex=$($rankCarousel.selectedIndex); focusedBinding=$($rankFocusedBindingSong.title)/$($rankFocusedBindingSong.hash); focusedDisplay=$($rankFocusedDisplaySong.title)/$($rankFocusedDisplaySong.hash); centerTitle=$($rankCenter.title); selecting=$($rankState.screen.selecting); difficulty=$($rankState.upperScreen.selectedDifficulty); prepareOk=$rankPrepareOk; openOk=$rankOpenOk; difficultyOk=$rankDifficultyOk; visibleSongs=$($rankCarousel.debug.visibleSongRows)" -Screenshot $rankStateShot

    Add-QolFeatureProbeCases -Cases $cases -Screenshots $screenshots -Label $Label -ReferenceState $rankState -ReferenceScreenshot $rankStateShot
    Add-QolFeatureSpecCases -Cases $cases -Screenshots $screenshots -Label $Label -ReferenceState $rankState -ReferenceScreenshot $rankStateShot

    return [pscustomobject]@{
        Label = $Label
        Cases = $cases
        Screenshots = $screenshots
        ScreenshotDirectory = (Resolve-Path $RunScreenshotDir).Path
    }
}

function Run-Snapshot {
    param(
        [string]$Label,
        [bool]$WithQolMod
    )

    Stop-Game
    if ($WithQolMod) {
        & (Join-Path $PSScriptRoot "install.ps1")
    } else {
        Disable-QolMod
    }

    Install-Hook
    Start-GameAndOpenList
    Run-JportalUiSnapshot -Label $Label
}

function Assert-SnapshotSelfConsistent {
    param([object]$Snapshot)

    $failures = @()
    foreach ($case in $Snapshot.Cases) {
        if (-not $case.Consistent) {
            $failures += "$($Snapshot.Label): $($case.Name) failed: $($case.Details)"
        }

        if ([string]::IsNullOrWhiteSpace([string]$case.ExpectedBehavior)) {
            $failures += "$($Snapshot.Label): $($case.Name) is missing natural-language expected behavior."
        }

        if ([string]::IsNullOrWhiteSpace([string]$case.ScreenshotPath)) {
            $failures += "$($Snapshot.Label): $($case.Name) is missing a screenshot reference."
        } elseif (-not [bool]$case.ScreenshotMatchesExpected) {
            $failures += "$($Snapshot.Label): $($case.Name) screenshot did not match expected behavior: $($case.ScreenshotCheckDetails)"
        }
    }

    if ($failures.Count -gt 0) {
        if (-not [string]::IsNullOrWhiteSpace($Snapshot.ScreenshotDirectory)) {
            $failures += "$($Snapshot.Label): screenshots: $($Snapshot.ScreenshotDirectory)"
        }
        throw ($failures -join [Environment]::NewLine)
    }
}

try {
    if ($Mode -eq "KnownBugs") {
        $snapshot = Run-KnownBugSnapshotWithSetup -Label "mod-known-bugs"
        Publish-JportalArtifact -Snapshot $snapshot
        Assert-SnapshotSelfConsistent -Snapshot $snapshot
        Write-Host "Mod known-bug canaries passed: $($snapshot.Cases.Count) cases."
        exit 0
    }

    if ($Mode -eq "Vanilla") {
        $snapshot = Run-Snapshot -Label "vanilla" -WithQolMod:$false
        Publish-JportalArtifact -Snapshot $snapshot
        Assert-SnapshotSelfConsistent -Snapshot $snapshot
        Write-Host "Vanilla JPORTAL UI regressions passed: $($snapshot.Cases.Count) cases."
        exit 0
    }

    if ($Mode -eq "Mod") {
        $snapshot = Run-Snapshot -Label "mod" -WithQolMod:$true
        Publish-JportalArtifact -Snapshot $snapshot
        Assert-SnapshotSelfConsistent -Snapshot $snapshot
        Write-Host "Mod JPORTAL UI regressions passed: $($snapshot.Cases.Count) cases."
        exit 0
    }

    $vanilla = Run-Snapshot -Label "vanilla" -WithQolMod:$false
    Publish-JportalArtifact -Snapshot $vanilla
    Assert-SnapshotSelfConsistent -Snapshot $vanilla
    Write-Host "Vanilla JPORTAL UI regressions passed: $($vanilla.Cases.Count) cases."

    $mod = Run-Snapshot -Label "mod" -WithQolMod:$true
    Publish-JportalArtifact -Snapshot $mod
    Assert-SnapshotSelfConsistent -Snapshot $mod
    Write-Host "Mod JPORTAL UI regressions passed: $($mod.Cases.Count) cases."
    exit 0
} finally {
    Stop-Game
}
