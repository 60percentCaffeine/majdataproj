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

function Read-JportalState {
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

        UnityEngine.Component big = UnityEngine.Resources.FindObjectsOfTypeAll(bigType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        TMPro.TMP_Text title = big == null ? null : bigType.GetField("_title", Flags).GetValue(big) as TMPro.TMP_Text;
        TMPro.TMP_Text metadataLine = big == null ? null : big.GetComponentsInChildren<TMPro.TMP_Text>(true)
            .FirstOrDefault(t => t != null && t.gameObject != null && t.gameObject.name == "QoLSelectedSongMetadataLine");

        UnityEngine.Component analyzer = UnityEngine.Resources.FindObjectsOfTypeAll(chartAnalyzerType)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
        UnityEngine.UI.Text analyzerText = analyzer == null ? null : chartAnalyzerType.GetField("anaText", Flags).GetValue(analyzer) as UnityEngine.UI.Text;

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
        Array bindings = memory == null ? Array.Empty<object>() : (Array)memory.GetType().GetMethod("ToArray").Invoke(memory, null);
        MajdataPlay.ISongDetail focusedBinding = null;
        MajdataPlay.ISongDetail focusedDisplay = null;
        int activeSmallCovers = 0;
        int staleVisibleSprites = 0;
        int duplicateVisibleSpriteDifferentSongs = 0;
        int duplicateVisibleBoundHashes = 0;
        var spriteOwners = new Dictionary<int, string>();
        var boundHashes = new HashSet<string>(StringComparer.Ordinal);
        var assignedDisplayerIds = new HashSet<int>();
        var rows = new List<string>();

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
            rows.Add(i.ToString() + ":" + (bindingSong == null ? "" : bindingSong.Hash) + "/" + boundHash + "/sprite:" + spriteId.ToString() + "/stale:" + stale.ToString());
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

        return new {
            ok = true,
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            mode = coverListType.GetProperty("Mode", Flags).GetValue(coverList, null).ToString(),
            selectedDifficulty = Convert.ToInt32(coverListType.GetField("selectedDifficulty", Flags).GetValue(coverList)),
            collection = selectedCollection == null ? "" : selectedCollection.Name,
            collectionCount = selectedCollection == null ? 0 : selectedCollection.Count,
            desiredListPos = desired,
            selectedTitle = selectedSong == null ? "" : selectedSong.Title,
            selectedHash = selectedSong == null ? "" : selectedSong.Hash,
            centerTitle = title == null ? "" : title.text,
            qolLoaded = qolLoaded,
            metadataFound = metadataLine != null && metadataLine.gameObject.activeInHierarchy,
            metadataText = metadataLine == null ? "" : metadataLine.text,
            analyzerText = analyzerText == null ? "" : analyzerText.text,
            focusedBindingTitle = focusedBinding == null ? "" : focusedBinding.Title,
            focusedBindingHash = focusedBinding == null ? "" : focusedBinding.Hash,
            focusedDisplayTitle = focusedDisplay == null ? "" : focusedDisplay.Title,
            focusedDisplayHash = focusedDisplay == null ? "" : focusedDisplay.Hash,
            activeSmallCovers = activeSmallCovers,
            staleVisibleSprites = staleVisibleSprites,
            duplicateVisibleSpriteDifferentSongs = duplicateVisibleSpriteDifferentSongs,
            duplicateVisibleBoundHashes = duplicateVisibleBoundHashes,
            activeSmallCoverDisplayers = activeSmallCoverDisplayers,
            orphanedActiveSmallCovers = orphanedActiveSmallCovers,
            duplicateAllActiveBoundHashes = duplicateAllActiveBoundHashes,
            duplicateAllActiveSpriteDifferentSongs = duplicateAllActiveSpriteDifferentSongs,
            staleAllActiveSprites = staleAllActiveSprites,
            visibleRows = string.Join(";", rows.ToArray()),
            allActiveRows = string.Join(";", allRows.ToArray())
        };
    } catch (Exception ex) {
        return new { ok = false, error = ex.ToString() };
    }
})()
"@

    return $result.result.properties
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

function Run-JportalUiSnapshot {
    param([string]$Label)

    $cases = New-Object 'System.Collections.Generic.List[object]'
    $screenshots = New-Object 'System.Collections.Generic.List[object]'

    Invoke-JportalAction -Action OpenDefault | Out-Null
    Start-Sleep -Seconds 8
    $openDefaultState = Read-JportalState
    $screenshots.Add((Capture-JportalScreenshot $Label "01-open-default" "JPORTAL opened on the default first song." $openDefaultState)) | Out-Null
    $scroll = Invoke-JportalAction -Action Scroll
    Start-Sleep -Seconds 8
    $afterSimpleScroll = Read-JportalState
    $screenshots.Add((Capture-JportalScreenshot $Label "02-scroll-once" "JPORTAL after one song-list scroll." $afterSimpleScroll)) | Out-Null
    $scrollOk = [bool]$scroll.result.properties.ok
    if (-not ($afterSimpleScroll.ok -and $scrollOk)) {
        Add-Case $cases "simple JPORTAL scroll reaches readable state before difficulty regression" $false "song=$($afterSimpleScroll.selectedTitle)/$($afterSimpleScroll.selectedHash); scrollOk=$scrollOk; error=$($afterSimpleScroll.error)"
    }

    $difficulty = Invoke-JportalAction -Action Difficulty
    Start-Sleep -Seconds 3
    $afterDifficulty = Read-JportalState
    $screenshots.Add((Capture-JportalScreenshot $Label "03-difficulty-change" "JPORTAL after changing difficulty." $afterDifficulty)) | Out-Null
    $scrollAfterDifficulty = Invoke-JportalAction -Action Scroll
    Start-Sleep -Seconds 8
    $afterDifficultyScroll = Read-JportalState
    $screenshots.Add((Capture-JportalScreenshot $Label "04-difficulty-change-scroll-once" "JPORTAL after difficulty change and one additional scroll." $afterDifficultyScroll)) | Out-Null
    $difficultyOk = [bool]$difficulty.result.properties.ok
    $scrollAfterDifficultyOk = [bool]$scrollAfterDifficulty.result.properties.ok
    Add-Case $cases "visible carousel covers are not stale or duplicated after difficulty change and scroll" (
        $afterDifficultyScroll.ok -and $difficultyOk -and $scrollAfterDifficultyOk -and
        $afterDifficultyScroll.staleVisibleSprites -eq 0 -and
        $afterDifficultyScroll.duplicateVisibleSpriteDifferentSongs -eq 0 -and
        $afterDifficultyScroll.duplicateVisibleBoundHashes -eq 0 -and
        $afterDifficultyScroll.orphanedActiveSmallCovers -eq 0 -and
        $afterDifficultyScroll.staleAllActiveSprites -eq 0 -and
        $afterDifficultyScroll.duplicateAllActiveSpriteDifferentSongs -eq 0 -and
        $afterDifficultyScroll.duplicateAllActiveBoundHashes -eq 0
    ) "song=$($afterDifficultyScroll.selectedTitle)/$($afterDifficultyScroll.selectedHash); staleVisibleSprites=$($afterDifficultyScroll.staleVisibleSprites); duplicateVisibleSpriteDifferentSongs=$($afterDifficultyScroll.duplicateVisibleSpriteDifferentSongs); duplicateVisibleBoundHashes=$($afterDifficultyScroll.duplicateVisibleBoundHashes); orphanedActiveSmallCovers=$($afterDifficultyScroll.orphanedActiveSmallCovers); staleAllActiveSprites=$($afterDifficultyScroll.staleAllActiveSprites); duplicateAllActiveSpriteDifferentSongs=$($afterDifficultyScroll.duplicateAllActiveSpriteDifferentSongs); duplicateAllActiveBoundHashes=$($afterDifficultyScroll.duplicateAllActiveBoundHashes); difficultyOk=$difficultyOk; scrollOk=$scrollAfterDifficultyOk; rows=$($afterDifficultyScroll.visibleRows); allRows=$($afterDifficultyScroll.allActiveRows)"

    $rankPrepare = Invoke-JportalAction -Action PrepareRank
    $rankPrepareOk = [bool]$rankPrepare.result.properties.ok
    Start-Sleep -Seconds 4
    $rankOpen = Invoke-JportalAction -Action OpenRank
    Start-Sleep -Seconds 2
    $rankOpenState = Read-JportalState
    $screenshots.Add((Capture-JportalScreenshot $Label "05-rank-open" "JPORTAL rank-sorted setup before changing difficulty." $rankOpenState)) | Out-Null
    $rankDifficulty = Invoke-JportalAction -Action DifficultyToBasic
    Start-Sleep -Seconds 4
    $rankState = Read-JportalState
    $screenshots.Add((Capture-JportalScreenshot $Label "06-rank-difficulty-change" "JPORTAL rank-sorted setup after changing difficulty." $rankState)) | Out-Null
    $rankOpenOk = [bool]$rankOpen.result.properties.ok
    $rankDifficultyOk = [bool]$rankDifficulty.result.properties.ok
    $expectedBasicIndex = if ($rankPrepare.result.properties.expectedBasicIndex -ne $null) { [int]$rankPrepare.result.properties.expectedBasicIndex } else { -1 }
    Add-Case $cases "rank-sorted carousel moves focused item after difficulty change" (
        $rankState.ok -and $rankPrepareOk -and $rankOpenOk -and $rankDifficultyOk -and
        $rankState.selectedDifficulty -eq 1 -and
        $rankState.desiredListPos -eq $expectedBasicIndex -and
        $rankState.selectedHash -eq $rankState.focusedBindingHash -and
        $rankState.selectedHash -eq $rankState.focusedDisplayHash -and
        $rankState.centerTitle -eq $rankState.selectedTitle
    ) "expectedBasicIndex=$expectedBasicIndex; selected=$($rankState.selectedTitle)/$($rankState.selectedHash); desired=$($rankState.desiredListPos); focusedBinding=$($rankState.focusedBindingTitle)/$($rankState.focusedBindingHash); focusedDisplay=$($rankState.focusedDisplayTitle)/$($rankState.focusedDisplayHash); centerTitle=$($rankState.centerTitle); prepareOk=$rankPrepareOk; openOk=$rankOpenOk; difficultyOk=$rankDifficultyOk; rows=$($rankState.visibleRows)"

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
