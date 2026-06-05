param(
    [string]$OutPath
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\screenshots\latest-random-recommended-folder-prototype.png"
}
$HookUrl = "http://127.0.0.1:17443/eval-isolated"

function Invoke-GameEval {
    param([string]$Code)

    $body = @{
        code = $Code
        timeoutMs = 60000
        maxDepth = 8
        maxResponseBytes = 500000
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

$result = Invoke-GameEval @"
new Func<object>(() => {
    const string folderName = "Random Recommended";
    const string folderTileText = "Random\nRecommended";
    const System.Reflection.BindingFlags PublicStatic =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Static;
    const System.Reflection.BindingFlags NonPublicStatic =
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Static;
    const System.Reflection.BindingFlags InstanceFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    Type storageType = Type.GetType("MajdataPlay.SongStorage, Assembly-CSharp", true);
    Type collectionType = Type.GetType("MajdataPlay.Collections.SongCollection, Assembly-CSharp", true);
    Type songDetailType = Type.GetType("MajdataPlay.ISongDetail, Assembly-CSharp", true);

    var collectionsProp = storageType.GetProperty("Collections", PublicStatic);
    var collections = ((System.Collections.IEnumerable)collectionsProp.GetValue(null, null)).Cast<object>().ToList();
    int existingIndex = collections.FindIndex(c => {
        string name = (string)c.GetType().GetProperty("Name").GetValue(c, null);
        return string.Equals(name, folderName, StringComparison.Ordinal) ||
            string.Equals(name, folderTileText, StringComparison.Ordinal);
    });

    if (existingIndex < 0) {
        Array emptySongs = Array.CreateInstance(songDetailType, 0);
        object randomRecommended = Activator.CreateInstance(collectionType, new object[] { folderName, emptySongs });
        randomRecommended.GetType().GetProperty("Id").SetValue(randomRecommended, Guid.NewGuid(), null);
        randomRecommended.GetType().GetProperty("Path").SetValue(randomRecommended, folderName, null);
        randomRecommended.GetType().GetProperty("IsOnline").SetValue(randomRecommended, true, null);
        randomRecommended.GetType().GetProperty("IsVirtual").SetValue(randomRecommended, true, null);
        randomRecommended.GetType().GetProperty("IsSorted").SetValue(randomRecommended, true, null);

        int onlineIndex = collections.FindIndex(c => (bool)c.GetType().GetProperty("IsOnline").GetValue(c, null));
        int insertIndex = onlineIndex >= 0 ? onlineIndex + 1 : collections.Count;
        collections.Insert(insertIndex, randomRecommended);
        existingIndex = insertIndex;
    } else {
        collections[existingIndex].GetType().GetProperty("Name").SetValue(collections[existingIndex], folderName, null);
        collections[existingIndex].GetType().GetProperty("Path").SetValue(collections[existingIndex], folderName, null);
        collections[existingIndex].GetType().GetProperty("IsOnline").SetValue(collections[existingIndex], true, null);
        collections[existingIndex].GetType().GetProperty("IsVirtual").SetValue(collections[existingIndex], true, null);
    }

    Array newCollections = Array.CreateInstance(collectionType, collections.Count);
    for (int i = 0; i < collections.Count; i++) {
        newCollections.SetValue(collections[i], i);
    }

    if (collectionsProp.CanWrite) {
        collectionsProp.SetValue(null, newCollections, null);
    } else {
        storageType.GetField("<Collections>k__BackingField", NonPublicStatic).SetValue(null, newCollections);
    }
    storageType.GetProperty("CollectionIndex", PublicStatic).SetValue(null, existingIndex, null);

    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (coverList == null) {
        throw new Exception("Active CoverListDisplayer was not found.");
    }

    coverListType.GetMethod("SwitchToDirListInternal", InstanceFlags).Invoke(coverList, new object[0]);
    coverListType.GetMethod("SlideListInternal", InstanceFlags).Invoke(coverList, new object[] { existingIndex });

    Type folderCoverType = Type.GetType("MajdataPlay.Scenes.List.FolderCoverSmallDisplayer, Assembly-CSharp", true);
    foreach (UnityEngine.Component tile in UnityEngine.Resources.FindObjectsOfTypeAll(folderCoverType).OfType<UnityEngine.Component>()) {
        object boundCollection = folderCoverType.GetField("_boundCollection", InstanceFlags).GetValue(tile);
        if (boundCollection == null) {
            continue;
        }
        string boundName = (string)boundCollection.GetType().GetProperty("Name").GetValue(boundCollection, null);
        if (string.Equals(boundName, folderName, StringComparison.Ordinal)) {
            TMPro.TMP_Text folderText = (TMPro.TMP_Text)folderCoverType.GetField("_folderText", InstanceFlags).GetValue(tile);
            if (folderText != null) {
                folderText.text = folderTileText;
                folderText.alignment = TMPro.TextAlignmentOptions.Center;
                folderText.enableWordWrapping = false;
                folderText.overflowMode = TMPro.TextOverflowModes.Overflow;
                folderText.enableAutoSizing = true;
                folderText.fontSizeMin = 12f;
                folderText.fontSizeMax = 18f;
                var textRect = (UnityEngine.RectTransform)folderText.transform;
                textRect.sizeDelta = new UnityEngine.Vector2(118f, 54f);
            }
            folderCoverType.GetProperty("IsOnline", InstanceFlags).SetValue(tile, true, null);
            UnityEngine.GameObject icon = (UnityEngine.GameObject)folderCoverType.GetField("_icon", InstanceFlags).GetValue(tile);
            if (icon != null) {
                icon.SetActive(true);
            }
        }
    }

    return new {
        folderName,
        index = existingIndex,
        totalCollections = collections.Count,
        isOnline = collections[existingIndex].GetType().GetProperty("IsOnline").GetValue(collections[existingIndex], null),
        isVirtual = collections[existingIndex].GetType().GetProperty("IsVirtual").GetValue(collections[existingIndex], null),
        count = collections[existingIndex].GetType().GetProperty("Count").GetValue(collections[existingIndex], null)
    };
})()
"@

Start-Sleep -Seconds 2

Invoke-GameEval @"
new Func<object>(() => {
    const string folderName = "Random Recommended";
    const string folderTileText = "Random\nRecommended";
    const System.Reflection.BindingFlags InstanceFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;
    Type folderCoverType = Type.GetType("MajdataPlay.Scenes.List.FolderCoverSmallDisplayer, Assembly-CSharp", true);
    int enabled = 0;
    foreach (UnityEngine.Component tile in UnityEngine.Resources.FindObjectsOfTypeAll(folderCoverType).OfType<UnityEngine.Component>()) {
        object boundCollection = folderCoverType.GetField("_boundCollection", InstanceFlags).GetValue(tile);
        if (boundCollection == null) {
            continue;
        }
        string boundName = (string)boundCollection.GetType().GetProperty("Name").GetValue(boundCollection, null);
        if (string.Equals(boundName, folderName, StringComparison.Ordinal)) {
            TMPro.TMP_Text folderText = (TMPro.TMP_Text)folderCoverType.GetField("_folderText", InstanceFlags).GetValue(tile);
            if (folderText != null) {
                folderText.text = folderTileText;
                folderText.alignment = TMPro.TextAlignmentOptions.Center;
                folderText.enableWordWrapping = false;
                folderText.overflowMode = TMPro.TextOverflowModes.Overflow;
                folderText.enableAutoSizing = true;
                folderText.fontSizeMin = 12f;
                folderText.fontSizeMax = 18f;
                var textRect = (UnityEngine.RectTransform)folderText.transform;
                textRect.sizeDelta = new UnityEngine.Vector2(118f, 54f);
            }
            folderCoverType.GetProperty("IsOnline", InstanceFlags).SetValue(tile, true, null);
            UnityEngine.GameObject icon = (UnityEngine.GameObject)folderCoverType.GetField("_icon", InstanceFlags).GetValue(tile);
            if (icon != null) {
                icon.SetActive(true);
                enabled++;
            }
        }
    }
    return new { enabled };
})()
"@ | Out-Null

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
