using HarmonyLib;

namespace StandsOfValheim
{
    /// <summary>Dev command (requires `devcommands`) that equips Star Platinum as the guardian power.</summary>
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    internal static class Commands
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            new Terminal.ConsoleCommand("stand",
                "equip Star Platinum as your forsaken power (activate it with the power key, like a boss power)", args =>
                {
                    var player = Player.m_localPlayer;
                    if (player == null) { args.Context.AddString("No local player."); return; }
                    player.SetGuardianPower(SE_StarPlatinum.EffectName);
                    args.Context.AddString("Star Platinum equipped. Press the forsaken power key to summon.");
                }, isCheat: true);
        }
    }
}
