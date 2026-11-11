using HarmonyLib;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Star Platinum as a Valheim guardian (forsaken) power. Enforces "one at a time",
    /// the cooldown (60s), active duration (30s), the HUD slot, and activation via the 'F' power key.
    /// </summary>
    public class SE_StarPlatinum : SE_Stats
    {
        public const string EffectName = "GP_StarPlatinum";

        public override void Setup(Character character)
        {
            base.Setup(character);
            if (character is Player p) StandManager.Summon(p);
        }

        public override void Stop()
        {
            base.Stop();
            if (m_character is Player p) StandManager.Dismiss(p);
        }
    }

    /// <summary>Registers the status effect into ObjectDB so it can be equipped as a guardian power.</summary>
    [HarmonyPatch]
    internal static class ObjectDBPatches
    {
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        [HarmonyPostfix]
        private static void Awake(ObjectDB __instance) => Register(__instance);

        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        [HarmonyPostfix]
        private static void CopyOtherDB(ObjectDB __instance) => Register(__instance);

        private static void Register(ObjectDB db)
        {
            if (db.m_StatusEffects.Count == 0) return;

            var existing = db.m_StatusEffects.Find(s => s != null && s.name == SE_StarPlatinum.EffectName);
            if (existing != null)
            {
                existing.m_ttl = 30f;
                existing.m_cooldown = 60f;
                return;
            }

            var se = ScriptableObject.CreateInstance<SE_StarPlatinum>();
            se.name = SE_StarPlatinum.EffectName;
            se.m_name = "Star Platinum";
            se.m_tooltip = "Summon Star Platinum (30s active, 60s cooldown). Use Middle Mouse for ORA barrage, Right Mouse to block/parry!";
            se.m_startMessageType = MessageHud.MessageType.Center;
            se.m_startMessage = "STAR PLATINUM!";
            se.m_ttl = 30f;
            se.m_cooldown = 60f;
            se.m_addMaxCarryWeight = 100f;
            se.m_staminaRegenMultiplier = 1.3f;

            // Borrow an existing boss power icon
            var template = db.m_StatusEffects.Find(s => s != null && s.name == "GP_Eikthyr");
            if (template != null) se.m_icon = template.m_icon;

            db.m_StatusEffects.Add(se);
            Plugin.Log?.LogInfo("Registered GP_StarPlatinum in ObjectDB (30s duration, 60s cooldown).");
        }
    }
}
