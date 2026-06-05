param(
    [switch]$UseSRankJportal,
    [string]$OutPath
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$GameRoot = Join-Path $ProjectRoot "Majdata Hub\game"
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = if ($UseSRankJportal) {
        Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\screenshots\latest-song-info-line-s-rank.png"
    } else {
        Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\screenshots\latest-song-info-line-prototype.png"
    }
}
$HookUrl = "http://127.0.0.1:17443/eval-isolated"

function Invoke-GameEval {
    param([string]$Code)

    $body = @{
        code = $Code
        timeoutMs = 60000
        maxDepth = 6
        maxResponseBytes = 300000
    } | ConvertTo-Json -Compress

    Invoke-RestMethod -Uri $HookUrl -Method Post -ContentType application/json -Body $body
}

function Wait-ForHook {
    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Seconds 1
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:17443/health" -TimeoutSec 2
            if ($health.ok) {
                return
            }
        } catch {
        }
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for TestHookMod."
}

Wait-ForHook

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
    sceneSwitcherType.GetMethod("SwitchScene", InstanceFlags).Invoke(switcher, new object[] { "List", true });
    return new { requested = true, frame = UnityEngine.Time.frameCount };
})()
"@ | Out-Null

Start-Sleep -Seconds 5

if ($UseSRankJportal) {
    $target = Invoke-GameEval @"
new Func<object>(() => {
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    Type storageType = Type.GetType("MajdataPlay.SongStorage, Assembly-CSharp", true);
    var collections = (System.Collections.IEnumerable)storageType.GetProperty("Collections", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).GetValue(null, null);
    var allCollections = collections.Cast<object>().ToArray();
    int collectionIndex = Array.FindIndex(allCollections, c => string.Equals((string)c.GetType().GetProperty("Name").GetValue(c, null), "JPORTAL", StringComparison.OrdinalIgnoreCase));
    if (collectionIndex < 0) {
        throw new Exception("JPORTAL collection was not found.");
    }

    object jportal = allCollections[collectionIndex];
    var songs = ((System.Collections.IEnumerable)jportal.GetType().GetMethod("ToArray").Invoke(jportal, new object[0])).Cast<object>().ToArray();

    Type scoreManagerType = Type.GetType("MajdataPlay.ScoreManager, Assembly-CSharp", true);
    Type chartLevelType = Type.GetType("MajdataPlay.ChartLevel, Assembly-CSharp", true);
    var getScore = scoreManagerType.GetMethod("GetScore", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

    int bestSongIndex = -1;
    int bestDiff = -1;
    double bestDx = 0.0;
    int bestPlayCount = 0;
    object bestSong = null;
    for (int songIndex = 0; songIndex < songs.Length; songIndex++) {
        for (int diff = 0; diff <= 4; diff++) {
            object level = Enum.ToObject(chartLevelType, diff);
            object score = getScore.Invoke(null, new object[] { songs[songIndex], level });
            long playCount = (long)score.GetType().GetProperty("PlayCount").GetValue(score, null);
            object acc = score.GetType().GetProperty("Acc").GetValue(score, null);
            double dx = (double)acc.GetType().GetProperty("DX").GetValue(acc, null);
            if (playCount > 0 && dx >= 97.0 && dx < 99.0) {
                bestSongIndex = songIndex;
                bestDiff = diff;
                bestDx = dx;
                bestPlayCount = (int)playCount;
                bestSong = songs[songIndex];
                goto Found;
            }
            if (bestSongIndex < 0 && playCount > 0 && dx >= 97.0) {
                bestSongIndex = songIndex;
                bestDiff = diff;
                bestDx = dx;
                bestPlayCount = (int)playCount;
                bestSong = songs[songIndex];
            }
        }
    }

Found:
    if (bestSongIndex < 0) {
        throw new Exception("No JPORTAL chart with S-rank-or-better score was found.");
    }

    storageType.GetProperty("CollectionIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).SetValue(null, collectionIndex, null);
    jportal.GetType().GetProperty("Index").SetValue(jportal, bestSongIndex, null);

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
        collection = "JPORTAL",
        collectionIndex,
        songIndex = bestSongIndex,
        difficulty = bestDiff,
        dx = bestDx,
        playCount = bestPlayCount,
        title = bestSong.GetType().GetProperty("Title").GetValue(bestSong, null),
        artist = bestSong.GetType().GetProperty("Artist").GetValue(bestSong, null)
    };
})()
"@

    Start-Sleep -Seconds 2
    $target | ConvertTo-Json -Depth 8
}

$result = Invoke-GameEval @"
new Func<object>(() => {
    const string prototypeLine = "JPORTAL | 01:20 | 3 diffs | 240BPM";
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    Type displayerType = Type.GetType("MajdataPlay.Scenes.List.CoverBigDisplayer, Assembly-CSharp", true);
    object displayer = UnityEngine.Resources.FindObjectsOfTypeAll(displayerType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);

    var artist = (TMPro.TMP_Text)displayerType.GetField("_artist", Flags).GetValue(displayer);
    var charter = (TMPro.TMP_Text)displayerType.GetField("_charter", Flags).GetValue(displayer);
    var title = (TMPro.TMP_Text)displayerType.GetField("_title", Flags).GetValue(displayer);
    var archieveRate = (TMPro.TMP_Text)displayerType.GetField("_archieveRate", Flags).GetValue(displayer);
    var rank = (TMPro.TMP_Text)displayerType.GetField("_rank", Flags).GetValue(displayer);

    if ((artist.text ?? "").Contains(prototypeLine)) {
        artist.text = artist.text.Replace("\n" + prototypeLine, "").Replace(prototypeLine, "");
    }

    Transform parent = charter.transform.parent;
    for (int i = parent.childCount - 1; i >= 0; i--) {
        Transform child = parent.GetChild(i);
        if (child.name.StartsWith("QoLPrototypeInfoLine", StringComparison.Ordinal)) {
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    GameObject lineObject = new GameObject("QoLPrototypeInfoLine", typeof(UnityEngine.RectTransform));
    lineObject.transform.SetParent(parent, false);
    TMPro.TextMeshProUGUI extra = lineObject.AddComponent<TMPro.TextMeshProUGUI>();

    var artistRect = (UnityEngine.RectTransform)artist.transform;
    var charterRect = (UnityEngine.RectTransform)charter.transform;
    var extraRect = (UnityEngine.RectTransform)extra.transform;
    extraRect.anchorMin = charterRect.anchorMin;
    extraRect.anchorMax = charterRect.anchorMax;
    extraRect.pivot = charterRect.pivot;
    extraRect.anchoredPosition = artistRect.anchoredPosition + new UnityEngine.Vector2(0f, -21f);
    extraRect.sizeDelta = new UnityEngine.Vector2(charterRect.sizeDelta.x, 17f);
    charterRect.anchoredPosition = artistRect.anchoredPosition + new UnityEngine.Vector2(0f, -43f);

    if (archieveRate != null && archieveRate.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(archieveRate.text)) {
        var archieveRect = (UnityEngine.RectTransform)archieveRate.transform;
        archieveRect.anchoredPosition = artistRect.anchoredPosition + new UnityEngine.Vector2(9.5f, -75f);
        if (rank != null) {
            var rankRect = (UnityEngine.RectTransform)rank.transform;
            rankRect.anchoredPosition = artistRect.anchoredPosition + new UnityEngine.Vector2(-35.8f, -130f);
        }
    }

    extra.font = charter.font;
    extra.fontSharedMaterial = charter.fontSharedMaterial;
    extra.color = charter.color;
    extra.alignment = TMPro.TextAlignmentOptions.Left;
    extra.raycastTarget = false;
    extra.enableWordWrapping = false;
    extra.fontSize = 13f;
    extra.enableAutoSizing = true;
    extra.fontSizeMin = 10f;
    extra.fontSizeMax = 13f;
    extra.overflowMode = TMPro.TextOverflowModes.Ellipsis;
    extra.text = prototypeLine;

    return new {
        title = title.text,
        artist = artist.text,
        charter = charter.text,
        extra = extra.text,
        containsPrototype = extra.text.Contains(prototypeLine)
    };
})()
"@

Start-Sleep -Seconds 1

$capturePath = $OutPath.Replace("\", "\\")
Invoke-GameEval @"
new Func<object>(() => {
    string path = @"$capturePath";
    UnityEngine.ScreenCapture.CaptureScreenshot(path);
    return new {
        path = path,
        width = UnityEngine.Screen.width,
        height = UnityEngine.Screen.height,
        frame = UnityEngine.Time.frameCount,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
    };
})()
"@ | Out-Null

Start-Sleep -Seconds 2
Get-Item $OutPath | Select-Object FullName,Length,LastWriteTime | Format-List
$result | ConvertTo-Json -Depth 8
