using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class AudioSystemCanaryTests
    {
        [Fact]
        public async Task BootedGameHasInitializedSaneAudioState()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                BridgeResponse response = await run.Client.EvalIsolatedAsync(@"new Func<object>(() => {
    const System.Reflection.BindingFlags StaticFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    const System.Reflection.BindingFlags InstanceFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    float readFloatProperty(object target, string name) {
        return Convert.ToSingle(target.GetType().GetProperty(name, InstanceFlags).GetValue(target));
    }

    Type audioManagerType = Type.GetType(""MajdataPlay.IO.AudioManager, Assembly-CSharp"", true);
    var audioManagers = UnityEngine.Resources.FindObjectsOfTypeAll(audioManagerType)
        .OfType<UnityEngine.Component>()
        .Where(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy)
        .ToArray();
    object audioManager = audioManagers.FirstOrDefault();

    int activeAudioListenerCount = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(UnityEngine.AudioListener))
        .OfType<UnityEngine.AudioListener>()
        .Count(listener => listener != null && listener.isActiveAndEnabled);
    int activeAudioSourceCount = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(UnityEngine.AudioSource))
        .OfType<UnityEngine.AudioSource>()
        .Count(source => source != null && source.gameObject != null && source.gameObject.activeInHierarchy);

    Type majEnvType = Type.GetType(""MajdataPlay.MajEnv, Assembly-CSharp"", true);
    object settings = majEnvType.GetProperty(""Settings"", StaticFlags).GetValue(null);
    object audioSettings = settings.GetType().GetProperty(""Audio"", InstanceFlags).GetValue(settings);
    object volumeSettings = audioSettings.GetType().GetProperty(""Volume"", InstanceFlags).GetValue(audioSettings);
    string backend = audioSettings.GetType().GetProperty(""Backend"", InstanceFlags).GetValue(audioSettings).ToString();

    float globalVolume = readFloatProperty(volumeSettings, ""Global"");
    float bgmVolume = readFloatProperty(volumeSettings, ""BGM"");
    float trackVolume = readFloatProperty(volumeSettings, ""Track"");
    float tapVolume = readFloatProperty(volumeSettings, ""Tap"");

    Array mixingMatrix = (Array)audioManagerType.GetProperty(""MixingMatrix"", StaticFlags).GetValue(null);
    int matrixRows = mixingMatrix.GetLength(0);
    int matrixColumns = mixingMatrix.GetLength(1);
    bool matrixFinite = true;
    bool matrixHasNonZero = false;
    float matrixMaxAbs = 0f;
    for (int row = 0; row < matrixRows; row++) {
        for (int column = 0; column < matrixColumns; column++) {
            float value = Convert.ToSingle(mixingMatrix.GetValue(row, column));
            matrixFinite &= !float.IsNaN(value) && !float.IsInfinity(value);
            matrixHasNonZero |= Math.Abs(value) > 0.0001f;
            matrixMaxAbs = Math.Max(matrixMaxAbs, Math.Abs(value));
        }
    }

    int cachedSfxCount = 0;
    int nonEmptyCachedSfxCount = 0;
    bool tapPerfectPresent = false;
    bool tapPerfectIsEmpty = true;
    double tapPerfectLengthSeconds = 0;
    bool tapPerfectPlaying = false;
    bool loadedClipIsEmpty = true;
    double loadedClipLengthSeconds = 0;
    bool loadedClipPlaying = true;
    string clipPath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, ""SFX"", ""tap_perfect.wav"");
    bool clipFileExists = System.IO.File.Exists(clipPath);

    if (audioManager != null) {
        var samplesField = audioManagerType.GetField(""SFXSamples"", InstanceFlags);
        var samples = samplesField.GetValue(audioManager) as System.Collections.IEnumerable;
        foreach (object sample in samples) {
            cachedSfxCount++;
            bool isEmpty = Convert.ToBoolean(sample.GetType().GetProperty(""IsEmpty"", InstanceFlags).GetValue(sample));
            if (!isEmpty) {
                nonEmptyCachedSfxCount++;
            }
        }

        object tapPerfect = audioManagerType.GetMethod(""GetSFX"", InstanceFlags).Invoke(audioManager, new object[] { ""tap_perfect.wav"" });
        tapPerfectPresent = tapPerfect != null;
        if (tapPerfect != null) {
            tapPerfectIsEmpty = Convert.ToBoolean(tapPerfect.GetType().GetProperty(""IsEmpty"", InstanceFlags).GetValue(tapPerfect));
            tapPerfectPlaying = Convert.ToBoolean(tapPerfect.GetType().GetProperty(""IsPlaying"", InstanceFlags).GetValue(tapPerfect));
            var length = (TimeSpan)tapPerfect.GetType().GetProperty(""Length"", InstanceFlags).GetValue(tapPerfect);
            tapPerfectLengthSeconds = length.TotalSeconds;
        }

        object loadedClip = audioManagerType.GetMethod(""LoadMusic"", InstanceFlags).Invoke(audioManager, new object[] { clipPath, false, false });
        try {
            loadedClipIsEmpty = Convert.ToBoolean(loadedClip.GetType().GetProperty(""IsEmpty"", InstanceFlags).GetValue(loadedClip));
            loadedClipPlaying = Convert.ToBoolean(loadedClip.GetType().GetProperty(""IsPlaying"", InstanceFlags).GetValue(loadedClip));
            var loadedLength = (TimeSpan)loadedClip.GetType().GetProperty(""Length"", InstanceFlags).GetValue(loadedClip);
            loadedClipLengthSeconds = loadedLength.TotalSeconds;
            loadedClip.GetType().GetMethod(""Stop"", InstanceFlags).Invoke(loadedClip, Array.Empty<object>());
        }
        finally {
            loadedClip.GetType().GetMethod(""Dispose"", InstanceFlags).Invoke(loadedClip, Array.Empty<object>());
        }
    }

    return new {
        ActiveAudioListenerCount = activeAudioListenerCount,
        ActiveAudioSourceCount = activeAudioSourceCount,
        ActiveAudioManagerCount = audioManagers.Length,
        Backend = backend,
        GlobalVolume = globalVolume,
        BgmVolume = bgmVolume,
        TrackVolume = trackVolume,
        TapVolume = tapVolume,
        MixingMatrixRows = matrixRows,
        MixingMatrixColumns = matrixColumns,
        MixingMatrixFinite = matrixFinite,
        MixingMatrixHasNonZero = matrixHasNonZero,
        MixingMatrixMaxAbs = matrixMaxAbs,
        CachedSfxCount = cachedSfxCount,
        NonEmptyCachedSfxCount = nonEmptyCachedSfxCount,
        TapPerfectPresent = tapPerfectPresent,
        TapPerfectIsEmpty = tapPerfectIsEmpty,
        TapPerfectLengthSeconds = tapPerfectLengthSeconds,
        TapPerfectPlaying = tapPerfectPlaying,
        ClipFileExists = clipFileExists,
        LoadedClipIsEmpty = loadedClipIsEmpty,
        LoadedClipLengthSeconds = loadedClipLengthSeconds,
        LoadedClipPlaying = loadedClipPlaying
    };
})()");

                JsonElement properties = IntegrationAssertions.EvalResultProperties(response);
                Assert.True(properties.GetProperty("ActiveAudioListenerCount").GetInt32() > 0, "Boot should expose at least one active audio listener.");
                Assert.True(properties.GetProperty("ActiveAudioManagerCount").GetInt32() > 0, "MajdataPlay AudioManager should be active after boot.");
                Assert.False(string.IsNullOrWhiteSpace(properties.GetProperty("Backend").GetString()));
                Assert.InRange(properties.GetProperty("GlobalVolume").GetSingle(), 0f, 1f);
                Assert.InRange(properties.GetProperty("BgmVolume").GetSingle(), 0f, 1f);
                Assert.InRange(properties.GetProperty("TrackVolume").GetSingle(), 0f, 1f);
                Assert.InRange(properties.GetProperty("TapVolume").GetSingle(), 0f, 1f);
                Assert.True(properties.GetProperty("MixingMatrixRows").GetInt32() > 0, "Audio mixing matrix should have output rows.");
                Assert.Equal(2, properties.GetProperty("MixingMatrixColumns").GetInt32());
                Assert.True(properties.GetProperty("MixingMatrixFinite").GetBoolean(), "Audio mixing matrix should contain only finite values.");
                Assert.True(properties.GetProperty("MixingMatrixHasNonZero").GetBoolean(), "Audio mixing matrix should route at least one channel.");
                Assert.InRange(properties.GetProperty("MixingMatrixMaxAbs").GetSingle(), 0f, 2f);
                Assert.True(properties.GetProperty("CachedSfxCount").GetInt32() > 0, "AudioManager should cache boot SFX samples.");
                Assert.True(properties.GetProperty("NonEmptyCachedSfxCount").GetInt32() > 0, "AudioManager should have non-empty SFX samples.");
                Assert.True(properties.GetProperty("TapPerfectPresent").GetBoolean(), "Expected tap_perfect.wav in AudioManager SFX cache.");
                Assert.False(properties.GetProperty("TapPerfectIsEmpty").GetBoolean(), "tap_perfect.wav should be decoded as a non-empty sample.");
                Assert.True(properties.GetProperty("TapPerfectLengthSeconds").GetDouble() > 0, "tap_perfect.wav should report a positive duration.");
                Assert.False(properties.GetProperty("TapPerfectPlaying").GetBoolean(), "Inspection of tap_perfect.wav should not start playback.");
                Assert.True(properties.GetProperty("ClipFileExists").GetBoolean(), "Known short SFX clip should exist on disk.");
                Assert.False(properties.GetProperty("LoadedClipIsEmpty").GetBoolean(), "Loading a known SFX clip should produce a non-empty sample.");
                Assert.True(properties.GetProperty("LoadedClipLengthSeconds").GetDouble() > 0, "Loaded SFX clip should report a positive duration.");
                Assert.False(properties.GetProperty("LoadedClipPlaying").GetBoolean(), "Loaded SFX clip should not play unless explicitly started.");
            }
            catch (Exception ex)
            {
                scenarioFailure = ex;
            }
            finally
            {
                if (run != null)
                {
                    await run.ShutdownOrKillAsync();
                    run.Dispose();
                }

                harness.CollectLogs("audio-system");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "audio-system")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }
    }
}
