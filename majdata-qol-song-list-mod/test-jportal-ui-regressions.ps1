param(
    [ValidateSet("Compare", "Vanilla", "Mod")]
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
        [object]$State
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
        [string]$Details
    )

    $Cases.Add([pscustomobject]@{
        Name = $Name
        Consistent = $Consistent
        Details = $Details
    }) | Out-Null
}

function Invoke-QolFeatureProbe {
    param(
        [string]$Feature,
        [string]$Grouping,
        [string]$Sorting,
        [string]$DifficultyFilter,
        [string]$DownloadedFilter,
        [string]$TargetCollection,
        [bool]$InstallWebsiteCollection = $false
    )

    $featureLiteral = Convert-ToCSharpStringLiteral $Feature
    $groupingLiteral = Convert-ToCSharpStringLiteral $Grouping
    $sortingLiteral = Convert-ToCSharpStringLiteral $Sorting
    $difficultyFilterLiteral = Convert-ToCSharpStringLiteral $DifficultyFilter
    $downloadedFilterLiteral = Convert-ToCSharpStringLiteral $DownloadedFilter
    $targetCollectionLiteral = Convert-ToCSharpStringLiteral $TargetCollection
    $installWebsiteLiteral = if ($InstallWebsiteCollection) { "true" } else { "false" }

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
        coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(coverList, new object[] { 0 });
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
        [object]$ReferenceState
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
            Add-Case $Cases "feature probe $($probe.Feature) keeps visible state coherent" $true "skipped: QoL bridge is not loaded in $Label."
        }
        return
    }

    foreach ($probe in $probes) {
        $action = Invoke-QolFeatureProbe -Feature $probe.Feature -Grouping $probe.Grouping -Sorting $probe.Sorting -DifficultyFilter $probe.DifficultyFilter -DownloadedFilter $probe.DownloadedFilter -TargetCollection $probe.Target -InstallWebsiteCollection:$probe.InstallWebsite
        Start-Sleep -Seconds 5
        $state = Read-VisibleGameState
        $screenshots.Add((Capture-JportalScreenshot $Label $probe.Screenshot "QoL feature probe: $($probe.Feature)." $state)) | Out-Null

        $actionOk = [bool]$action.result.properties.ok
        $selectedSong = $state.songSelect.selected.song
        $carousel = $state.songSelect.carousel
        $selectedElement = $carousel.selectedElement
        $bindingSong = $selectedElement.binding.song
        $displaySong = $selectedElement.display.song
        $diagnostics = $carousel.diagnostics
        Add-Case $Cases "feature probe $($probe.Feature) keeps visible state coherent" (
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
        ) "feature=$($probe.Feature); actionOk=$actionOk; collection=$($action.result.properties.collection); selected=$($selectedSong.title)/$($selectedSong.hash); center=$($state.songSelect.center.title); right=$($state.songSelect.rightInfo.title); binding=$($bindingSong.title)/$($bindingSong.hash); display=$($displaySong.title)/$($displaySong.hash); orphaned=$($diagnostics.orphanedActiveSmallCovers); duplicateAllBound=$($diagnostics.duplicateAllActiveBoundHashes); staleAll=$($diagnostics.staleAllActiveSprites); duplicateAllSprites=$($diagnostics.duplicateAllActiveSpriteDifferentSongs); error=$($action.result.properties.error)"
    }
}

function Run-JportalUiSnapshot {
    param([string]$Label)

    $cases = New-Object 'System.Collections.Generic.List[object]'
    $screenshots = New-Object 'System.Collections.Generic.List[object]'

    Invoke-JportalAction -Action OpenDefault | Out-Null
    Start-Sleep -Seconds 8
    $openDefaultState = Read-VisibleGameState
    $screenshots.Add((Capture-JportalScreenshot $Label "01-open-default" "JPORTAL opened on the default first song." $openDefaultState)) | Out-Null
    $scroll = Invoke-JportalAction -Action Scroll
    Start-Sleep -Seconds 8
    $afterSimpleScroll = Read-VisibleGameState
    $screenshots.Add((Capture-JportalScreenshot $Label "02-scroll-once" "JPORTAL after one song-list scroll." $afterSimpleScroll)) | Out-Null
    $scrollOk = [bool]$scroll.result.properties.ok
    if (-not ($afterSimpleScroll.ok -and $scrollOk)) {
        Add-Case $cases "simple JPORTAL scroll reaches readable state before difficulty regression" $false "song=$($afterSimpleScroll.songSelect.selected.song.title)/$($afterSimpleScroll.songSelect.selected.song.hash); selecting=$($afterSimpleScroll.screen.selecting); scrollOk=$scrollOk; error=$($afterSimpleScroll.error)"
    }

    $difficulty = Invoke-JportalAction -Action Difficulty
    Start-Sleep -Seconds 3
    $afterDifficulty = Read-VisibleGameState
    $screenshots.Add((Capture-JportalScreenshot $Label "03-difficulty-change" "JPORTAL after changing difficulty." $afterDifficulty)) | Out-Null
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
    ) "selected=$($afterDifficultySelectedSong.title)/$($afterDifficultySelectedSong.hash); centerTitle=$($afterDifficulty.songSelect.center.title); centerImageMatchesSelected=$($afterDifficulty.songSelect.center.image.matchesSelectedSongCachedCover); carouselIndex=$($afterDifficultyCarousel.selectedIndex); binding=$($afterDifficultySelectedElement.binding.song.title)/$($afterDifficultySelectedElement.binding.song.hash); display=$($afterDifficultyDisplaySong.title)/$($afterDifficultyDisplaySong.hash); visibleSongs=$($afterDifficultyCarousel.debug.visibleSongRows)"
    Add-Case $cases "right song info matches selected carousel song after difficulty change" (
        $afterDifficulty.ok -and
        $afterDifficulty.screen.selecting -eq "songs" -and
        $afterDifficulty.songSelect.rightInfo.title -eq $afterDifficultySelectedSong.title -and
        $afterDifficulty.songSelect.rightInfo.title -eq $afterDifficultyDisplaySong.title -and
        $afterDifficulty.songSelect.center.title -eq $afterDifficulty.songSelect.rightInfo.title
    ) "rightTitle=$($afterDifficulty.songSelect.rightInfo.title); selected=$($afterDifficultySelectedSong.title)/$($afterDifficultySelectedSong.hash); centerTitle=$($afterDifficulty.songSelect.center.title); carouselDisplay=$($afterDifficultyDisplaySong.title)/$($afterDifficultyDisplaySong.hash)"
    $scrollAfterDifficulty = Invoke-JportalAction -Action Scroll
    Start-Sleep -Seconds 8
    $afterDifficultyScroll = Read-VisibleGameState
    $screenshots.Add((Capture-JportalScreenshot $Label "04-difficulty-change-scroll-once" "JPORTAL after difficulty change and one additional scroll." $afterDifficultyScroll)) | Out-Null
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
    ) "selected=$($afterDifficultyScrollSelectedSong.title)/$($afterDifficultyScrollSelectedSong.hash); centerTitle=$($afterDifficultyScroll.songSelect.center.title); centerImageMatchesSelected=$($afterDifficultyScroll.songSelect.center.image.matchesSelectedSongCachedCover); carouselIndex=$($afterDifficultyScrollCarousel.selectedIndex); binding=$($afterDifficultyScrollSelectedElement.binding.song.title)/$($afterDifficultyScrollSelectedElement.binding.song.hash); display=$($afterDifficultyScrollDisplaySong.title)/$($afterDifficultyScrollDisplaySong.hash); visibleSongs=$($afterDifficultyScrollCarousel.debug.visibleSongRows)"
    Add-Case $cases "right song info matches selected carousel song after difficulty change and scroll" (
        $afterDifficultyScroll.ok -and
        $afterDifficultyScroll.screen.selecting -eq "songs" -and
        $afterDifficultyScroll.songSelect.rightInfo.title -eq $afterDifficultyScrollSelectedSong.title -and
        $afterDifficultyScroll.songSelect.rightInfo.title -eq $afterDifficultyScrollDisplaySong.title -and
        $afterDifficultyScroll.songSelect.center.title -eq $afterDifficultyScroll.songSelect.rightInfo.title
    ) "rightTitle=$($afterDifficultyScroll.songSelect.rightInfo.title); selected=$($afterDifficultyScrollSelectedSong.title)/$($afterDifficultyScrollSelectedSong.hash); centerTitle=$($afterDifficultyScroll.songSelect.center.title); carouselDisplay=$($afterDifficultyScrollDisplaySong.title)/$($afterDifficultyScrollDisplaySong.hash)"
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
    ) "song=$($afterDifficultyScroll.songSelect.selected.song.title)/$($afterDifficultyScroll.songSelect.selected.song.hash); selecting=$($afterDifficultyScroll.screen.selecting); staleVisibleSprites=$($afterDifficultyScrollDiagnostics.staleVisibleSprites); duplicateVisibleSpriteDifferentSongs=$($afterDifficultyScrollDiagnostics.duplicateVisibleSpriteDifferentSongs); duplicateVisibleBoundHashes=$($afterDifficultyScrollDiagnostics.duplicateVisibleBoundHashes); orphanedActiveSmallCovers=$($afterDifficultyScrollDiagnostics.orphanedActiveSmallCovers); staleAllActiveSprites=$($afterDifficultyScrollDiagnostics.staleAllActiveSprites); duplicateAllActiveSpriteDifferentSongs=$($afterDifficultyScrollDiagnostics.duplicateAllActiveSpriteDifferentSongs); duplicateAllActiveBoundHashes=$($afterDifficultyScrollDiagnostics.duplicateAllActiveBoundHashes); difficultyOk=$difficultyOk; scrollOk=$scrollAfterDifficultyOk; visibleSongs=$($afterDifficultyScrollCarousel.debug.visibleSongRows); allActiveSongs=$($afterDifficultyScrollCarousel.debug.allActiveSongCoverRows)"

    $rankPrepare = Invoke-JportalAction -Action PrepareRank
    $rankPrepareOk = [bool]$rankPrepare.result.properties.ok
    Start-Sleep -Seconds 4
    $rankOpen = Invoke-JportalAction -Action OpenRank
    Start-Sleep -Seconds 2
    $rankOpenState = Read-VisibleGameState
    $screenshots.Add((Capture-JportalScreenshot $Label "05-rank-open" "JPORTAL rank-sorted setup before changing difficulty." $rankOpenState)) | Out-Null
    $rankDifficulty = Invoke-JportalAction -Action DifficultyToBasic
    Start-Sleep -Seconds 4
    $rankState = Read-VisibleGameState
    $screenshots.Add((Capture-JportalScreenshot $Label "06-rank-difficulty-change" "JPORTAL rank-sorted setup after changing difficulty." $rankState)) | Out-Null
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
    ) "expectedBasicIndex=$expectedBasicIndex; selected=$($rankSelectedSong.title)/$($rankSelectedSong.hash); selectedIndex=$($rankCarousel.selectedIndex); focusedBinding=$($rankFocusedBindingSong.title)/$($rankFocusedBindingSong.hash); focusedDisplay=$($rankFocusedDisplaySong.title)/$($rankFocusedDisplaySong.hash); centerTitle=$($rankCenter.title); selecting=$($rankState.screen.selecting); difficulty=$($rankState.upperScreen.selectedDifficulty); prepareOk=$rankPrepareOk; openOk=$rankOpenOk; difficultyOk=$rankDifficultyOk; visibleSongs=$($rankCarousel.debug.visibleSongRows)"

    Add-QolFeatureProbeCases -Cases $cases -Screenshots $screenshots -Label $Label -ReferenceState $rankState

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
    }

    if ($failures.Count -gt 0) {
        if (-not [string]::IsNullOrWhiteSpace($Snapshot.ScreenshotDirectory)) {
            $failures += "$($Snapshot.Label): screenshots: $($Snapshot.ScreenshotDirectory)"
        }
        throw ($failures -join [Environment]::NewLine)
    }
}

try {
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
