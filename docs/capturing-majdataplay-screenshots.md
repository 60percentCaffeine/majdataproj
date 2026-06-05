# Capturing MajdataPlay screenshots
Use Unity `ScreenCapture.CaptureScreenshot` through `TestHookMod`.

1. Launch from `Majdata Hub/game`:

```powershell
powershell.exe -NoProfile -Command "Start-Process '.\start-controller.bat'"
```

2. Wait for `UserData/TestHookMod/ready.json`, then run from repo root:

```powershell
powershell.exe -NoProfile -Command '
$out = "C:\Users\user0-pc\majdataproj\.scratch\majdataplay-screenshot.png"
$code = @"
new Func<object>(() => {
    string path = @"C:\Users\user0-pc\majdataproj\.scratch\majdataplay-screenshot.png";
    UnityEngine.ScreenCapture.CaptureScreenshot(path);
    return new {
        path,
        width = UnityEngine.Screen.width,
        height = UnityEngine.Screen.height,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
    };
})()
"@
$body = @{ code = $code; timeoutMs = 60000; maxDepth = 4; maxResponseBytes = 200000 } | ConvertTo-Json -Compress
Invoke-RestMethod -Uri "http://127.0.0.1:17443/eval-isolated" -Method Post -ContentType application/json -Body $body | ConvertTo-Json -Depth 6
Start-Sleep -Seconds 2
Get-Item $out | Select-Object FullName,Length,LastWriteTime | Format-List
'
```

Notes: use Windows PowerShell for hook HTTP calls; WSL loopback can fail.
`CaptureScreenshot` writes asynchronously. For a specific scene, switch there
first, let UI settle, then run this.
