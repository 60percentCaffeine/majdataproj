param(
    [string]$HydrationOutPath,
    [string]$RefreshOutPath
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
if ([string]::IsNullOrWhiteSpace($HydrationOutPath)) {
    $HydrationOutPath = Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\screenshots\latest-hydration-progress-prototype.png"
}
if ([string]::IsNullOrWhiteSpace($RefreshOutPath)) {
    $RefreshOutPath = Join-Path $ProjectRoot ".scratch\majdata-qol-song-list-mod\screenshots\latest-random-recommended-refresh-prototype.png"
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

function Show-StatusAndCapture {
    param(
        [string]$Message,
        [string]$OutPath
    )

    $escapedMessage = $Message.Replace("\", "\\").Replace('"', '\"')
    Invoke-GameEval @"
new Func<object>(() => {
    const string rootName = "QoLUpperScreenStatusPrototype";
    const string message = "$escapedMessage";

    foreach (var old in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Transform>()) {
        if (old != null && old.gameObject != null && old.gameObject.name == rootName) {
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
    }

    TMPro.TMP_Text styleSource = UnityEngine.Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()
        .FirstOrDefault(t =>
            t != null &&
            t.font != null &&
            t.gameObject != null &&
            t.gameObject.activeInHierarchy &&
            (t.text ?? "").Contains("Press Select P1"));
    if (styleSource == null) {
        styleSource = UnityEngine.Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()
            .FirstOrDefault(t => t != null && t.font != null && t.gameObject != null && t.gameObject.activeInHierarchy);
    }
    if (styleSource == null || styleSource.transform.parent == null) {
        throw new Exception("No active text anchor was found.");
    }

    var root = new UnityEngine.GameObject(rootName, typeof(UnityEngine.RectTransform));
    root.transform.SetParent(styleSource.transform.parent, false);
    root.transform.SetAsLastSibling();

    var rootRect = (UnityEngine.RectTransform)root.transform;
    rootRect.anchorMin = new UnityEngine.Vector2(1f, 0.5f);
    rootRect.anchorMax = new UnityEngine.Vector2(1f, 0.5f);
    rootRect.pivot = new UnityEngine.Vector2(1f, 0.5f);
    rootRect.anchoredPosition = new UnityEngine.Vector2(-22f, 5f);
    rootRect.sizeDelta = new UnityEngine.Vector2(320f, 42f);

    var panelObject = new UnityEngine.GameObject("StatusPanel", typeof(UnityEngine.RectTransform));
    panelObject.transform.SetParent(root.transform, false);
    var panel = panelObject.AddComponent<UnityEngine.UI.Image>();
    panel.color = new UnityEngine.Color(0f, 0f, 0f, 0.58f);
    panel.raycastTarget = false;
    var panelRect = (UnityEngine.RectTransform)panelObject.transform;
    panelRect.anchorMin = new UnityEngine.Vector2(0f, 0f);
    panelRect.anchorMax = new UnityEngine.Vector2(1f, 1f);
    panelRect.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    panelRect.anchoredPosition = UnityEngine.Vector2.zero;
    panelRect.sizeDelta = UnityEngine.Vector2.zero;

    var textObject = new UnityEngine.GameObject("StatusText", typeof(UnityEngine.RectTransform));
    textObject.transform.SetParent(root.transform, false);
    var text = textObject.AddComponent<TMPro.TextMeshProUGUI>();
    if (styleSource != null) {
        text.font = styleSource.font;
        text.fontSharedMaterial = styleSource.fontSharedMaterial;
    }
    text.text = message;
    text.color = UnityEngine.Color.white;
    text.alignment = TMPro.TextAlignmentOptions.MidlineRight;
    text.fontSize = 18f;
    text.enableAutoSizing = true;
    text.fontSizeMin = 14f;
    text.fontSizeMax = 18f;
    text.enableWordWrapping = false;
    text.raycastTarget = false;

    var rect = (UnityEngine.RectTransform)text.transform;
    rect.anchorMin = new UnityEngine.Vector2(0f, 0f);
    rect.anchorMax = new UnityEngine.Vector2(1f, 1f);
    rect.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    rect.anchoredPosition = new UnityEngine.Vector2(-16f, 0f);
    rect.sizeDelta = new UnityEngine.Vector2(-32f, -4f);
    text.ForceMeshUpdate();
    float panelWidth = UnityEngine.Mathf.Clamp(text.preferredWidth + 44f, 240f, 560f);
    rootRect.sizeDelta = new UnityEngine.Vector2(panelWidth, 42f);

    var shadowObject = new UnityEngine.GameObject("StatusTextShadow", typeof(UnityEngine.RectTransform));
    shadowObject.transform.SetParent(root.transform, false);
    var shadow = shadowObject.AddComponent<TMPro.TextMeshProUGUI>();
    if (styleSource != null) {
        shadow.font = styleSource.font;
        shadow.fontSharedMaterial = styleSource.fontSharedMaterial;
    }
    shadow.text = message;
    shadow.color = new UnityEngine.Color(0f, 0f, 0f, 0.75f);
    shadow.alignment = TMPro.TextAlignmentOptions.MidlineRight;
    shadow.fontSize = text.fontSize;
    shadow.enableAutoSizing = true;
    shadow.fontSizeMin = text.fontSizeMin;
    shadow.fontSizeMax = text.fontSizeMax;
    shadow.enableWordWrapping = false;
    shadow.raycastTarget = false;

    var shadowRect = (UnityEngine.RectTransform)shadow.transform;
    shadowRect.anchorMin = rect.anchorMin;
    shadowRect.anchorMax = rect.anchorMax;
    shadowRect.pivot = rect.pivot;
    shadowRect.anchoredPosition = rect.anchoredPosition + new UnityEngine.Vector2(1.5f, -1.5f);
    shadowRect.sizeDelta = rect.sizeDelta;
    panelObject.transform.SetAsFirstSibling();
    textObject.transform.SetAsLastSibling();

    return new {
        message,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        screen = UnityEngine.Screen.width + "x" + UnityEngine.Screen.height
    };
})()
"@ | Out-Null

    Start-Sleep -Milliseconds 500

    $capturePath = $OutPath.Replace("\", "\\")
    Invoke-GameEval @"
new Func<object>(() => {
    string path = @"$capturePath";
    UnityEngine.ScreenCapture.CaptureScreenshot(path);
    return new {
        path,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        width = UnityEngine.Screen.width,
        height = UnityEngine.Screen.height
    };
})()
"@ | Out-Null

    Start-Sleep -Seconds 2
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

Show-StatusAndCapture -Message "Calculating BPM for 4/18 songs..." -OutPath $HydrationOutPath
Show-StatusAndCapture -Message "Refreshing Random Recommended..." -OutPath $RefreshOutPath

Get-Item $HydrationOutPath | Select-Object FullName,Length,LastWriteTime | Format-List
Get-Item $RefreshOutPath | Select-Object FullName,Length,LastWriteTime | Format-List
