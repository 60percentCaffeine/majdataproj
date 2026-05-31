using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using TestMod.Core;

[assembly: MelonInfo(typeof(TestMod.TestMod), "TestMod", "1.0.0", "user0")]
[assembly: MelonGame]
[assembly: HarmonyDontPatchAll]

namespace TestMod
{
    public sealed class TestMod : MelonMod
    {
        static TestMod()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSupportAssembly;
        }

        public override void OnApplicationStart()
        {
            TestModLogic logic = new TestModLogic();
            MelonLogger.Msg(logic.StartupMessage());
        }

        private static Assembly ResolveSupportAssembly(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new AssemblyName(args.Name);
            if (name.Name != "TestMod.Core")
            {
                return null;
            }

            string path = Path.Combine(Environment.CurrentDirectory, "Mods", "TestModLib", "TestMod.Core.dll");
            if (!File.Exists(path))
            {
                return null;
            }

            return Assembly.LoadFrom(path);
        }
    }
}
