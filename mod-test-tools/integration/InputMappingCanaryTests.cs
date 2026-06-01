using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModTestHarness;
using Xunit;

namespace ModTestIntegration
{
    public sealed class InputMappingCanaryTests
    {
        [Fact]
        public async Task BootedGameHasInitializedInputMappingsAndRuntimeState()
        {
            string projectRoot = Environment.GetEnvironmentVariable("MODTEST_PROJECT_ROOT");
            Assert.False(string.IsNullOrWhiteSpace(projectRoot));

            TestHarness harness = new TestHarness(projectRoot);
            HarnessRun run = null;
            Exception scenarioFailure = null;
            try
            {
                run = await harness.LaunchAsync(TimeSpan.FromSeconds(90));
                JsonElement properties = await WaitForInputStateAsync(run.Client, TimeSpan.FromSeconds(60));

                Assert.True(properties.GetProperty("InputManagerInitialized").GetBoolean(), "InputManager should initialize after boot.");
                Assert.True(properties.GetProperty("ActiveGameUpdaterCount").GetInt32() > 0, "GameUpdater should be active and driving input pre-update.");
                Assert.True(properties.GetProperty("ActiveDummyTouchPanelRendererCount").GetInt32() > 0, "Dummy touch-panel renderer should be active for hardware-free touch mapping.");
                Assert.True(properties.GetProperty("DummyTouchMapCount").GetInt32() > 0, "Dummy touch-panel renderer should expose collider-to-sensor mappings.");
                Assert.Equal(12, properties.GetProperty("ButtonZoneCount").GetInt32());
                Assert.Equal(33, properties.GetProperty("SensorAreaCount").GetInt32());
                Assert.Equal(2, properties.GetProperty("SwitchStatusCount").GetInt32());
                Assert.Equal(12, properties.GetProperty("BindingKeyCount").GetInt32());
                Assert.Equal(12, properties.GetProperty("UniqueBindingKeyCount").GetInt32());
                Assert.Equal(12, properties.GetProperty("ButtonObjectCount").GetInt32());
                Assert.Equal(12, properties.GetProperty("UniqueButtonZoneCount").GetInt32());
                Assert.Equal(33, properties.GetProperty("SensorObjectCount").GetInt32());
                Assert.Equal(33, properties.GetProperty("UniqueSensorAreaCount").GetInt32());
                Assert.Equal(12, properties.GetProperty("ButtonStateArrayLength").GetInt32());
                Assert.Equal(12, properties.GetProperty("ButtonRealtimeStateArrayLength").GetInt32());
                Assert.Equal(35, properties.GetProperty("TouchSensorStateArrayLength").GetInt32());
                Assert.Equal(35, properties.GetProperty("TouchSensorRealtimeStateArrayLength").GetInt32());
                Assert.True(properties.GetProperty("UnitCircleLength").GetInt32() > 0, "InputManager should generate touch-angle samples.");
                Assert.InRange(properties.GetProperty("ButtonPollingRateMs").GetInt32(), 0, int.MaxValue);
                Assert.InRange(properties.GetProperty("ButtonDebounceThresholdMs").GetInt32(), 0, int.MaxValue);
                Assert.InRange(properties.GetProperty("TouchPollingRateMs").GetInt32(), 0, int.MaxValue);
                Assert.InRange(properties.GetProperty("TouchDebounceThresholdMs").GetInt32(), 0, int.MaxValue);
                Assert.True(properties.GetProperty("RepresentativeButtonStatusReadable").GetBoolean(), "Representative button status should be readable without hardware input.");
                Assert.True(properties.GetProperty("RepresentativeSensorStatusReadable").GetBoolean(), "Representative sensor status should be readable without touch-panel hardware.");
                Assert.False(string.IsNullOrWhiteSpace(properties.GetProperty("ButtonRingEnabledFromSettings").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(properties.GetProperty("TouchPanelEnabledFromSettings").GetString()));
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

                harness.CollectLogs("input-mapping");
            }

            Exception logFailure = Record.Exception(() => IntegrationAssertions.AssertNoFatalBootFailures(Path.Combine(harness.ArtifactsRoot, "input-mapping")));
            IntegrationAssertions.ThrowCombined(scenarioFailure, logFailure);
        }

        private static async Task<JsonElement> WaitForInputStateAsync(TestClient client, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            JsonElement latest = default;
            while (DateTimeOffset.UtcNow < deadline)
            {
                latest = await ReadInputStateAsync(client);
                if (latest.GetProperty("InputManagerInitialized").GetBoolean())
                {
                    return latest;
                }

                await Task.Delay(1000);
            }

            return latest;
        }

        private static async Task<JsonElement> ReadInputStateAsync(TestClient client)
        {
            BridgeResponse response = await client.EvalIsolatedAsync(@"new Func<object>(() => {
    const System.Reflection.BindingFlags StaticFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    const System.Reflection.BindingFlags InstanceFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    int readMemoryLength(object memory) {
        return Convert.ToInt32(memory.GetType().GetProperty(""Length"").GetValue(memory));
    }

    Array readMemoryArray(object memory) {
        return (Array)memory.GetType().GetMethod(""ToArray"").Invoke(memory, Array.Empty<object>());
    }

    int countActiveComponents(Type type) {
        if (type == null) {
            return 0;
        }

        return UnityEngine.Resources.FindObjectsOfTypeAll(type)
            .OfType<UnityEngine.Component>()
            .Count(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy);
    }

    object readProperty(object target, string name) {
        return target.GetType().GetProperty(name, InstanceFlags).GetValue(target);
    }

    Type inputManagerType = Type.GetType(""MajdataPlay.IO.InputManager, Assembly-CSharp"", true);
    Type buttonZoneType = Type.GetType(""MajdataPlay.IO.ButtonZone, Assembly-CSharp"", true);
    Type sensorAreaType = Type.GetType(""MajdataPlay.IO.SensorArea, Assembly-CSharp"", true);
    Type switchStatusType = Type.GetType(""MajdataPlay.IO.SwitchStatus, Assembly-CSharp"", true);
    Type gameUpdaterType = Type.GetType(""MajdataPlay.GameUpdater, Assembly-CSharp"", false);
    Type dummyTouchRendererType = Type.GetType(""MajdataPlay.DummyTouchPanelRenderer, Assembly-CSharp"", false);

    bool inputManagerInitialized = Convert.ToBoolean(inputManagerType.GetField(""_isInited"", StaticFlags).GetValue(null));
    object bindingKeysMemory = inputManagerType.GetField(""_bindingKeys"", StaticFlags).GetValue(null);
    object buttonsMemory = inputManagerType.GetField(""_buttons"", StaticFlags).GetValue(null);
    object sensorsMemory = inputManagerType.GetField(""_sensors"", StaticFlags).GetValue(null);
    int bindingKeyCount = readMemoryLength(bindingKeysMemory);
    int buttonObjectCount = readMemoryLength(buttonsMemory);
    int sensorObjectCount = readMemoryLength(sensorsMemory);

    var bindingKeyNames = readMemoryArray(bindingKeysMemory).Cast<object>().Select(value => value.ToString()).ToArray();
    var buttonObjects = readMemoryArray(buttonsMemory).Cast<object>().ToArray();
    var sensorObjects = readMemoryArray(sensorsMemory).Cast<object>().ToArray();
    int uniqueButtonZoneCount = buttonObjects.Select(button => readProperty(button, ""Zone"").ToString()).Distinct().Count();
    int uniqueSensorAreaCount = sensorObjects.Select(sensor => readProperty(sensor, ""Area"").ToString()).Distinct().Count();

    Type buttonRingType = inputManagerType.GetNestedType(""ButtonRing"", System.Reflection.BindingFlags.NonPublic);
    Type touchPanelType = inputManagerType.GetNestedType(""TouchPanel"", System.Reflection.BindingFlags.NonPublic);
    var buttonStates = (Array)buttonRingType.GetField(""_buttonStates"", StaticFlags).GetValue(null);
    var buttonRealtimeStates = (Array)buttonRingType.GetField(""_buttonRealTimeStates"", StaticFlags).GetValue(null);
    var touchSensorStates = (Array)touchPanelType.GetField(""_sensorStates"", StaticFlags).GetValue(null);
    var touchSensorRealtimeStates = (Array)touchPanelType.GetField(""_sensorRealTimeStates"", StaticFlags).GetValue(null);
    bool buttonRingConnected = Convert.ToBoolean(buttonRingType.GetProperty(""IsConnected"", StaticFlags).GetValue(null));
    bool touchPanelConnected = Convert.ToBoolean(touchPanelType.GetProperty(""IsConnected"", StaticFlags).GetValue(null));

    object unitCircleMemory = inputManagerType.GetField(""_unitCircle"", StaticFlags).GetValue(null);
    object touchMap = inputManagerType.GetField(""_instanceID2SensorIndexMappingTable"", StaticFlags).GetValue(null);
    int inputManagerTouchMapCount = Convert.ToInt32(touchMap.GetType().GetProperty(""Count"").GetValue(touchMap));

    int dummyTouchMapCount = 0;
    if (dummyTouchRendererType != null) {
        var renderers = UnityEngine.Resources.FindObjectsOfTypeAll(dummyTouchRendererType)
            .OfType<UnityEngine.Component>()
            .Where(component => component != null && component.gameObject != null && component.gameObject.activeInHierarchy)
            .ToArray();
        object renderer = renderers.FirstOrDefault();
        if (renderer != null) {
            object rendererMap = dummyTouchRendererType.GetProperty(""InstanceID2SensorIndexMappingTable"", InstanceFlags).GetValue(renderer);
            dummyTouchMapCount = Convert.ToInt32(rendererMap.GetType().GetProperty(""Count"").GetValue(rendererMap));
        }
    }

    Type majEnvType = Type.GetType(""MajdataPlay.MajEnv, Assembly-CSharp"", true);
    object settings = majEnvType.GetProperty(""Settings"", StaticFlags).GetValue(null);
    object ioSettings = readProperty(settings, ""IO"");
    object inputDeviceSettings = readProperty(ioSettings, ""InputDevice"");
    object buttonRingSettings = readProperty(inputDeviceSettings, ""ButtonRing"");
    object touchPanelSettings = readProperty(inputDeviceSettings, ""TouchPanel"");

    object offStatus = Enum.Parse(switchStatusType, ""Off"");
    object a1Button = Enum.Parse(buttonZoneType, ""A1"");
    object a1Sensor = Enum.Parse(sensorAreaType, ""A1"");
    bool representativeButtonStatusReadable = Convert.ToBoolean(inputManagerType.GetMethod(""CheckButtonStatusInThisFrame"", StaticFlags).Invoke(null, new object[] { a1Button, offStatus }));
    bool representativeSensorStatusReadable = Convert.ToBoolean(inputManagerType.GetMethod(""CheckSensorStatusInThisFrame"", StaticFlags).Invoke(null, new object[] { a1Sensor, offStatus }));

    return new {
        InputManagerInitialized = inputManagerInitialized,
        ActiveGameUpdaterCount = countActiveComponents(gameUpdaterType),
        ActiveDummyTouchPanelRendererCount = countActiveComponents(dummyTouchRendererType),
        DummyTouchMapCount = dummyTouchMapCount,
        InputManagerTouchMapCount = inputManagerTouchMapCount,
        ButtonZoneCount = Enum.GetNames(buttonZoneType).Length,
        SensorAreaCount = Enum.GetNames(sensorAreaType).Length,
        SwitchStatusCount = Enum.GetNames(switchStatusType).Length,
        BindingKeyCount = bindingKeyCount,
        UniqueBindingKeyCount = bindingKeyNames.Distinct().Count(),
        ButtonObjectCount = buttonObjectCount,
        UniqueButtonZoneCount = uniqueButtonZoneCount,
        SensorObjectCount = sensorObjectCount,
        UniqueSensorAreaCount = uniqueSensorAreaCount,
        ButtonStateArrayLength = buttonStates.Length,
        ButtonRealtimeStateArrayLength = buttonRealtimeStates.Length,
        TouchSensorStateArrayLength = touchSensorStates.Length,
        TouchSensorRealtimeStateArrayLength = touchSensorRealtimeStates.Length,
        UnitCircleLength = readMemoryLength(unitCircleMemory),
        ButtonPollingRateMs = Convert.ToInt32(readProperty(buttonRingSettings, ""PollingRateMs"")),
        ButtonDebounceThresholdMs = Convert.ToInt32(readProperty(buttonRingSettings, ""DebounceThresholdMs"")),
        TouchPollingRateMs = Convert.ToInt32(readProperty(touchPanelSettings, ""PollingRateMs"")),
        TouchDebounceThresholdMs = Convert.ToInt32(readProperty(touchPanelSettings, ""DebounceThresholdMs"")),
        ButtonRingEnabledFromSettings = readProperty(buttonRingSettings, ""Enable"").ToString(),
        TouchPanelEnabledFromSettings = readProperty(touchPanelSettings, ""Enable"").ToString(),
        ButtonRingConnected = buttonRingConnected,
        TouchPanelConnected = touchPanelConnected,
        RepresentativeButtonStatusReadable = representativeButtonStatusReadable,
        RepresentativeSensorStatusReadable = representativeSensorStatusReadable
    };
})()");

            return IntegrationAssertions.EvalResultProperties(response);
        }
    }
}
