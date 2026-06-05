param(
    [string]$OutPath,
    [string]$TestReportPath
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\screenshots\latest-map-list-settings-prototype.png"
}
if ([string]::IsNullOrWhiteSpace($TestReportPath)) {
    $TestReportPath = Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\map-list-settings-auto-test.json"
}
$HookUrl = "http://127.0.0.1:17443/eval-isolated"

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
    sceneSwitcherType.GetMethod("SwitchScene", InstanceFlags).Invoke(switcher, new object[] { "Setting", true });
    return new { requested = true, frame = UnityEngine.Time.frameCount };
})()
"@ | Out-Null

Start-Sleep -Seconds 2

$settingsResult = Invoke-GameEval @"
new Func<object>(() => {
    const string prototypeName = "QoLMapListSettingsPrototype";
    const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    Type managerType = Type.GetType("MajdataPlay.Scenes.Setting.SettingManager, Assembly-CSharp", true);
    Type menuType = Type.GetType("MajdataPlay.Scenes.Setting.Menu, Assembly-CSharp", true);
    Type optionType = Type.GetType("MajdataPlay.Scenes.Setting.Option, Assembly-CSharp", true);
    UnityEngine.Component manager = UnityEngine.Resources.FindObjectsOfTypeAll(managerType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (manager == null) {
        throw new Exception("Active SettingManager was not found.");
    }

    for (int i = manager.transform.childCount - 1; i >= 0; i--) {
        UnityEngine.Transform child = manager.transform.GetChild(i);
        if (child.name.StartsWith(prototypeName, StringComparison.Ordinal)) {
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        } else {
            child.gameObject.SetActive(false);
        }
    }

    UnityEngine.GameObject menuPrefab = (UnityEngine.GameObject)managerType.GetField("menuPrefab", Flags).GetValue(manager);
    UnityEngine.GameObject menuObject = UnityEngine.Object.Instantiate(menuPrefab, manager.transform);
    menuObject.name = prototypeName;
    menuObject.SetActive(true);

    UnityEngine.Component menu = menuObject.GetComponent(menuType);
    if (menu != null) {
        ((MonoBehaviour)menu).enabled = false;
    }
    menuType.GetProperty("Name", Flags).SetValue(menu, "Map List", null);

    TMPro.TextMeshPro title = (TMPro.TextMeshPro)menuType.GetField("titleText", Flags).GetValue(menu);
    title.text = "Map List Settings";

    UnityEngine.GameObject optionPrefab = (UnityEngine.GameObject)menuType.GetField("optionPrefab", Flags).GetValue(menu);
    var options = new[] {
        new { Name = "Difficulty Filter", Value = "No", Description = "Difficulty Filter Description" },
        new { Name = "Sorting", Value = "Default", Description = "Sorting Description" },
        new { Name = "Grouping", Value = "Default", Description = "Grouping Description" },
        new { Name = "Downloaded Songs Filter", Value = "Mixed", Description = "Downloaded Songs Filter Description" }
    };

    var created = new System.Collections.Generic.List<object>();
    for (int i = 0; i < options.Length; i++) {
        UnityEngine.GameObject optionObject = UnityEngine.Object.Instantiate(optionPrefab, menuObject.transform);
        optionObject.name = prototypeName + "Option" + i;
        optionObject.SetActive(true);
        UnityEngine.Component option = optionObject.GetComponent(optionType);
        if (option != null) {
            ((MonoBehaviour)option).enabled = false;
        }

        TMPro.TextMeshPro nameText = (TMPro.TextMeshPro)optionType.GetField("_nameText", Flags).GetValue(option);
        TMPro.TextMeshPro valueText = (TMPro.TextMeshPro)optionType.GetField("_valueText", Flags).GetValue(option);
        TMPro.TextMeshPro descriptionText = (TMPro.TextMeshPro)optionType.GetField("_descriptionText", Flags).GetValue(option);
        nameText.text = options[i].Name;
        valueText.text = options[i].Value;
        descriptionText.text = options[i].Description;

        if (i == 0) {
            optionObject.transform.localPosition = new UnityEngine.Vector3(0f, 0f, 0f);
            optionObject.transform.localScale = UnityEngine.Vector3.one;
        } else if (i == 1) {
            optionObject.transform.localPosition = new UnityEngine.Vector3(330f, 0f, 0f);
            optionObject.transform.localScale = new UnityEngine.Vector3(0.6f, 0.6f, 0.6f);
        } else {
            optionObject.transform.localPosition = new UnityEngine.Vector3(1000f, 0f, 0f);
            optionObject.transform.localScale = UnityEngine.Vector3.zero;
        }
        created.Add(new { options[i].Name, options[i].Value, options[i].Description });
    }

    return new {
        settingsGroups = new[] { "Map List", "Game", "Judge", "Display", "Volume", "Mod", "Debug", "ChartSetting" },
        activeGroup = "Map List",
        firstVisibleOption = options[0].Name,
        secondVisibleOption = options[1].Name,
        options = created.ToArray()
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
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
    };
})()
"@ | Out-Null

Start-Sleep -Seconds 2

$folderSwitchResult = Invoke-GameEval @"
new Func<object>(() => {
    const string targetFolder = "Random Recommended";
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

    Type sceneSwitcherType = Type.GetType("MajdataPlay.SceneSwitcher, Assembly-CSharp", true);
    object switcher = UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    sceneSwitcherType.GetMethod("SwitchScene", InstanceFlags).Invoke(switcher, new object[] { "List", true });
    return new { requested = true, targetFolder };
})()
"@

Start-Sleep -Seconds 5

$folderAssertResult = Invoke-GameEval @"
new Func<object>(() => {
    const string targetFolder = "Random Recommended";
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

    int targetIndex = collections.FindIndex(c => string.Equals((string)c.GetType().GetProperty("Name").GetValue(c, null), targetFolder, StringComparison.Ordinal));
    if (targetIndex < 0) {
        Array emptySongs = Array.CreateInstance(songDetailType, 0);
        object randomRecommended = Activator.CreateInstance(collectionType, new object[] { targetFolder, emptySongs });
        randomRecommended.GetType().GetProperty("Id").SetValue(randomRecommended, Guid.NewGuid(), null);
        randomRecommended.GetType().GetProperty("Path").SetValue(randomRecommended, targetFolder, null);
        randomRecommended.GetType().GetProperty("IsOnline").SetValue(randomRecommended, true, null);
        randomRecommended.GetType().GetProperty("IsVirtual").SetValue(randomRecommended, true, null);
        int onlineIndex = collections.FindIndex(c => (bool)c.GetType().GetProperty("IsOnline").GetValue(c, null));
        targetIndex = onlineIndex >= 0 ? onlineIndex + 1 : collections.Count;
        collections.Insert(targetIndex, randomRecommended);
        Array newCollections = Array.CreateInstance(collectionType, collections.Count);
        for (int i = 0; i < collections.Count; i++) {
            newCollections.SetValue(collections[i], i);
        }
        if (collectionsProp.CanWrite) {
            collectionsProp.SetValue(null, newCollections, null);
        } else {
            storageType.GetField("<Collections>k__BackingField", NonPublicStatic).SetValue(null, newCollections);
        }
    }

    Type coverListType = Type.GetType("MajdataPlay.Scenes.List.CoverListDisplayer, Assembly-CSharp", true);
    object coverList = UnityEngine.Resources.FindObjectsOfTypeAll(coverListType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy);
    if (coverList == null) {
        throw new Exception("Active CoverListDisplayer was not found.");
    }

    coverListType.GetMethod("SwitchToDirListInternal", InstanceFlags).Invoke(coverList, new object[0]);

    object memory = coverListType.GetField("_collections", InstanceFlags).GetValue(coverList);
    object[] carouselCollections = ((System.Collections.IEnumerable)memory.GetType().GetMethod("ToArray").Invoke(memory, new object[0])).Cast<object>().ToArray();
    int carouselTargetIndex = -1;
    int carouselAllIndex = -1;
    string selectedTargetName = "";
    string selectedAllName = "";
    for (int pos = 0; pos < carouselCollections.Length; pos++) {
        coverListType.GetMethod("SlideListInternal", InstanceFlags).Invoke(coverList, new object[] { pos });
        object selected = coverListType.GetProperty("SelectedCollection", InstanceFlags).GetValue(coverList, null);
        string selectedName = selected == null ? "" : (string)selected.GetType().GetProperty("Name").GetValue(selected, null);
        if (carouselTargetIndex < 0 && selectedName == targetFolder) {
            carouselTargetIndex = pos;
            selectedTargetName = selectedName;
        }
        if (carouselAllIndex < 0 && selectedName == "All") {
            carouselAllIndex = pos;
            selectedAllName = selectedName;
        }
    }

    return new {
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        storageTargetIndex = targetIndex,
        carouselTargetIndex,
        carouselAllIndex,
        selectedTargetName,
        selectedAllName,
        targetSwitchOk = selectedTargetName == targetFolder,
        allSwitchOk = selectedAllName == "All"
    };
})()
"@

$report = [ordered]@{
    settingsPrototype = $settingsResult.result.properties
    settingsGroupOrderOk = $settingsResult.result.properties.settingsGroups.items[0] -eq "Map List" -and $settingsResult.result.properties.settingsGroups.items[1] -eq "Game"
    folderRoundTrip = $folderAssertResult.result.properties
    folderSwitchingOk = [bool]$folderAssertResult.result.properties.targetSwitchOk -and [bool]$folderAssertResult.result.properties.allSwitchOk
    screenshot = (Resolve-Path $OutPath).Path
}

$report | ConvertTo-Json -Depth 20 | Set-Content -Path $TestReportPath -Encoding UTF8
Get-Item $OutPath | Select-Object FullName,Length,LastWriteTime | Format-List
Get-Item $TestReportPath | Select-Object FullName,Length,LastWriteTime | Format-List
$report | ConvertTo-Json -Depth 20
