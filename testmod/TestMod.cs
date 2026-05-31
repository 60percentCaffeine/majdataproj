using MelonLoader;

[assembly: MelonInfo(typeof(TestMod.TestMod), "TestMod", "1.0.0", "user0")]
[assembly: MelonGame]
[assembly: HarmonyDontPatchAll]

namespace TestMod
{
    public sealed class TestMod : MelonMod
    {
        public override void OnApplicationStart()
        {
            MelonLogger.Msg("Loaded");
        }
    }
}
