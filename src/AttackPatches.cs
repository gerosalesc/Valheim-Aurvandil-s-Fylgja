using HarmonyLib;

namespace StandsOfValheim
{
    /// <summary>
    /// Intercepts PlayerAttackInput to route secondary attack commands to Star Platinum,
    /// only when hands are free (no items equipped in right or left hand).
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
            if (StandManager.IsActive(__instance) && StandManager.AreHandsFree(__instance))
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
