using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class GameplayDryRunCanaryTests
    {
        [Fact]
        public async Task KnownLocalChartCanEnterGameplayAndAdvanceTime()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            JsonElement? setupState = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                await WaitForGameplayPrerequisitesAsync(run.Client, TimeSpan.FromSeconds(75));
                setupState = await StartKnownGameplayAsync(run.Client);

                Assert.True(setupState.Value.GetProperty("SwitchRequested").GetBoolean(), "Game scene switch should be requested.");
                Assert.Equal("MAJTITLE", setupState.Value.GetProperty("SelectedSongTitle").GetString());
                Assert.Equal("Normal", setupState.Value.GetProperty("GameMode").GetString());
                Assert.Equal("Easy", setupState.Value.GetProperty("SelectedLevel").GetString());
                Assert.Equal("Enable", setupState.Value.GetProperty("RuntimeAutoPlay").GetString());

                JsonElement running = await WaitForGameplayStateAsync(
                    run.Client,
                    TimeSpan.FromSeconds(90),
                    state => state.GetProperty("CurrentScene").GetString() == "Game"
                        && state.GetProperty("ActiveGamePlayManagerCount").GetInt32() > 0
                        && state.GetProperty("GamePlayState").GetString() == "Running"
                        && state.GetProperty("NoteLoaderNoteCount").GetInt64() > 0
                        && state.GetProperty("ObjectCounterNoteSum").GetInt32() > 0);

                await Task.Delay(2500);
                JsonElement advanced = await ReadGameplayStateAsync(run.Client);

                Assert.Equal("Game", running.GetProperty("CurrentScene").GetString());
                Assert.Equal("Game", running.GetProperty("ActiveSceneName").GetString());
                Assert.True(running.GetProperty("ActiveNoteManagerCount").GetInt32() > 0, "NoteManager should be active in gameplay.");
                Assert.True(running.GetProperty("ActiveObjectCounterCount").GetInt32() > 0, "ObjectCounter should be active in gameplay.");
                Assert.True(running.GetProperty("ActiveNotePoolManagerCount").GetInt32() > 0, "NotePoolManager should be active in gameplay.");
                Assert.Equal("Running", running.GetProperty("NotePoolManagerState").GetString());
                Assert.True(running.GetProperty("AudioLength").GetSingle() > 0, "Gameplay should load a positive-length audio track.");
                Assert.True(running.GetProperty("IsStart").GetBoolean(), "Gameplay should mark play as started.");
                Assert.True(running.GetProperty("IsAutoplay").GetBoolean(), "Dry run should use autoplay for deterministic no-input execution.");
                Assert.Equal("Enable", running.GetProperty("AutoplayMode").GetString());
                Assert.Equal("MAJTITLE", running.GetProperty("GameInfoSongTitle").GetString());
                Assert.Equal("Easy", running.GetProperty("GameInfoLevel").GetString());
                Assert.True(advanced.GetProperty("FrameCount").GetInt32() > running.GetProperty("FrameCount").GetInt32());
                Assert.True(advanced.GetProperty("ThisFrameSec").GetSingle() > running.GetProperty("ThisFrameSec").GetSingle(), "Gameplay time should advance after entering Running.");

                JsonElement returned = await RestoreSettingsAndReturnToListAsync(run.Client, setupState.Value);
                Assert.True(returned.GetProperty("SwitchRequested").GetBoolean(), "List scene switch should be requested during cleanup.");
                JsonElement listState = await WaitForGameplayStateAsync(
                    run.Client,
                    TimeSpan.FromSeconds(30),
                    state => state.GetProperty("CurrentScene").GetString() == "List");
                Assert.Equal("List", listState.GetProperty("CurrentScene").GetString());
            }
            catch (Exception ex)
            {
                scenarioFailure = ex;
            }
            finally
            {
                if (run != null)
                {
                    if (setupState.HasValue)
                    {
                        await Record.ExceptionAsync(() => RestoreSettingsAndReturnToListAsync(run.Client, setupState.Value));
                    }

                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("gameplay-dry-run");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "gameplay-dry-run")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }

        private static async Task<JsonElement> WaitForGameplayPrerequisitesAsync(TestClient client, TimeSpan timeout)
        {
            return await WaitForGameplayStateAsync(
                client,
                timeout,
                state => state.GetProperty("CurrentScene").GetString() == "Title"
                    && state.GetProperty("SongStorageReady").GetBoolean()
                    && state.GetProperty("KnownSongFound").GetBoolean());
        }

        private static async Task<JsonElement> WaitForGameplayStateAsync(TestClient client, TimeSpan timeout, Func<JsonElement, bool> predicate)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            JsonElement latest = default;
            while (DateTimeOffset.UtcNow < deadline)
            {
                latest = await ReadGameplayStateAsync(client);
                if (predicate(latest))
                {
                    return latest;
                }

                await Task.Delay(1000);
            }

            throw new TimeoutException("Timed out waiting for gameplay state. Last state: " + latest);
        }

        private static async Task<JsonElement> ReadGameplayStateAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(GameplayStateEval);
            return IntegrationAssertions.EvalResultProperties(response);
        }

        private static async Task<JsonElement> StartKnownGameplayAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    const System.Reflection.BindingFlags StaticFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    const System.Reflection.BindingFlags InstanceFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    Type sceneSwitcherType = Type.GetType(""MajdataPlay.SceneSwitcher, Assembly-CSharp"", true);
    Type majEnvType = Type.GetType(""MajdataPlay.MajEnv, Assembly-CSharp"", true);
    Type gameInfoType = Type.GetType(""MajdataPlay.Scenes.Game.GameInfo, Assembly-CSharp"", true);
    Type gameModeType = Type.GetType(""MajdataPlay.Scenes.Game.GameMode, Assembly-CSharp"", true);
    Type songDetailType = Type.GetType(""MajdataPlay.ISongDetail, Assembly-CSharp"", true);
    Type chartLevelType = Type.GetType(""MajdataPlay.ChartLevel, Assembly-CSharp"", true);
    Type autoplayType = Type.GetType(""MajdataPlay.Settings.AutoplayModeOption, Assembly-CSharp"", true);
    Type majdataOpenType = Type.GetType(""MajdataPlay.Majdata`1, Assembly-CSharp"", true);

    object settings = majEnvType.GetProperty(""Settings"", StaticFlags).GetValue(null);
    object modSettings = settings.GetType().GetProperty(""Mod"", InstanceFlags).GetValue(settings);
    object audioSettings = settings.GetType().GetProperty(""Audio"", InstanceFlags).GetValue(settings);
    object volumeSettings = audioSettings.GetType().GetProperty(""Volume"", InstanceFlags).GetValue(audioSettings);
    var autoplayProperty = modSettings.GetType().GetProperty(""AutoPlay"", InstanceFlags);
    var globalVolumeProperty = volumeSettings.GetType().GetProperty(""Global"", InstanceFlags);
    var trackVolumeProperty = volumeSettings.GetType().GetProperty(""Track"", InstanceFlags);
    object originalAutoPlay = autoplayProperty.GetValue(modSettings);
    float originalGlobalVolume = Convert.ToSingle(globalVolumeProperty.GetValue(volumeSettings));
    float originalTrackVolume = Convert.ToSingle(trackVolumeProperty.GetValue(volumeSettings));

    autoplayProperty.SetValue(modSettings, Enum.Parse(autoplayType, ""Enable""));
    globalVolumeProperty.SetValue(volumeSettings, 0f);
    trackVolumeProperty.SetValue(volumeSettings, 0f);

    var collections = MajdataPlay.SongStorage.Collections ?? Array.Empty<MajdataPlay.Collections.SongCollection>();
    object selectedSong = collections.SelectMany(collection => collection.ToArray()).FirstOrDefault(song => song.Title == ""MAJTITLE"");
    if (selectedSong == null) {
        throw new InvalidOperationException(""Known MAJTITLE song was not available in SongStorage."");
    }

    Array charts = Array.CreateInstance(songDetailType, 1);
    charts.SetValue(selectedSong, 0);
    Array levels = Array.CreateInstance(chartLevelType, 1);
    object selectedLevel = Enum.Parse(chartLevelType, ""Easy"");
    levels.SetValue(selectedLevel, 0);
    object gameInfo = Activator.CreateInstance(gameInfoType, new object[] { Enum.Parse(gameModeType, ""Normal""), charts, levels });
    Type majdataGameInfoType = majdataOpenType.MakeGenericType(gameInfoType);
    majdataGameInfoType.GetField(""_instance"", StaticFlags).SetValue(null, gameInfo);

    object switcher = UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);

    bool switchRequested = false;
    if (switcher != null) {
        sceneSwitcherType.GetMethod(""SwitchScene"", InstanceFlags).Invoke(switcher, new object[] { ""Game"", false });
        switchRequested = true;
    }

    return new {
        SwitchRequested = switchRequested,
        SelectedSongTitle = ((MajdataPlay.ISongDetail)selectedSong).Title,
        GameMode = gameInfoType.GetProperty(""Mode"", InstanceFlags).GetValue(gameInfo).ToString(),
        SelectedLevel = selectedLevel.ToString(),
        RuntimeAutoPlay = autoplayProperty.GetValue(modSettings).ToString(),
        OriginalAutoPlay = originalAutoPlay.ToString(),
        OriginalGlobalVolume = originalGlobalVolume,
        OriginalTrackVolume = originalTrackVolume
    };
})()");

            return IntegrationAssertions.EvalResultProperties(response);
        }

        private static async Task<JsonElement> RestoreSettingsAndReturnToListAsync(TestClient client, JsonElement setupState)
        {
            string originalAutoPlay = setupState.GetProperty("OriginalAutoPlay").GetString();
            float originalGlobalVolume = setupState.GetProperty("OriginalGlobalVolume").GetSingle();
            float originalTrackVolume = setupState.GetProperty("OriginalTrackVolume").GetSingle();
            string code = @"new Func<object>(() => {
    const System.Reflection.BindingFlags StaticFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    const System.Reflection.BindingFlags InstanceFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    Type sceneSwitcherType = Type.GetType(""MajdataPlay.SceneSwitcher, Assembly-CSharp"", true);
    Type majEnvType = Type.GetType(""MajdataPlay.MajEnv, Assembly-CSharp"", true);
    Type autoplayType = Type.GetType(""MajdataPlay.Settings.AutoplayModeOption, Assembly-CSharp"", true);
    object settings = majEnvType.GetProperty(""Settings"", StaticFlags).GetValue(null);
    object modSettings = settings.GetType().GetProperty(""Mod"", InstanceFlags).GetValue(settings);
    object audioSettings = settings.GetType().GetProperty(""Audio"", InstanceFlags).GetValue(settings);
    object volumeSettings = audioSettings.GetType().GetProperty(""Volume"", InstanceFlags).GetValue(audioSettings);
    modSettings.GetType().GetProperty(""AutoPlay"", InstanceFlags).SetValue(modSettings, Enum.Parse(autoplayType, """ + originalAutoPlay + @"""));
    volumeSettings.GetType().GetProperty(""Global"", InstanceFlags).SetValue(volumeSettings, " + originalGlobalVolume.ToString(System.Globalization.CultureInfo.InvariantCulture) + @"f);
    volumeSettings.GetType().GetProperty(""Track"", InstanceFlags).SetValue(volumeSettings, " + originalTrackVolume.ToString(System.Globalization.CultureInfo.InvariantCulture) + @"f);

    object switcher = UnityEngine.Resources.FindObjectsOfTypeAll(sceneSwitcherType)
        .OfType<UnityEngine.Component>()
        .FirstOrDefault(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);

    bool switchRequested = false;
    if (switcher != null) {
        sceneSwitcherType.GetMethod(""SwitchScene"", InstanceFlags).Invoke(switcher, new object[] { ""List"", true });
        switchRequested = true;
    }

    return new {
        SwitchRequested = switchRequested,
        RestoredAutoPlay = modSettings.GetType().GetProperty(""AutoPlay"", InstanceFlags).GetValue(modSettings).ToString(),
        RestoredGlobalVolume = Convert.ToSingle(volumeSettings.GetType().GetProperty(""Global"", InstanceFlags).GetValue(volumeSettings)),
        RestoredTrackVolume = Convert.ToSingle(volumeSettings.GetType().GetProperty(""Track"", InstanceFlags).GetValue(volumeSettings))
    };
})()";

            BridgeResponse response = await client.EvalIsolatedAsync(code);
            return IntegrationAssertions.EvalResultProperties(response);
        }

        private const string GameplayStateEval = @"new Func<object>(() => {
    const System.Reflection.BindingFlags StaticFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    const System.Reflection.BindingFlags InstanceFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    int countActiveComponents(Type type) {
        if (type == null) {
            return 0;
        }

        return UnityEngine.Resources.FindObjectsOfTypeAll(type)
            .OfType<UnityEngine.Component>()
            .Count(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);
    }

    object firstActiveComponent(Type type) {
        if (type == null) {
            return null;
        }

        return UnityEngine.Resources.FindObjectsOfTypeAll(type)
            .OfType<UnityEngine.Component>()
            .FirstOrDefault(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);
    }

    Type sceneSwitcherType = Type.GetType(""MajdataPlay.SceneSwitcher, Assembly-CSharp"", true);
    Type gamePlayManagerType = Type.GetType(""MajdataPlay.Scenes.Game.GamePlayManager, Assembly-CSharp"", false);
    Type noteManagerType = Type.GetType(""MajdataPlay.Scenes.Game.Notes.Controllers.NoteManager, Assembly-CSharp"", false);
    Type notePoolManagerType = Type.GetType(""MajdataPlay.Scenes.Game.Notes.Controllers.NotePoolManager, Assembly-CSharp"", false);
    Type noteLoaderType = Type.GetType(""MajdataPlay.Scenes.Game.NoteLoader, Assembly-CSharp"", false);
    Type objectCounterType = Type.GetType(""MajdataPlay.Scenes.Game.ObjectCounter, Assembly-CSharp"", false);
    Type gameInfoType = Type.GetType(""MajdataPlay.Scenes.Game.GameInfo, Assembly-CSharp"", false);
    Type majdataOpenType = Type.GetType(""MajdataPlay.Majdata`1, Assembly-CSharp"", true);
    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    object gamePlayManager = firstActiveComponent(gamePlayManagerType);
    object notePoolManager = firstActiveComponent(notePoolManagerType);
    object noteLoader = firstActiveComponent(noteLoaderType);
    object objectCounter = firstActiveComponent(objectCounterType);

    string gameInfoSongTitle = string.Empty;
    string gameInfoLevel = string.Empty;
    string gameInfoMode = string.Empty;
    if (gameInfoType != null) {
        Type majdataGameInfoType = majdataOpenType.MakeGenericType(gameInfoType);
        object gameInfo = majdataGameInfoType.GetField(""_instance"", StaticFlags).GetValue(null);
        if (gameInfo != null) {
            gameInfoSongTitle = ((MajdataPlay.ISongDetail)gameInfoType.GetProperty(""Current"", InstanceFlags).GetValue(gameInfo))?.Title ?? string.Empty;
            gameInfoLevel = gameInfoType.GetProperty(""CurrentLevel"", InstanceFlags).GetValue(gameInfo).ToString();
            gameInfoMode = gameInfoType.GetProperty(""Mode"", InstanceFlags).GetValue(gameInfo).ToString();
        }
    }

    var collections = MajdataPlay.SongStorage.Collections ?? Array.Empty<MajdataPlay.Collections.SongCollection>();
    bool knownSongFound = collections.SelectMany(collection => collection.ToArray()).Any(song => song.Title == ""MAJTITLE"");

    return new {
        ActiveSceneName = activeScene.name,
        ActiveSceneHandle = activeScene.handle,
        ActiveSceneLoaded = activeScene.isLoaded,
        CurrentScene = sceneSwitcherType.GetProperty(""CurrentScene"", StaticFlags).GetValue(null).ToString(),
        LastScene = sceneSwitcherType.GetProperty(""LastScene"", StaticFlags).GetValue(null).ToString(),
        FrameCount = UnityEngine.Time.frameCount,
        SongStorageReady = collections.Length > 0 && !MajdataPlay.SongStorage.IsEmpty,
        KnownSongFound = knownSongFound,
        ActiveGamePlayManagerCount = countActiveComponents(gamePlayManagerType),
        ActiveNoteManagerCount = countActiveComponents(noteManagerType),
        ActiveNotePoolManagerCount = countActiveComponents(notePoolManagerType),
        ActiveNoteLoaderCount = countActiveComponents(noteLoaderType),
        ActiveObjectCounterCount = countActiveComponents(objectCounterType),
        GamePlayState = gamePlayManager == null ? string.Empty : gamePlayManagerType.GetProperty(""State"", InstanceFlags).GetValue(gamePlayManager).ToString(),
        ThisFrameSec = gamePlayManager == null ? 0f : Convert.ToSingle(gamePlayManagerType.GetProperty(""ThisFrameSec"", InstanceFlags).GetValue(gamePlayManager)),
        AudioLength = gamePlayManager == null ? 0f : Convert.ToSingle(gamePlayManagerType.GetProperty(""AudioLength"", InstanceFlags).GetValue(gamePlayManager)),
        IsStart = gamePlayManager != null && Convert.ToBoolean(gamePlayManagerType.GetProperty(""IsStart"", InstanceFlags).GetValue(gamePlayManager)),
        IsAutoplay = gamePlayManager != null && Convert.ToBoolean(gamePlayManagerType.GetProperty(""IsAutoplay"", InstanceFlags).GetValue(gamePlayManager)),
        AutoplayMode = gamePlayManager == null ? string.Empty : gamePlayManagerType.GetProperty(""AutoplayMode"", InstanceFlags).GetValue(gamePlayManager).ToString(),
        NotePoolManagerState = notePoolManager == null ? string.Empty : notePoolManagerType.GetProperty(""State"", InstanceFlags).GetValue(notePoolManager).ToString(),
        NoteLoaderNoteCount = noteLoader == null ? 0L : Convert.ToInt64(noteLoaderType.GetProperty(""NoteCount"", InstanceFlags).GetValue(noteLoader)),
        NoteLoaderProgress = noteLoader == null ? 0d : Convert.ToDouble(noteLoaderType.GetProperty(""Progress"", InstanceFlags).GetValue(noteLoader)),
        ObjectCounterNoteSum = objectCounter == null ? 0 : Convert.ToInt32(objectCounterType.GetProperty(""NoteSum"", InstanceFlags).GetValue(objectCounter)),
        ObjectCounterTapSum = objectCounter == null ? 0 : Convert.ToInt32(objectCounterType.GetProperty(""TapSum"", InstanceFlags).GetValue(objectCounter)),
        ObjectCounterHoldSum = objectCounter == null ? 0 : Convert.ToInt32(objectCounterType.GetProperty(""HoldSum"", InstanceFlags).GetValue(objectCounter)),
        ObjectCounterSlideSum = objectCounter == null ? 0 : Convert.ToInt32(objectCounterType.GetProperty(""SlideSum"", InstanceFlags).GetValue(objectCounter)),
        ObjectCounterTouchSum = objectCounter == null ? 0 : Convert.ToInt32(objectCounterType.GetProperty(""TouchSum"", InstanceFlags).GetValue(objectCounter)),
        GameInfoSongTitle = gameInfoSongTitle,
        GameInfoLevel = gameInfoLevel,
        GameInfoMode = gameInfoMode
    };
})()";
    }
}
