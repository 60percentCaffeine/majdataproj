using System;
using System.IO;
using System.Reflection;
using MajdataQolSongListMod.Core;
using MelonLoader;

[assembly: MelonInfo(typeof(MajdataQolSongListMod.MajdataQolSongListMod), "Majdata QoL Song List Mod", "0.1.0", "user0")]
[assembly: MelonGame]
[assembly: HarmonyDontPatchAll]

namespace MajdataQolSongListMod
{
    public sealed class MajdataQolSongListMod : MelonMod
    {
        static MajdataQolSongListMod()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSupportAssembly;
        }

        public override void OnApplicationStart()
        {
            QolSongListModLogic logic = new QolSongListModLogic();
            MelonLogger.Msg(logic.StartupMessage());
            MelonLogger.Msg("Majdata QoL Song List Mod active with default folder behavior preserved.");
        }

        private static Assembly ResolveSupportAssembly(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new AssemblyName(args.Name);
            if (name.Name != "MajdataQolSongListMod.Core")
            {
                return null;
            }

            string path = Path.Combine(Environment.CurrentDirectory, "Mods", "MajdataQolSongListModLib", "MajdataQolSongListMod.Core.dll");
            if (!File.Exists(path))
            {
                return null;
            }

            return Assembly.LoadFrom(path);
        }
    }
}
