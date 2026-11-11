using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace StandsOfValheim
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "grosales.StandsOfValheim";
        public const string Name = "Stands of Valheim";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            new Harmony(Guid).PatchAll();
            Log.LogInfo($"{Name} {Version} loaded. Use devcommands, then 'stand' in the F5 console.");
        }
    }
}
