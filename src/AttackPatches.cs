using HarmonyLib;

namespace StandsOfValheim
{
    /// <summary>
    /// Intercepts PlayerAttackInput to route secondary attack commands to Star Platinum.
    /// </summary>
    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    internal static class AttackPatches
    {
        private static readonly AccessTools.FieldRef<Player, float> QueuedSecondAttackTimerRef =
            AccessTools.FieldRefAccess<Player, float>("m_queuedSecondAttackTimer");

        private static readonly AccessTools.FieldRef<Character, bool> SecondaryAttackRef =
            AccessTools.FieldRefAccess<Character, bool>("m_secondaryAttack");

        [HarmonyPrefix]
        private static bool Prefix(Player __instance)
        {
            if (StandManager.IsActive(__instance))
            {
                ref float queuedSec = ref QueuedSecondAttackTimerRef(__instance);
                ref bool secAttack = ref SecondaryAttackRef(__instance);

                if (queuedSec > 0f || secAttack)
                {
                    queuedSec = 0f;
                    secAttack = false;
                    StandManager.TriggerAttack(__instance);
                    return false;
                }
            }
            return true;
        }
    }
}
