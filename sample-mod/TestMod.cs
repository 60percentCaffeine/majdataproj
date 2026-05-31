using MelonLoader;
using TestMod.Core;

[assembly: MelonInfo(typeof(TestMod.TestMod), "TestMod", "1.0.0", "user0")]
[assembly: MelonGame]
[assembly: HarmonyDontPatchAll]

namespace TestMod
{
    public sealed class TestMod : MelonMod
    {
        public override void OnApplicationStart()
        {
            TestModLogic logic = new TestModLogic();
            MelonLogger.Msg(logic.StartupMessage());
        }
    }
}
