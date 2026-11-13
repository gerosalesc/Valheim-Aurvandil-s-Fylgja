using HarmonyLib;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Intercepts PlayerAttackInput to route primary (left-click single punch) and secondary
    /// (middle-click ORA barrage) attack commands to Star Platinum when hands are free.
    /// </summary>
    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    internal static class AttackPatches
    {
        private static readonly AccessTools.FieldRef<Player, float> QueuedAttackTimerRef =
            AccessTools.FieldRefAccess<Player, float>("m_queuedAttackTimer");

        private static readonly AccessTools.FieldRef<Player, float> QueuedSecondAttackTimerRef =
            AccessTools.FieldRefAccess<Player, float>("m_queuedSecondAttackTimer");

        private static readonly AccessTools.FieldRef<Character, bool> AttackRef =
            AccessTools.FieldRefAccess<Character, bool>("m_attack");

        private static readonly AccessTools.FieldRef<Character, bool> AttackHoldRef =
            AccessTools.FieldRefAccess<Character, bool>("m_attackHold");

        private static readonly AccessTools.FieldRef<Character, bool> SecondaryAttackRef =
            AccessTools.FieldRefAccess<Character, bool>("m_secondaryAttack");

        private static readonly AccessTools.FieldRef<Character, bool> SecondaryAttackHoldRef =
            AccessTools.FieldRefAccess<Character, bool>("m_secondaryAttackHold");

        [HarmonyPrefix]
        private static bool Prefix(Player __instance)
        {
            if (!StandManager.IsActive(__instance) || !StandManager.AreHandsFree(__instance))
            {
                return true;
            }

            if (__instance.InPlaceMode() || __instance.InDodge() || !__instance.CanMove() ||
                __instance.IsKnockedBack() || __instance.IsStaggering() || __instance.InMinorAction())
            {
                return true;
            }

            ref float queuedAtk = ref QueuedAttackTimerRef(__instance);
            ref float queuedSec = ref QueuedSecondAttackTimerRef(__instance);
            ref bool attack = ref AttackRef(__instance);
            bool attackHold = AttackHoldRef(__instance);
            ref bool secAttack = ref SecondaryAttackRef(__instance);
            bool secAttackHold = SecondaryAttackHoldRef(__instance);

            if (attack)
            {
                queuedAtk = 0.30f;
                queuedSec = 0f;
            }
            if (secAttack)
            {
                queuedSec = 0.35f;
                queuedAtk = 0f;
            }

            queuedAtk = Mathf.Max(0f, queuedAtk - Time.fixedDeltaTime);
            queuedSec = Mathf.Max(0f, queuedSec - Time.fixedDeltaTime);

            bool hasSecondary = queuedSec > 0f || secAttackHold;
            bool hasPrimary = queuedAtk > 0f || attackHold;

            if (!hasSecondary && !hasPrimary)
            {
                return true;
            }

            // Route secondary (ORA barrage) or primary (single punch) uniformly to Star Platinum
            bool useSecondary = hasSecondary;
            bool allowStaminaFlash = useSecondary ? secAttack : attack;
            ref float activeQueue = ref (useSecondary ? ref queuedSec : ref queuedAtk);

            if (StandManager.TriggerAttack(__instance, useSecondary, allowStaminaFlash) != StandFollower.AttackStartStatus.Busy)
            {
                activeQueue = 0f;
            }

            attack = false;
            secAttack = false;
            return false;
        }
    }
}
