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

## 2026-06-01 - Issue 16: input mapping canary

- Added `InputMappingCanaryTests.BootedGameHasInitializedInputMappingsAndRuntimeState`.
- The canary boots the real game with candidate mods installed, waits up to 60 seconds for `InputManager` initialization, then verifies:
  - `GameUpdater` is active and able to drive input pre-update;
  - `DummyTouchPanelRenderer` is active and exposes collider-to-sensor mappings, so the test does not require physical touch-panel hardware;
  - public input enums expose the expected 12 button zones, 33 sensor areas, and 2 switch states;
  - private `InputManager` binding and runtime arrays are populated with 12 unique binding keys, 12 unique button zones, 33 unique sensor areas, 12 button states, and 35 touch sensor states;
  - touch-angle samples are generated;
  - configured button/touch polling and debounce values load from settings and are non-negative;
  - representative button and sensor status calls can be made without hardware input.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `InputMappingCanaryTests`: passed, 1/1, duration 14s.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File './sample-mod/test-integration.ps1'`: passed, 8/8, duration 2m 22s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - The canary intentionally records but does not require `ButtonRingConnected` or `TouchPanelConnected`; hardware presence is not part of the assertion surface.
  - Existing fatal log scanning still applies to the `input-mapping` artifact, so new input initialization errors fail the test while missing physical touch hardware is not asserted as a failure by the canary itself.

## 2026-06-01 - Issue 17: enter and exit flow canary

- Added `EnterExitFlowCanaryTests.GameCanNavigateFromTitleToListAndBackToTitle`.
- The canary boots the real game with candidate mods installed, waits for the stable Title state plus ready `SongStorage`, then drives a shallow scene flow through `SceneSwitcher.SwitchScene`:
  - records the initial Title scene state, active scene handle/root count, frame count, active `TitleManager`, and active `SceneSwitcher`;
  - requests a deterministic switch from `Title` to `List`;
  - verifies `SceneSwitcher.CurrentScene`, Unity active scene, frame count, and active `ListManager` after entry;
  - requests a deterministic switch back from `List` to `Title`;
  - verifies the returned Title scene and active `TitleManager` before shutdown.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `EnterExitFlowCanaryTests`: passed, 1/1, duration 1m 10s.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File './sample-mod/test-integration.ps1'`: passed, 9/9, duration 3m 24s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - The scene switch is intentionally API-driven rather than input-driven so the test remains deterministic and does not depend on physical controls.
  - The canary waits for `SongStorage` before entering `List`; switching earlier can race Title's startup scan and produce less useful failures.

## 2026-06-01 - Issue 18: gameplay dry-run canary

- Added `GameplayDryRunCanaryTests.KnownLocalChartCanEnterGameplayAndAdvanceTime`.
- The canary boots the real game with candidate mods installed, waits for Title plus ready `SongStorage`, then starts local `MAJTITLE` on `Easy` by constructing the expected internal `GameInfo` and switching to the `Game` scene through `SceneSwitcher`.
- Runtime changes are kept in-memory and restored before shutdown:
  - `Mod.AutoPlay` is set to `Enable` so the dry run does not require deterministic physical/input simulation;
  - global and track volume are set to `0` before gameplay load to avoid audible playback;
  - original autoplay and volume values are restored during cleanup, and the scene is switched back to `List`.
- The canary verifies:
  - the `Game` scene becomes active;
  - `GamePlayManager`, `NoteManager`, `NotePoolManager`, `NoteLoader`, and `ObjectCounter` are active;
  - `GamePlayManager.State` reaches `Running`, `IsStart` is true, autoplay is enabled, and audio length is positive;
  - `NoteLoader.NoteCount` and `ObjectCounter.NoteSum` are nonzero;
  - `ThisFrameSec` and frame count advance after gameplay starts.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `GameplayDryRunCanaryTests`: passed, 1/1, duration 1m 14s.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File './sample-mod/test-integration.ps1'`: passed, 10/10, duration 4m 29s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - Reflection sets the backing field for `Majdata<GameInfo>.Instance`; the exposed property returns by reference and is not practical to assign via reflection.
  - The canary asserts initialization and time progression rather than exact score, because deterministic score assertions are unnecessary for this regression layer.
