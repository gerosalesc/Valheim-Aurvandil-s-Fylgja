using HarmonyLib;

namespace StandsOfValheim
{
    /// <summary>Registers dev console commands (require `devcommands` in F5 console).</summary>
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    internal static class Commands
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            new Terminal.ConsoleCommand("stand",
                "[summon|dismiss] - toggle Star Platinum behind the player", args =>
                {
                    var player = Player.m_localPlayer;
                    if (player == null) { args.Context.AddString("No local player."); return; }

                    string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "toggle";
                    bool active = StandManager.IsActive(player);
                    if (sub == "dismiss" || (sub == "toggle" && active))
                    {
                        StandManager.Dismiss(player);
                        args.Context.AddString("Star Platinum dismissed.");
                    }
                    else if (!active)
                    {
                        StandManager.Summon(player);
                        args.Context.AddString("ORA! Star Platinum summoned.");
                    }
                }, isCheat: true);
        }
    }
}
