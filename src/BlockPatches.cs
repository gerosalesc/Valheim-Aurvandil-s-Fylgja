using HarmonyLib;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Patches Humanoid.BlockAttack so that when Star Platinum is summoned,
    /// the Stand acts as the player's shield with stats calculated from
    /// the player's base armor and blocking skill.
    /// - Normal block armor = 2x base player armor * (1 + skill * 0.5)
    /// - Parry block armor  = 6x base player armor * (1 + skill * 0.5)
    /// - Parry bonus        = 2.5x
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class BlockPatches
    {
        private static readonly AccessTools.FieldRef<Humanoid, float> BlockTimerRef =
            AccessTools.FieldRefAccess<Humanoid, float>("m_blockTimer");

        private delegate bool AddStaggerDamageDelegate(Character instance, float damage, Vector3 dir, HitData hit);

        private static readonly AddStaggerDamageDelegate AddStaggerDamageInvoker =
            AccessTools.MethodDelegate<AddStaggerDamageDelegate>(AccessTools.Method(typeof(Character), "AddStaggerDamage"));

        [HarmonyPrefix]
        private static bool Prefix(Humanoid __instance, HitData hit, Character attacker, ref bool __result)
        {
            if (__instance is Player player && StandManager.IsActive(player))
            {
                __result = ExecuteStandBlock(player, hit, attacker);
                return false; // Stand takes the block, skip vanilla calculation
            }
            return true;
        }

        private static bool ExecuteStandBlock(Player player, HitData hit, Character attacker)
        {
            // Block angle check: attack must come from in front of the player
            if (Vector3.Dot(hit.m_dir, player.transform.forward) > 0f)
            {
                return false;
            }

            // Parry timing: attack blocked within 0.25s of raising block
            float blockTimer = BlockTimerRef(player);
            bool isParry = blockTimer != -1f && blockTimer < 0.25f;

            // Base player armor from equipped armor pieces (floor of 10 so unarmored players can block)
            float basePlayerArmor = Mathf.Max(10f, player.GetBodyArmor());

            // Blocking skill scaling: +50% at skill level 100
            float skillFactor = player.GetSkillFactor(Skills.SkillType.Blocking);
            float skillMult = 1f + (skillFactor * 0.5f);

            // Calculate block power based on player stats and skills
            // Normal: 2x base player armor | Parry: 6x base player armor | Parry bonus: 2.5x
            float normalBlockArmor = 2f * basePlayerArmor * skillMult;
            float parryBlockArmor  = 6f * basePlayerArmor * skillMult;

            float activeBlockArmor = isParry ? parryBlockArmor : normalBlockArmor;

            // Apply armor to clone to calculate absorbed damage
            HitData.DamageTypes remaining = hit.m_damage.Clone();
            remaining.ApplyArmor(activeBlockArmor);

            float totalBlockable = hit.GetTotalBlockableDamage();
            float remainingBlockable = remaining.GetTotalBlockableDamage();
            float blockedDamage = totalBlockable - remainingBlockable;

            // Stamina drain
            float staminaDrain = isParry
                ? player.m_perfectBlockStaminaDrain
                : (player.m_blockStaminaDrain * Mathf.Clamp01(blockedDamage / Mathf.Max(1f, activeBlockArmor)));

            staminaDrain += staminaDrain * player.GetEquipmentBlockStaminaModifier();
            player.UseStamina(staminaDrain);

            // Stagger check on the player via invoker
            float staggerDamage = remaining.GetTotalStaggerDamage();
            bool staggered = AddStaggerDamageInvoker(player, staggerDamage, hit.m_dir, hit);

            bool success = player.HaveStamina(0f) && !staggered;

            if (success)
            {
                // Attack successfully blocked by Star Platinum
                hit.m_statusEffectHash = 0;
                hit.BlockDamage(blockedDamage);

                // Show standard blocked damage number
                if (DamageText.instance != null)
                {
                    DamageText.instance.ShowText(DamageText.TextType.Blocked, hit.m_point + Vector3.up * 0.5f, blockedDamage, false);
                }

                // Raise player's blocking skill
                player.RaiseSkill(Skills.SkillType.Blocking, isParry ? 2f : 1f);

                // Stand deflection / recoil reaction
                StandManager.TriggerBlockReaction(player, isParry);

                if (isParry)
                {
                    // Perfect block parry effect (spark burst & sound)
                    if (player.m_perfectBlockEffect != null)
                    {
                        player.m_perfectBlockEffect.Create(hit.m_point, Quaternion.identity, null, 1f, -1, player.GetZDOID());
                    }

                    // Stagger the attacker!
                    if (attacker != null && attacker.m_staggerWhenBlocked)
                    {
                        attacker.Stagger(-hit.m_dir);
                    }

                    Plugin.Log?.LogInfo($"Stand Block: PARRY! Block Armor: {activeBlockArmor:F1} (6x base), Blocked: {blockedDamage:F1}");
                }
                else
                {
                    Plugin.Log?.LogInfo($"Stand Block: Blocked {blockedDamage:F1} damage with {activeBlockArmor:F1} armor (2x base).");
                }
            }

            return true;
        }
    }
}
