# Progress

## 2026-06-01 - Issue 01: test hook contract smoke test

- Added `TestHookContractSmokeTests.ReadinessFileAndHealthEndpointExposeMatchingHookContract`.
- The test launches the real MajdataPlay game through `TestHarness.LaunchAsync`, then parses `UserData/TestHookMod/ready.json` from the game directory and calls `/health` through `TestClient`.
- The readiness file assertions cover:
  - positive `pid`;
  - non-empty `host`;
  - positive `port`;
  - parseable `startedAt`;
  - non-empty `bridgeVersion`.
- The `/health` assertions verify `ok:true`, matching `pid`, `host`, `port`, `bridgeVersion`, and `replEnabled`, plus `mainThreadDispatcherReady:true`.
- The test shuts down through `HarnessRun.ShutdownOrKillAsync` and collects logs under `.scratch/mod-test-tools-artifacts/test-hook-contract`.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `TestHookContractSmokeTests`: passed, 1/1, duration 4s.
- Notes:
  - The test uses `JsonDocument` for both readiness and `/health`, so contract regressions fail on structured fields rather than brittle string-only checks.
  - The current full integration suite had already passed 13/13 before this issue was added; the next full run should include this as the 14th integration test.

## 2026-06-01 - Issue 02: persistent eval session regression

- Added `PersistentEvalSessionRegressionTests.EvalSessionStateIsPersistentIsolatedAndResettable`.
- The test launches the real game through the harness and exercises the eval endpoints in order:
  - creates `int issue02Value = 41;` through persistent `/eval`;
  - verifies a later persistent `/eval` can read `issue02Value + 1 == 42`;
  - verifies `/eval-isolated` cannot see the persistent variable and returns a structured `phase:"compilation"` failure;
  - calls `/reset-session` and verifies it returns `phase:"reset"` with an advanced `sessionVersion`;
  - verifies the persistent `/eval` session can no longer see `issue02Value` after reset.
- Failure messages include the session transition being exercised, so regressions identify whether persistence, isolation, or reset behavior broke.
- Cleanup calls `/reset-session` again best-effort, then shuts the game down through `HarnessRun.ShutdownOrKillAsync` and collects logs under `.scratch/mod-test-tools-artifacts/persistent-eval-session`.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `PersistentEvalSessionRegressionTests`: passed, 1/1, duration 22s.
- Notes:
  - The persistent session model replays prior statements into later snippets, so a local variable declaration is sufficient to prove session replay.
  - The expected isolated/reset failures are compilation-phase failures; the test intentionally checks phase and `ok:false`, not exact compiler diagnostic text.

## 2026-06-01 - Issue 03: structured eval serialization regression

- Added `StructuredEvalSerializationRegressionTests.EvalReturnsStructuredPrimitiveArrayDictionaryAndDtoShapes`.
- The test launches the real game and evaluates one object containing:
  - primitive `Primitive = 123`;
  - array `Array = new[] { 2, 4, 6 }`;
  - dictionary entries `alpha = 7` and `beta = 9`;
  - DTO-shaped anonymous object with `Name = "structured-dto"`, `Score = 42`, and `Active = true`.
- Assertions parse the eval response JSON and verify:
  - `ok:true` and `phase:"execution"`;
  - primitive value is directly usable as a number;
  - array is a structured object with three `items`, not a string;
  - dictionary is a structured object with two `entries`, not a string;
  - DTO is a structured object with named `properties`, not a string.
- The test shuts down through the harness and collects logs under `.scratch/mod-test-tools-artifacts/structured-eval-serialization`.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `StructuredEvalSerializationRegressionTests`: passed, 1/1, duration 13s.
- Notes:
  - This locks in the current bounded serializer shape: enumerables use `items`, dictionaries use `entries`, and DTO-like objects expose public `properties`.
  - The assertions intentionally fail if array/dictionary/DTO values degrade to opaque strings.

## 2026-06-01 - Issue 04: serialization limits and cycles regression

- Added `SerializationLimitsAndCyclesRegressionTests.EvalSerializationHandlesCyclesAndReportsBoundedLimitFailures`.
- Cycle scenario:
  - eval creates a `Dictionary<string, object>` with `name = "cycle-root"` and `self` pointing back to the same dictionary;
  - assertions verify the serialized root has a positive `$id`;
  - assertions verify the `self` dictionary entry serializes as a `$ref` back to the root id.
- Depth-limit scenario:
  - eval returns `new { Inner = new { Value = 7 } }`;
  - request sets `MaxDepth = 1`;
  - assertions verify a structured failure with `ok:false` and `phase:"serialization"`.
- Response-size scenario:
  - eval returns `new string('x', 4096)`;
  - request sets `MaxResponseBytes = 80`;
  - assertions verify a structured failure with `ok:false` and `phase:"serialization"`.
- The test does not assert exact exception text; it only requires a serialization-phase failure with an error object and type.
- Cleanup shuts down through the harness and collects logs under `.scratch/mod-test-tools-artifacts/serialization-limits-cycles`.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `SerializationLimitsAndCyclesRegressionTests`: passed, 1/1, duration 23s.
- Notes:
  - The cycle check locks in the current `$id`/`$ref` behavior without caring about the concrete dictionary type name.
  - The limit checks use request-level `MaxDepth` and `MaxResponseBytes`, so they exercise the public eval contract rather than private serializer methods.

## 2026-06-01 - Issue 05: eval error shape regression

- Added `EvalErrorShapeRegressionTests.EvalReportsStructuredCompilationExecutionAndTimeoutErrors`.
- Compilation failure scenario:
  - sends invalid C# source `this is not valid csharp`;
  - asserts HTTP 400, `ok:false`, `phase:"compilation"`, and a non-empty structured `errors` array.
- Runtime exception scenario:
  - returns `Task.FromException<object>(new InvalidOperationException("issue05 runtime boom"))`;
  - asserts HTTP 500, `ok:false`, `phase:"execution"`, error type containing `InvalidOperationException`, and broad message evidence.
- Timeout scenario:
  - evaluates a snippet that sleeps for 2000 ms;
  - request sets `TimeoutMs = 50`;
  - asserts HTTP 408, `ok:false`, `phase:"execution"`, and error type containing `TimeoutException`.
- The test shuts down through the harness and collects logs under `.scratch/mod-test-tools-artifacts/eval-error-shape`.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `EvalErrorShapeRegressionTests`: passed, 1/1, duration 22s.
- Notes:
  - A synchronous `throw` inside the reflected snippet surfaced as `TargetInvocationException`, so the runtime-exception assertion uses a faulted `Task<object>` to exercise the hook's base-exception handling path.
  - The assertions intentionally avoid exact compiler/runtime wording; they pin the stable envelope and broad error identity only.

## 2026-06-01 - Issue 06: Unity main-thread eval regression

- Added `MainThreadEvalRegressionTests.EvalCanReadUnitySceneStateOnMainThreadAndGameContinuesAdvancing`.
- The test launches the real game and reads Unity state through `/eval-isolated`:
  - active scene name;
  - active scene loaded flag;
  - active scene root `GameObject` count;
  - active camera count through `Resources.FindObjectsOfTypeAll`;
  - `Time.frameCount`;
  - `Time.realtimeSinceStartup`;
  - `Application.isPlaying`.
- Assertions verify the first probe can inspect live Unity scene data and sees at least one scene root and camera.
- After a 1500 ms delay, the test probes again and verifies the active scene is still loaded, frame count advanced, and realtime advanced.
- Cleanup shuts down through the harness and collects logs under `.scratch/mod-test-tools-artifacts/main-thread-eval`.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `MainThreadEvalRegressionTests`: passed, 1/1, duration 14s.
- Notes:
  - The first attempt assumed the scene name would stay constant between samples; the game naturally transitioned from `Init` to `Title`, so the final assertion allows normal boot scene transitions while still requiring loaded scene state and frame/time progression.
  - The selected invariant is intentionally broad: eval can safely inspect Unity scene roots/cameras and the game continues advancing afterward.

## 2026-06-01 - Issue 07: test hook config and launch options regression

- Added `TestHookLaunchOptions` and an overload `TestHarness.LaunchAsync(TimeSpan, TestHookLaunchOptions, CancellationToken)`.
- The harness option path writes a temporary `UserData/TestHookMod/config.json` before starting MajdataPlay, waits for readiness/health, then restores the prior config file state before returning the `HarnessRun`.
- Existing callers still use `LaunchAsync(TimeSpan, CancellationToken)` unchanged; it delegates to the new overload with no options.
- Added `TestHookConfigRegressionTests.HarnessCanLaunchWithTemporaryConfigFileOverrideAndRestoreIt`.
- The test launches with:
  - `BridgeHost = "127.0.0.1"`;
  - `BridgePort = 17444`;
  - `ReplEnabled = false`.
- Assertions verify:
  - any pre-existing config file is restored, or the temporary config file is removed if none existed;
  - readiness and `/health` both report host `127.0.0.1`, port `17444`, and `replEnabled:false`;
  - no `ModTestReplClient` process is launched when REPL is disabled.
- Cleanup restores config state again defensively, shuts down through the harness, and collects logs under `.scratch/mod-test-tools-artifacts/test-hook-config`.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `TestHookConfigRegressionTests`: passed, 1/1, duration 4s.
- Notes:
  - The config compatibility path remains `UserData/TestHookMod/config.json`; the harness does not delete or move the containing directory.
  - The temporary config is restored immediately after readiness because Test Hook Mod reads it during startup; keeping it around for the whole run is unnecessary and would make side effects easier to leak.

## 2026-06-01 - Issue 08: test hook shutdown regression

- Added `TestHookShutdownRegressionTests.ShutdownEndpointReportsFallbackPidAndStopsRuntimeProcesses`.
- The test launches the real game through the harness, then calls `/shutdown` directly through `TestClient.ShutdownAsync`.
- The structured shutdown response assertions verify:
  - HTTP 200;
  - `ok:true`;
  - `phase:"shutdown"`;
  - `accepted:true`;
  - `fallbackPid` equals the readiness file PID.
- After the direct shutdown request, the test waits through `TestHarness.WaitForReadinessPidExitAsync`, applies the existing safe runtime-process cleanup path, and asserts no `MajdataPlay` or `ModTestReplClient` processes remain.
- Cleanup only calls `HarnessRun.ShutdownOrKillAsync` if the direct shutdown request was not sent; otherwise it just disposes the client, stops runtime processes defensively, and collects logs under `.scratch/mod-test-tools-artifacts/test-hook-shutdown`.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `TestHookShutdownRegressionTests`: passed, 1/1, duration 3s.
- Notes:
  - This test intentionally bypasses the harness convenience shutdown method for the main assertion so the `/shutdown` response contract is directly covered.
  - The existing fallback process cleanup remains part of the test path, matching how the harness protects later runs from leaked Windows processes.

## 2026-06-01 - Issue 09: sample mod invariant regression tests

- Expanded `SampleModIntegrationTests.SampleModInvariantSuitePassesInMajdataPlay` from a single core eval smoke test into an invariant suite.
- The suite verifies inside MajdataPlay:
  - `TestMod` assembly is loaded;
  - `TestMod.Core` assembly is loaded;
  - `TestMod.TestMod` is discoverable through reflection;
  - `TestMod.Core.TestModLogic` is discoverable through reflection;
  - `StartupMessage()` returns `Loaded`;
  - `AddScore(100, 25)` returns `125`;
  - `AddScore(100, -5)` keeps the score at `100`.
- The test always shuts down and collects `.scratch/mod-test-tools-artifacts/integration` logs, then scans `*.log` artifacts for sample-mod startup failures and common unhandled exception shapes including `TestMod failed`, `Unhandled Exception`, `NullReferenceException`, type/load failures, and method failures.
- Historical validation from the implementation commit:
  - `dotnet build` for `mod-test-tools/integration/ModTest.Integration.Tests.csproj`: passed with 0 warnings and 0 errors.
  - `sample-mod/test-integration.ps1`: passed 1/1 xUnit test in 8s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - The install step still emitted the existing REPL `SYSLIB0014` warning from `WebRequest.Create`.
  - Downstream log/error canaries needed a broader game/MelonLoader fatal-error taxonomy than this sample-mod-focused scan.

## 2026-06-01 - Issue 10: game boot stable state canary

- Added `GameBootCanaryTests.GameBootReachesStableRunningUnityState`.
- Added shared integration eval/log assertions and disabled xUnit parallelization for the real-game integration assembly because tests share one MajdataPlay install and bridge port.
- The boot canary launches through the harness, polls `/eval-isolated`, and waits for two consecutive samples with:
  - same non-empty active scene;
  - loaded scene state;
  - advancing `UnityEngine.Time.frameCount`;
  - increasing realtime;
  - `Application.isPlaying`;
  - present/writable `Time.timeScale` API.
- The test shuts down and scans `.scratch/mod-test-tools-artifacts/boot-stable-state/*.log` for fatal boot lines.
- Quality fixes made while implementing the canary:
  - `TestMod.Core.dll` installs under `Mods/TestModLib` with a `TestMod` assembly resolver instead of the root `Mods` folder, eliminating MelonLoader's support-DLL load error;
  - Test Hook Mod treats missing `UnityEngine.Application.Quit` as an expected fallback path and relies on forced process termination without logging `MissingMethodException`;
  - `ModTestHarness` serializes runtime ownership by killing stale `MajdataPlay`/REPL processes before launch, retrying locked readiness-file cleanup, waiting for shutdown, and cleaning runtime processes after each test.
- Historical validation from the implementation commit:
  - `dotnet build mod-test-tools/integration/ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - `sample-mod/test-integration.ps1`: passed 2/2 xUnit tests in 24s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - This Unity profile exposes `Time.timeScale` as writable but not readable in managed metadata, so the canary verifies API presence/writability and uses frame/realtime advancement for the actual liveness signal.
  - `.log.old` files may retain earlier failed-run lines, but current `*.log` artifacts were clean during implementation.

## 2026-06-01 - Issue 11: core assemblies and game types canary

- Added `GameTypeCanaryTests.CoreAssembliesAndRepresentativeGameTypesAreLoadable`.
- The canary runs a non-mutating AppDomain/type-resolution probe inside MajdataPlay through `/eval-isolated`.
- It verifies 5 representative assemblies are loaded:
  - `Assembly-CSharp`;
  - `MajSimai`;
  - `UnityEngine.CoreModule`;
  - `Unity.InputSystem`;
  - `ManagedBass`.
- It resolves 9 representative types across manager/input/audio/chart/gameplay surfaces:
  - `MajdataPlay.GameManager`;
  - `MajdataPlay.IO.InputManager`;
  - `MajdataPlay.IO.AudioManager`;
  - `MajdataPlay.Settings.ChartSetting`;
  - `MajSimai.SimaiParser`;
  - `MajdataPlay.Scenes.Game.GamePlayManager`;
  - `MajdataPlay.Scenes.Game.NoteLoader`;
  - `MajdataPlay.Scenes.Game.Notes.Controllers.NoteManager`;
  - `MajdataPlay.Scenes.Game.Buffers.NoteInfo`.
- Missing assembly/type failures are returned as newline-delimited names in assertion messages, and the test does not instantiate game types or call their methods.
- Historical validation from the implementation commit:
  - `dotnet build mod-test-tools/integration/ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - `sample-mod/test-integration.ps1`: passed 3/3 serialized real-game xUnit tests in 43s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - This canary deliberately checks loadability and type names only; behavior of those systems is left to the focused input/audio/chart/gameplay canaries.

## 2026-06-01 - Issue 12: scene object invariant canary

- Added `SceneObjectCanaryTests.BootedSceneHasBroadUnityAndMajdataObjectInvariants`.
- The test is a read-only live-scene probe using `UnityEngine.Resources.FindObjectsOfTypeAll` and active scene roots after boot.
- It verifies broad minimums instead of exact counts:
  - non-empty active scene name;
  - at least one active root object;
  - at least one active/enabled camera;
  - at least one active/enabled audio listener;
  - active UI surface via canvas or event system;
  - active `MajdataPlay.MajComponent` instances;
  - at least one stable MajdataPlay root/controller component among `GameManager`, `TitleManager`, or `ListManager`.
- The test avoids instantiating objects, calling controller methods, or mutating scene state; it only counts active components.
- Historical validation from the implementation commit:
  - `dotnet build mod-test-tools/integration/ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - `sample-mod/test-integration.ps1`: passed 4/4 serialized real-game xUnit tests in 46s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - This canary intentionally records no brittle exact object counts.
  - Downstream UI/gameplay tests should add focused assertions only after intentionally navigating to a specific scene.

## 2026-06-01 - Issue 13: game asset and data load canary

- Added `GameAssetDataCanaryTests.KnownChartMetadataAndAssetPathsResolveAfterBoot`.
- The canary is a read-only metadata/path check centered on the stable local/built-in `MAJTITLE` chart.
- It waits up to 60s for TitleManager's delayed chart scan to populate `SongStorage`, then verifies:
  - `SongStorage` has collections;
  - at least one non-empty collection;
  - non-zero `TotalChartCount`;
  - enumerable songs;
  - a `MAJTITLE` metadata record;
  - artist `bbben`;
  - non-empty hash;
  - at least 5 levels.
- It verifies concrete local and built-in asset paths:
  - `MaiCharts/Original/MAJTITLE/maidata.txt`;
  - `MaiCharts/Original/MAJTITLE/track.mp3`;
  - `MaiCharts/Original/MAJTITLE/bg.png`;
  - built-in `StreamingAssets/MaiCharts/Original/MAJTITLE/maidata.txt`;
  - built-in `StreamingAssets/MaiCharts/Original/MAJTITLE/track.opus`;
  - `Skins/default/TapSkins/tap.png`.
- The local `maidata.txt` is read only to confirm `&title=MAJTITLE`.
- Historical validation from the implementation commit:
  - `dotnet build mod-test-tools/integration/ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - `sample-mod/test-integration.ps1`: passed 5/5 serialized real-game xUnit tests in 1m24s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - Bridge readiness happens before `SongStorage.InitAsync` completes, so the canary must poll for chart metadata readiness rather than sample immediately.
  - Downstream data/chart tests should keep that startup delay in mind.

## 2026-06-01 - Issue 14: chart parse canary

- Added `ChartParseCanaryTests.KnownGoodChartParsesThroughGameSongDetailPath`, using deterministic `MAJTITLE` from Issue 13.
- The test waits up to 75s for `SongStorage` to expose `MAJTITLE`, calls the game's normal `ISongDetail.GetMaidataAsync(true)` path, then parses each raw `Fumen` through `MajSimai.SimaiParser.ParseChartAsync` until it finds a chart with timing points.
- Assertions verify:
  - known song is found;
  - title is stable;
  - parse succeeds;
  - chart count is at least 5;
  - selected chart index is in the expected 0-4 range;
  - timing point count is nonzero;
  - note count is nonzero;
  - BPM timing count is nonzero;
  - first BPM is positive.
- Historical validation from the implementation commit:
  - `dotnet build mod-test-tools/integration/ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - `sample-mod/test-integration.ps1`: passed 6/6 serialized real-game xUnit tests in 1m35s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - `SimaiFile.Charts[i].Fumen` is a raw string, so the canary explicitly calls `SimaiParser.ParseChartAsync`.
  - The eval compiler cannot emit async state machines in this profile, so async game APIs are invoked with `GetAwaiter().GetResult()` inside a non-async eval snippet.
  - `ReadOnlySpan<SimaiTimingPoint>` required plain indexed loops rather than LINQ.

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

## 2026-06-01 - Issue 19: no persistent side effects canary

- Added `PersistentSideEffectsCanaryTests.BootRunDoesNotMutatePersistentUserConfigOrProfileFiles`.
- The canary snapshots a deliberately small set of persistent game/user state files before launch and after shutdown:
  - `settings.json`;
  - `Cache/Runtime/config.json`;
  - `ChartSetting.db`;
  - `MajScores.db`;
  - the legacy `MajDatabase.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db.db` score database path.
- Snapshot comparison is content-based, using existence, byte length, and SHA-256. File mtimes are ignored so harmless rewrites with identical content do not fail the canary.
- The test boots the real game, waits for a loaded scene plus ready `SongStorage`, shuts down through the harness, then reports any added, removed, or changed monitored path with before/after fingerprints.
- Expected logs, readiness files, REPL files, and integration artifacts are excluded by design because the canary monitors only the explicit persistent file allowlist instead of scanning runtime/log directories.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `PersistentSideEffectsCanaryTests`: passed, 1/1, duration 35s.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File './sample-mod/test-integration.ps1'`: passed, 11/11, duration 5m 25s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - The current candidate mods did not add, remove, or change any monitored persistent file during the boot probe.
  - The canary intentionally does not monitor `UserData/TestHookMod`, log files, or `.scratch` artifacts because those are expected test harness outputs.

## 2026-06-01 - Issue 20: menu FPS regression canary

- Added `MenuFpsRegressionCanaryTests.StableTitleMenuFrameTimingStaysWithinConservativeThresholds`.
- Added a hook-side `FrameTimingProbe` and `FrameTimingRecorder` in `TestHookMod`:
  - the probe creates a `MonoBehaviour` recorder on demand so samples are captured from Unity's frame update loop;
  - `Reset` arms a fixed sampling window and discards the first post-reset frame delta to avoid counting the eval/reset stall;
  - `Read` reports sample count, start/end frame, sampling window seconds, average FPS, p95 frame time, max frame time, and dropped-frame ratio using a 33.3 ms dropped-frame budget.
- The canary waits for a stable Title menu state before sampling:
  - Unity active scene is loaded;
  - `SceneSwitcher.CurrentScene` is `Title`;
  - `SongStorage` is ready;
  - frame count and realtime both advance across stable samples.
- Regression gates are intentionally conservative for local Windows game launches:
  - at least 120 samples over the five-second window;
  - frame count advances and the observed sampling window is at least four seconds;
  - average FPS is at least 30;
  - p95 frame time is at most 50 ms;
  - dropped-frame ratio is at most 10%.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for `MenuFpsRegressionCanaryTests`: passed, 1/1, duration 56s.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File './sample-mod/test-integration.ps1'`: passed, 12/12, duration 5m 35s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - `Application.targetFrameRate` and `QualitySettings.vSyncCount` are not readable through the Unity reference assemblies available to the hook compiler, so the canary relies on sampled `Time.unscaledDeltaTime` metrics instead.
  - Isolated max-frame spikes were observed during local launches while p95 and dropped-frame ratio stayed healthy; max frame time is still reported in failure details, but it is not used as a standalone failure gate.

## 2026-06-01 - Issue 21: song FPS regression canary

- Added `SongFpsRegressionCanaryTests.KnownLocalChartGameplayFrameTimingStaysWithinConservativeThresholds`.
- The canary starts the same deterministic gameplay setup used by the dry-run canary:
  - local `MAJTITLE`;
  - `Easy` difficulty;
  - `Normal` game mode;
  - autoplay enabled;
  - global and track volume muted in memory and restored during cleanup.
- The test also asserts the current expected display/window settings before sampling:
  - configured resolution is `1080x1920`;
  - configured FPS limit is `120`;
  - configured VSync is enabled;
  - configured fullscreen mode is enabled;
  - actual `Screen.width`, `Screen.height`, and fullscreen state are captured in the setup payload.
- Sampling begins only after the `Game` scene is active, `GamePlayManager.State` is `Running`, the known game info is visible, gameplay audio has a positive length, and note/object counts are nonzero.
- Gameplay regression gates use conservative local defaults:
  - at least 120 samples over the five-second window;
  - frame count advances and the observed sampling window is at least four seconds;
  - average FPS is at least 30;
  - p95 frame time is at most 75 ms;
  - dropped-frame ratio is at most 20%.
- Adjusted the shared `FrameTimingProbe` average FPS calculation to use `sampleCount / realtimeWindow` instead of summing raw `Time.unscaledDeltaTime` values. This keeps average FPS aligned with the fixed sampling window while still reporting p95, max frame time, and dropped-frame ratio from the raw Unity frame deltas.
- Validation:
  - `dotnet build .\mod-test-tools\integration\ModTest.Integration.Tests.csproj -c Release --nologo`: passed with 0 warnings and 0 errors.
  - Filtered run for both FPS canaries: passed, 2/2, duration 2m 06s.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File './sample-mod/test-integration.ps1'`: passed, 13/13, duration 7m 04s.
  - Integration TRX artifact: `.scratch/mod-test-tools-artifacts/integration-test-results/integration.trx`.
- Notes:
  - `Screen.fullScreenMode` is not readable through the eval compiler's Unity reference assemblies, so the canary records actual fullscreen state plus configured display settings instead.
  - The full 13-test suite passed after the shared sampler average-FPS correction; the song FPS canary itself also passed in the earlier full run before that correction.
