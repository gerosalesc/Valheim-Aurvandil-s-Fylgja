using HarmonyLib;

namespace StandsOfValheim
{
    /// <summary>Dev command (requires `devcommands`) that equips Aurvandil's Fylgja as the guardian power.</summary>
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    internal static class Commands
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            new Terminal.ConsoleCommand("fylgja",
                "equip Aurvandil's Fylgja as your forsaken power (activate it with the power key, like a boss power)", args =>
                {
                    var player = Player.m_localPlayer;
                    if (player == null) { args.Context.AddString("No local player."); return; }
                    player.SetGuardianPower(SE_AurvandilsFylgja.EffectName);
                    args.Context.AddString("Aurvandil's Fylgja equipped. Press the forsaken power key to summon.");
                }, isCheat: true);

            new Terminal.ConsoleCommand("stand",
                "alias for 'fylgja': equip Aurvandil's Fylgja as your forsaken power", args =>
                {
                    var player = Player.m_localPlayer;
                    if (player == null) { args.Context.AddString("No local player."); return; }
                    player.SetGuardianPower(SE_AurvandilsFylgja.EffectName);
                    args.Context.AddString("Aurvandil's Fylgja equipped. Press the forsaken power key to summon.");
                }, isCheat: true);
        }
    }
}
