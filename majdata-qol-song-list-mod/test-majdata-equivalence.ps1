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

function Stop-Game {
    Get-Process -Name "MajdataPlay" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

function Invoke-GameEval {
    param([string]$Code)

    $body = @{
        code = $Code
        timeoutMs = 120000
        maxDepth = 12
        maxResponseBytes = 2000000
    } | ConvertTo-Json -Compress

    Invoke-RestMethod -Uri $HookUrl -Method Post -ContentType application/json -Body $body -TimeoutSec 150
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

function ConvertTo-CSharpStringLiteral {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    $escaped = $Value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '\r').Replace("`n", '\n').Replace("`t", '\t')
    return '"' + $escaped + '"'
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

function Wait-ForListScene {
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

    Wait-ForListScene
}

function Set-BuiltinSortAndReloadList {
    param([string]$SortTypeName)

    $sortLiteral = ConvertTo-CSharpStringLiteral $SortTypeName
    Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags InstanceFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    MajdataPlay.SortType sortType = (MajdataPlay.SortType)Enum.Parse(typeof(MajdataPlay.SortType), $sortLiteral);
    MajdataPlay.SongStorage.OrderBy.Keyword = "";
    MajdataPlay.SongStorage.OrderBy.SortBy = sortType;

    Type sceneSwitcherType = Type.GetType("MajdataPlay.SceneSwitcher, Assembly-CSharp", true);
    object switcher = UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (switcher != null) {
        sceneSwitcherType.GetMethod("SwitchScene", InstanceFlags).Invoke(switcher, new object[] { "List", true });
    }

    return new {
        sort = MajdataPlay.SongStorage.OrderBy.SortBy.ToString(),
        requested = switcher != null
    };
})()
"@ | Out-Null

    Wait-ForListScene
    Start-Sleep -Seconds 4
}

function Read-ActiveEquivalenceSnapshot {
    param(
        [string]$Label,
        [string]$CasePrefix = ""
    )

    $casePrefixLiteral = ConvertTo-CSharpStringLiteral $CasePrefix
    $result = Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.Static;

    var lines = new List<string>();
    string casePrefix = $casePrefixLiteral;
    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    Type bigType = Type.GetType("MajdataPlay.Scenes.List.CoverBigDisplayer, Assembly-CSharp", true);
    HashSet<string> customNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "All",
        "MyFavorites",
        "Random Recommended"
    };

    Func<string, string> clean = value => (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    Func<string, string> enc = value => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value ?? ""));
    Func<MajdataPlay.Collections.SongCollection, bool> isRealCollection = collection => {
        if (collection == null || collection.Count <= 0) return false;
        string name = collection.Name ?? "";
        if (customNames.Contains(name)) return false;
        if (name.StartsWith("QoL ", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    };

    Action<string, string> addLine = (name, value) =>
        lines.Add("CASE\t" + clean(casePrefix + name) + "\t" + value);

    Func<UnityEngine.Component> coverList = () => UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);

    Func<UnityEngine.Component> big = () => UnityEngine.Resources.FindObjectsOfTypeAll(bigType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);

    Func<object, MajdataPlay.ISongDetail> selectedSong = cl =>
        cl == null ? null : coverListType.GetProperty("SelectedSong", Flags).GetValue(cl, null) as MajdataPlay.ISongDetail;

    Func<object, MajdataPlay.Collections.SongCollection> currentCollection = cl =>
        cl == null ? null : coverListType.GetField("_currentCollection", Flags).GetValue(cl) as MajdataPlay.Collections.SongCollection;

    Func<object, int> desiredPosition = cl => {
        if (cl == null) return -1;
        object value = coverListType.GetField("desiredListPos", Flags).GetValue(cl);
        return value == null ? -1 : Convert.ToInt32(value);
    };

    Func<object, MajdataPlay.ISongDetail> bindingSongAtDesired = cl => {
        if (cl == null) return null;
        object memory = coverListType.GetField("_songDetailBindings", Flags).GetValue(cl);
        if (memory == null) return null;
        int length = (int)memory.GetType().GetProperty("Length").GetValue(memory, null);
        if (length == 0) return null;
        int index = desiredPosition(cl);
        if (index < 0) index = 0;
        if (index >= length) index = length - 1;
        Array array = (Array)memory.GetType().GetMethod("ToArray").Invoke(memory, null);
        object binding = array.GetValue(index);
        return binding == null ? null : binding.GetType().GetProperty("SongDetail").GetValue(binding, null) as MajdataPlay.ISongDetail;
    };

    Func<object, MajdataPlay.ISongDetail> displayedSongAtDesired = cl => {
        if (cl == null) return null;
        object memory = coverListType.GetField("_songDetailBindings", Flags).GetValue(cl);
        if (memory == null) return null;
        int length = (int)memory.GetType().GetProperty("Length").GetValue(memory, null);
        if (length == 0) return null;
        int index = desiredPosition(cl);
        if (index < 0) index = 0;
        if (index >= length) index = length - 1;
        Array array = (Array)memory.GetType().GetMethod("ToArray").Invoke(memory, null);
        object binding = array.GetValue(index);
        if (binding == null) return null;
        object displayer = binding.GetType().GetProperty("Displayer").GetValue(binding, null);
        if (displayer == null) return null;
        var boundSongField = displayer.GetType().GetField("_boundSong", Flags);
        return boundSongField == null ? null : boundSongField.GetValue(displayer) as MajdataPlay.ISongDetail;
    };

    Func<string> centerTitle = () => {
        var component = big();
        if (component == null) return "";
        var title = bigType.GetField("_title", Flags).GetValue(component) as TMPro.TMP_Text;
        return title == null ? "" : title.text;
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

    Func<object, UnityEngine.UI.Image> smallCoverImage = displayer => {
        if (displayer == null) return null;
        var field = displayer.GetType().GetField("_songCover", Flags);
        return field == null ? null : field.GetValue(displayer) as UnityEngine.UI.Image;
    };

    Func<UnityEngine.Component, UnityEngine.UI.Image> bigCoverImage = component => {
        if (component == null) return null;
        var field = bigType.GetField("_cover", Flags);
        return field == null ? null : field.GetValue(component) as UnityEngine.UI.Image;
    };

    Func<UnityEngine.Sprite, string> spriteName = sprite => sprite == null ? "" : (sprite.name ?? "");

    Func<object, string> selectedSpriteFingerprint = cl => {
        var selected = selectedSong(cl);
        var component = big();
        var image = bigCoverImage(component);
        var cached = cachedCover(selected);
        bool stale = image != null && image.sprite != null && cached != null && !object.ReferenceEquals(image.sprite, cached);
        return "bigSprite=" + enc(image == null ? "" : spriteName(image.sprite)) +
            "|bigCachedSprite=" + enc(spriteName(cached)) +
            "|bigSpriteStale=" + stale.ToString();
    };

    Func<object, string> desiredSmallSpriteFingerprint = cl => {
        if (cl == null) return "smallSprite=|smallCachedSprite=|smallSpriteStale=False";
        object memory = coverListType.GetField("_songDetailBindings", Flags).GetValue(cl);
        if (memory == null) return "smallSprite=|smallCachedSprite=|smallSpriteStale=False";
        int length = (int)memory.GetType().GetProperty("Length").GetValue(memory, null);
        if (length == 0) return "smallSprite=|smallCachedSprite=|smallSpriteStale=False";
        int index = desiredPosition(cl);
        if (index < 0) index = 0;
        if (index >= length) index = length - 1;
        Array array = (Array)memory.GetType().GetMethod("ToArray").Invoke(memory, null);
        object binding = array.GetValue(index);
        if (binding == null) return "smallSprite=|smallCachedSprite=|smallSpriteStale=False";
        var song = binding.GetType().GetProperty("SongDetail").GetValue(binding, null) as MajdataPlay.ISongDetail;
        object displayer = binding.GetType().GetProperty("Displayer").GetValue(binding, null);
        var image = smallCoverImage(displayer);
        var cached = cachedCover(song);
        bool stale = image != null && image.sprite != null && cached != null && !object.ReferenceEquals(image.sprite, cached);
        return "smallSprite=" + enc(image == null ? "" : spriteName(image.sprite)) +
            "|smallCachedSprite=" + enc(spriteName(cached)) +
            "|smallSpriteStale=" + stale.ToString();
    };

    Func<object, int> staleVisibleSmallSpriteCount = cl => {
        if (cl == null) return 0;
        object memory = coverListType.GetField("_songDetailBindings", Flags).GetValue(cl);
        if (memory == null) return 0;
        Array array = (Array)memory.GetType().GetMethod("ToArray").Invoke(memory, null);
        int stale = 0;
        foreach (object binding in array) {
            if (binding == null) continue;
            var song = binding.GetType().GetProperty("SongDetail").GetValue(binding, null) as MajdataPlay.ISongDetail;
            object displayer = binding.GetType().GetProperty("Displayer").GetValue(binding, null);
            var component = displayer as UnityEngine.Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) continue;
            var image = smallCoverImage(displayer);
            var cached = cachedCover(song);
            if (image != null && image.sprite != null && cached != null && !object.ReferenceEquals(image.sprite, cached)) {
                stale++;
            }
        }
        return stale;
    };

    Action<string> appendCase = name => {
        var cl = coverList();
        var selected = selectedSong(cl);
        var current = currentCollection(cl);
        var binding = bindingSongAtDesired(cl);
        var displayed = displayedSongAtDesired(cl);
        string selectedHash = selected == null ? "" : selected.Hash;
        string currentHash = current == null || current.Count == 0 ? "" : current.Current.Hash;
        string workingHash = MajdataPlay.SongStorage.WorkingCollection == null || MajdataPlay.SongStorage.WorkingCollection.Count == 0 ? "" : MajdataPlay.SongStorage.WorkingCollection.Current.Hash;
        string bindingHash = binding == null ? "" : binding.Hash;
        string displayedHash = displayed == null ? "" : displayed.Hash;
        string title = centerTitle();
        string selectedSprite = selectedSpriteFingerprint(cl);
        string desiredSmallSprite = desiredSmallSpriteFingerprint(cl);
        int staleSmallSprites = staleVisibleSmallSpriteCount(cl);
        bool staleBigSprite = selectedSprite.Contains("bigSpriteStale=True");
        bool staleDesiredSmallSprite = desiredSmallSprite.Contains("smallSpriteStale=True");
        bool consistent = !string.IsNullOrWhiteSpace(selectedHash) &&
            selectedHash == currentHash &&
            selectedHash == workingHash &&
            selectedHash == bindingHash &&
            selectedHash == displayedHash &&
            selected != null &&
            title == selected.Title &&
            !staleBigSprite &&
            !staleDesiredSmallSprite &&
            staleSmallSprites == 0;

        string fingerprint = "consistent=" + consistent +
            "|collection=" + enc(current == null ? "" : current.Name) +
            "|desired=" + desiredPosition(cl).ToString() +
            "|selectedTitle=" + enc(selected == null ? "" : selected.Title) +
            "|selectedHash=" + selectedHash +
            "|currentTitle=" + enc(current == null || current.Count == 0 ? "" : current.Current.Title) +
            "|currentHash=" + currentHash +
            "|workingHash=" + workingHash +
            "|bindingTitle=" + enc(binding == null ? "" : binding.Title) +
            "|bindingHash=" + bindingHash +
            "|displayedTitle=" + enc(displayed == null ? "" : displayed.Title) +
            "|displayedHash=" + displayedHash +
            "|centerTitle=" + enc(title) +
            "|" + selectedSprite +
            "|" + desiredSmallSprite +
            "|staleVisibleSmallSprites=" + staleSmallSprites.ToString();
        addLine(name, fingerprint);
    };

    Action<string, int> openCollection = (collectionName, songIndex) => {
        var collections = MajdataPlay.SongStorage.Collections;
        int collectionIndex = Array.FindIndex(collections, c => c != null && string.Equals(c.Name, collectionName, StringComparison.Ordinal));
        if (collectionIndex < 0) throw new InvalidOperationException("Collection not found: " + collectionName);

        MajdataPlay.SongStorage.CollectionIndex = collectionIndex;
        if (collections[collectionIndex].Count > 0) {
            collections[collectionIndex].Index = Math.Max(0, Math.Min(songIndex, collections[collectionIndex].Count - 1));
        }

        var cl = coverList();
        if (cl == null) throw new InvalidOperationException("CoverListDisplayer was not found.");
        coverListType.GetMethod("SwitchToDirList", Flags).Invoke(cl, new object[0]);
        var slide = coverListType.GetMethod("SlideList", Flags);
        int currentDir = desiredPosition(cl);
        slide.Invoke(cl, new object[] { collectionIndex - currentDir });
        coverListType.GetMethod("SwitchToSongList", Flags).Invoke(cl, new object[0]);
        int currentSong = desiredPosition(cl);
        slide.Invoke(cl, new object[] { songIndex - currentSong });
        var fixedUpdate = coverListType.GetMethod("FixedUpdate", Flags);
        if (fixedUpdate != null) {
            for (int i = 0; i < 10; i++) {
                fixedUpdate.Invoke(cl, new object[0]);
            }
        }
    };

    Func<MajdataPlay.ISongDetail, string> hash = song => song == null ? "" : (song.Hash ?? "");
    Func<MajdataPlay.ISongDetail, string> level4 = song => {
        if (song == null) return "";
        ReadOnlySpan<string> levels = song.Levels;
        return levels.Length > 4 ? (levels[4] ?? "") : "";
    };
    Func<MajdataPlay.ISongDetail, string> designer4 = song => {
        if (song == null) return "";
        ReadOnlySpan<string> designers = song.Designers;
        return designers.Length > 4 ? (designers[4] ?? "") : "";
    };
    Func<IEnumerable<MajdataPlay.ISongDetail>, string> hashList = songs =>
        string.Join(",", (songs ?? Enumerable.Empty<MajdataPlay.ISongDetail>()).Select(song => hash(song)).ToArray());
    Func<MajdataPlay.ISongDetail[], MajdataPlay.SortType, MajdataPlay.ISongDetail[]> expectedSort = (origin, sortType) => {
        IEnumerable<MajdataPlay.ISongDetail> rows = origin ?? new MajdataPlay.ISongDetail[0];
        switch (sortType) {
            case MajdataPlay.SortType.ByTime:
                return System.Linq.Enumerable.OrderByDescending<MajdataPlay.ISongDetail, DateTime>(rows, song => song == null ? DateTime.MinValue : song.Timestamp).ToArray();
            case MajdataPlay.SortType.ByDiff:
                return System.Linq.Enumerable.OrderByDescending<MajdataPlay.ISongDetail, string>(rows, song => level4(song)).ToArray();
            case MajdataPlay.SortType.ByDes:
                return System.Linq.Enumerable.OrderBy<MajdataPlay.ISongDetail, string>(rows, song => designer4(song)).ToArray();
            case MajdataPlay.SortType.ByTitle:
                return System.Linq.Enumerable.OrderBy<MajdataPlay.ISongDetail, string>(rows, song => song == null ? "" : (song.Title ?? "")).ToArray();
            default:
                return rows.ToArray();
        }
    };
    Func<object, MajdataPlay.ISongDetail[]> bindingSongs = cl => {
        if (cl == null) return new MajdataPlay.ISongDetail[0];
        object memory = coverListType.GetField("_songDetailBindings", Flags).GetValue(cl);
        if (memory == null) return new MajdataPlay.ISongDetail[0];
        Array array = (Array)memory.GetType().GetMethod("ToArray").Invoke(memory, null);
        return array.Cast<object>()
            .Where(binding => binding != null)
            .Select(binding => binding.GetType().GetProperty("SongDetail").GetValue(binding, null) as MajdataPlay.ISongDetail)
            .Where(song => song != null)
            .ToArray();
    };
    Func<string[], string[], bool> sameHashes = (left, right) => {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
        }
        return true;
    };

    Action appendSortInvariant = () => {
        MajdataPlay.SortType sortMode = MajdataPlay.SongStorage.OrderBy.SortBy;
        var candidates = MajdataPlay.SongStorage.Collections
            .Where(isRealCollection)
            .Where(collection => collection.Count >= 5)
            .ToArray();

        MajdataPlay.Collections.SongCollection candidate = null;
        MajdataPlay.ISongDetail[] expectedRows = new MajdataPlay.ISongDetail[0];
        foreach (var collection in candidates) {
            var rawRows = collection.ToArray();
            var sortedRows = expectedSort(rawRows, sortMode);
            int count = Math.Min(8, Math.Min(rawRows.Length, sortedRows.Length));
            bool differs = !sameHashes(rawRows.Take(count).Select(hash).ToArray(), sortedRows.Take(count).Select(hash).ToArray());
            if (sortMode == MajdataPlay.SortType.Default || differs) {
                candidate = collection;
                expectedRows = sortedRows;
                break;
            }
        }

        if (candidate == null) {
            candidate = candidates.FirstOrDefault();
            expectedRows = candidate == null ? new MajdataPlay.ISongDetail[0] : expectedSort(candidate.ToArray(), sortMode);
        }

        if (candidate == null || expectedRows.Length == 0) {
            addLine("built-in sort invariant " + sortMode.ToString(), "consistent=True|skipped=True|reason=" + enc("No real collection with at least five songs was available."));
            return;
        }

        openCollection(candidate.Name, 0);
        var cl = coverList();
        var current = currentCollection(cl);
        var actualRows = current == null ? new MajdataPlay.ISongDetail[0] : current.ToArray();
        var boundRows = bindingSongs(cl);
        int comparisonCount = Math.Min(8, Math.Min(expectedRows.Length, actualRows.Length));
        string[] expectedHashes = expectedRows.Take(comparisonCount).Select(hash).ToArray();
        string[] actualHashes = actualRows.Take(comparisonCount).Select(hash).ToArray();
        string[] bindingHashes = boundRows.Take(comparisonCount).Select(hash).ToArray();
        bool orderMatches = sameHashes(expectedHashes, actualHashes);
        bool bindingMatches = sameHashes(expectedHashes.Take(bindingHashes.Length).ToArray(), bindingHashes);

        var selectionMismatches = new List<string>();
        foreach (int index in new[] { 0, 1, 2, 4 }) {
            if (index >= expectedRows.Length) continue;
            openCollection(candidate.Name, index);
            var selected = selectedSong(coverList());
            string expectedHash = hash(expectedRows[index]);
            string selectedHash = hash(selected);
            if (!string.Equals(expectedHash, selectedHash, StringComparison.Ordinal)) {
                selectionMismatches.Add(index.ToString() + ":" + expectedHash + "!=" + selectedHash);
            }
        }

        cl = coverList();
        string selectedSprite = selectedSpriteFingerprint(cl);
        string desiredSmallSprite = desiredSmallSpriteFingerprint(cl);
        int staleSmallSprites = staleVisibleSmallSpriteCount(cl);
        bool spriteMatches = !selectedSprite.Contains("bigSpriteStale=True") &&
            !desiredSmallSprite.Contains("smallSpriteStale=True") &&
            staleSmallSprites == 0;
        bool consistent = orderMatches && bindingMatches && selectionMismatches.Count == 0 && spriteMatches;

        string fingerprint = "consistent=" + consistent.ToString() +
            "|sortMode=" + sortMode.ToString() +
            "|collection=" + enc(candidate.Name) +
            "|expectedFirstHashes=" + string.Join(",", expectedHashes) +
            "|actualFirstHashes=" + string.Join(",", actualHashes) +
            "|bindingFirstHashes=" + string.Join(",", bindingHashes) +
            "|selectionMismatches=" + string.Join(",", selectionMismatches.ToArray()) +
            "|" + selectedSprite +
            "|" + desiredSmallSprite +
            "|staleVisibleSmallSprites=" + staleSmallSprites.ToString();
        addLine("built-in sort invariant " + sortMode.ToString(), fingerprint);
    };

    var collectionsArray = MajdataPlay.SongStorage.Collections;
    var realCollections = collectionsArray
        .Where(isRealCollection)
        .ToArray();
    var sortedRealCollections = realCollections
        .OrderBy(c => c.Name, StringComparer.Ordinal)
        .ToArray();
    string[] sortedNames = sortedRealCollections.Select(c => c.Name ?? "").ToArray();
    string[] orderedNames = realCollections.Select(c => c.Name ?? "").ToArray();

    addLine("real collection order", string.Join(",", orderedNames.Select(enc).ToArray()));
    addLine("real collection sorted set", string.Join(",", sortedNames.Select(enc).ToArray()));

    if (sortedRealCollections.Length < 2) {
        throw new InvalidOperationException("Need at least two non-custom real collections for equivalence tests.");
    }

    string first = sortedRealCollections[0].Name;
    string second = sortedRealCollections[1].Name;

    openCollection(first, 0);
    appendCase("open first real folder index 0");
    openCollection(first, 1);
    appendCase("open first real folder index 1");
    openCollection(second, 0);
    appendCase("open second real folder index 0");

    openCollection(first, 0);
    openCollection(second, 0);
    appendCase("open first then second real folder");

    openCollection(first, 2);
    appendCase("slide first real folder to index 2");
    openCollection(first, 4);
    appendCase("slide first real folder to index 4");

    foreach (int diff in new[] { 0, 1, 2, 3, 4 }) {
        openCollection(first, 0);
        var cl = coverList();
        coverListType.GetMethod("SlideToDifficulty", Flags).Invoke(cl, new object[] { diff });
        var fixedUpdate = coverListType.GetMethod("FixedUpdate", Flags);
        if (fixedUpdate != null) {
            fixedUpdate.Invoke(cl, new object[0]);
            fixedUpdate.Invoke(cl, new object[0]);
        }
        appendCase("switch difficulty " + diff.ToString() + " on first real folder");
    }

    appendSortInvariant();

    return new { lines = lines.ToArray() };
})()
"@

    $rawLines = if ($result.result.properties.lines.items) { @($result.result.properties.lines.items) } else { @($result.result.properties.lines) }
    $cases = [ordered]@{}
    foreach ($line in $rawLines) {
        $parts = $line -split "`t", 3
        if ($parts.Length -ne 3 -or $parts[0] -ne "CASE") {
            continue
        }

        $cases[$parts[1]] = $parts[2]
    }

    if ($cases.Count -eq 0) {
        throw "No equivalence cases were captured for $Label."
    }

    return [pscustomobject]@{
        Label = $Label
        Cases = $cases
    }
}

function Read-EquivalenceSnapshot {
    param([string]$Label)

    $combined = [ordered]@{}

    function Add-ScenarioSnapshot {
        param([object]$Scenario)

        foreach ($case in $Scenario.Cases.GetEnumerator()) {
            $combined[$case.Name] = $case.Value
        }
    }

    Set-BuiltinSortAndReloadList -SortTypeName "Default"
    Add-ScenarioSnapshot (Read-ActiveEquivalenceSnapshot -Label $Label -CasePrefix "default / ")

    foreach ($sortMode in @("ByDiff", "ByDes", "ByTitle", "ByTime")) {
        Set-BuiltinSortAndReloadList -SortTypeName $sortMode
        Add-ScenarioSnapshot (Read-ActiveEquivalenceSnapshot -Label $Label -CasePrefix "sort $sortMode / ")
    }

    Set-BuiltinSortAndReloadList -SortTypeName "Default"

    if ($combined.Count -eq 0) {
        throw "No equivalence cases were captured for $Label."
    }

    return [pscustomobject]@{
        Label = $Label
        Cases = $combined
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
    Read-EquivalenceSnapshot -Label $Label
}

function Assert-SnapshotSelfConsistent {
    param([object]$Snapshot)

    $failures = @()
    foreach ($case in $Snapshot.Cases.GetEnumerator()) {
        if ($case.Name -like "*real collection order" -or $case.Name -like "*real collection sorted set") {
            continue
        }
        if ($case.Value -notlike "consistent=True|*") {
            $failures += "$($Snapshot.Label): $($case.Name) is internally inconsistent: $($case.Value)"
        }
    }

    if ($failures.Count -gt 0) {
        throw ($failures -join [Environment]::NewLine)
    }
}

function Compare-Snapshots {
    param(
        [object]$Expected,
        [object]$Actual
    )

    function ConvertFrom-EncodedList {
        param([string]$Value)

        if ([string]::IsNullOrWhiteSpace($Value)) {
            return @()
        }

        return @($Value -split "," | ForEach-Object {
            [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_))
        })
    }

    $failures = @()
    foreach ($case in $Expected.Cases.GetEnumerator()) {
        if (-not $Actual.Cases.Contains($case.Name)) {
            $failures += "Missing mod case: $($case.Name)"
            continue
        }

        $actualValue = $Actual.Cases[$case.Name]
        if ($case.Name -like "*real collection order" -or $case.Name -like "*real collection sorted set") {
            $expectedNames = ConvertFrom-EncodedList $case.Value
            $actualNames = ConvertFrom-EncodedList $actualValue
            $expectedNameSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
            foreach ($name in $expectedNames) {
                [void]$expectedNameSet.Add($name)
            }

            $actualWithoutCustomExtras = @($actualNames | Where-Object { $expectedNameSet.Contains($_) })
            if (($actualWithoutCustomExtras -join "`n") -ne ($expectedNames -join "`n")) {
                $failures += "Mismatch: $($case.Name)`n  vanilla: $($expectedNames -join ', ')`n  mod:     $($actualWithoutCustomExtras -join ', ')"
            }

            continue
        }

        if ($actualValue -ne $case.Value) {
            $failures += "Mismatch: $($case.Name)`n  vanilla: $($case.Value)`n  mod:     $actualValue"
        }
    }

    foreach ($case in $Actual.Cases.GetEnumerator()) {
        if (-not $Expected.Cases.Contains($case.Name)) {
            $failures += "Extra mod case: $($case.Name)"
        }
    }

    return $failures
}

try {
    if ($Mode -eq "Vanilla") {
        $snapshot = Run-Snapshot -Label "vanilla" -WithQolMod:$false
        Assert-SnapshotSelfConsistent -Snapshot $snapshot
        Write-Host "Vanilla Majdata equivalence baseline passed: $($snapshot.Cases.Count) cases."
        exit 0
    }

    if ($Mode -eq "Mod") {
        $snapshot = Run-Snapshot -Label "mod" -WithQolMod:$true
        Assert-SnapshotSelfConsistent -Snapshot $snapshot
        Write-Host "Mod Majdata equivalence self-check passed: $($snapshot.Cases.Count) cases."
        exit 0
    }

    $vanilla = Run-Snapshot -Label "vanilla" -WithQolMod:$false
    Assert-SnapshotSelfConsistent -Snapshot $vanilla
    Write-Host "Vanilla Majdata equivalence baseline passed: $($vanilla.Cases.Count) cases."

    $mod = Run-Snapshot -Label "mod" -WithQolMod:$true
    Assert-SnapshotSelfConsistent -Snapshot $mod
    Write-Host "Mod Majdata equivalence self-check passed: $($mod.Cases.Count) cases."

    $failures = Compare-Snapshots -Expected $vanilla -Actual $mod
    if ($failures.Count -gt 0) {
        Write-Host "Majdata default-behaviour equivalence failed: $($failures.Count) mismatches."
        $failures | ForEach-Object { Write-Host $_ }
        exit 1
    }

    Write-Host "Majdata default-behaviour equivalence passed: $($vanilla.Cases.Count) cases."
    exit 0
} finally {
    Stop-Game
}
