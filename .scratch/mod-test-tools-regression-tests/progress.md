# Progress

## 2026-06-01 - Issue 15: audio system canary

- Added `AudioSystemCanaryTests.BootedGameHasInitializedSaneAudioState`.
- The canary boots the real game with candidate mods installed, then verifies:
  - at least one active Unity `AudioListener`;
  - an active MajdataPlay `AudioManager`;
  - game audio backend and `Global`, `BGM`, `Track`, and `Tap` volumes are present and within `0..1`;
  - the generated `AudioManager.MixingMatrix` has rows, exactly two input columns, finite values, a nonzero route, and bounded values;
  - the private SFX cache has non-empty samples;
  - `tap_perfect.wav` is cached, decoded, has a positive duration, and is not started by inspection;
  - loading `StreamingAssets/SFX/tap_perfect.wav` through `AudioManager.LoadMusic(..., false, false)` produces a non-empty sample with positive duration, does not start playback, and is stopped/disposed immediately.
- Tightened `IntegrationAssertions.EvalResultProperties` so non-200 eval responses include the response body. This made isolated-eval compiler failures actionable while debugging the audio canary.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `AudioSystemCanaryTests`: passed, 1/1, duration 11s.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File './sample-mod/test-integration.ps1'`: passed, 7/7, duration 2m 11s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - Direct Windows `dotnet test` needs `MODTEST_PROJECT_ROOT` set inside PowerShell; setting it only in WSL did not reach the Windows process.
  - The Unity compile reference available to the eval bridge does not expose `AudioListener.pause`, `AudioListener.volume`, `AudioSettings.outputSampleRate`, or `AudioSettings.speakerMode`, so the canary relies on active listener presence plus MajdataPlay's own audio settings and mixer state.
