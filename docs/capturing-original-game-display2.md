# Capturing the original game on DISPLAY2

The game launches on the second portrait-oriented monitor. In Windows this
monitor can appear as `720x1280` unless the capture process is DPI-aware; that
only captures a scaled part of the 4K portrait display. Use a DPI-aware
PowerShell capture to get the full monitor.

Run this from the repo root after launching `MajdataPlay`:

```powershell
powershell.exe -NoProfile -Command '
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class DpiAwareness {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
}
"@
[DpiAwareness]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
[DpiAwareness]::SetProcessDPIAware() | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$root = "C:\Users\user0-pc\majdataproj"
$outDir = Join-Path $root ".scratch"
$screen = [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Select-Object -First 1
$b = $screen.Bounds
$out = Join-Path $outDir "original-game-display2-dpiaware-full.png"
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
$img = [System.Drawing.Image]::FromFile($out)
$targetW = 900
$targetH = [Math]::Round($img.Height * $targetW / $img.Width)
$scaled = New-Object System.Drawing.Bitmap($targetW, $targetH)
$sg = [System.Drawing.Graphics]::FromImage($scaled)
$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.DrawImage($img, 0, 0, $targetW, $targetH)
$scaledOut = Join-Path $outDir "original-game-display2-dpiaware-full-scaled.png"
$scaled.Save($scaledOut, [System.Drawing.Imaging.ImageFormat]::Png)
$sg.Dispose(); $scaled.Dispose(); $img.Dispose()
Write-Host "full=$out"
Write-Host "scaled=$scaledOut"
'
```

The full-size capture should be `.scratch/original-game-display2-dpiaware-full.png`;
the preview-sized capture should be
`.scratch/original-game-display2-dpiaware-full-scaled.png`.
