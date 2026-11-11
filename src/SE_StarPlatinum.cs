using HarmonyLib;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Star Platinum as a Valheim guardian (forsaken) power. Being a real guardian power means the game itself
    /// enforces "one at a time", the cooldown, the HUD slot and the activation keybind.
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
            if (db.m_StatusEffects.Count == 0) return; // not populated yet
            if (db.m_StatusEffects.Exists(se => se != null && se.name == SE_StarPlatinum.EffectName)) return;

            var se = ScriptableObject.CreateInstance<SE_StarPlatinum>();
            se.name = SE_StarPlatinum.EffectName;
            se.m_name = "Star Platinum";
            se.m_tooltip = "Summon Star Platinum behind you.";
            se.m_startMessageType = MessageHud.MessageType.Center;
            se.m_startMessage = "ORA!";
            se.m_ttl = 120f;
            se.m_cooldown = 60f;
            // Borrow an existing boss power's icon so the HUD doesn't choke on a null sprite.
            var template = db.m_StatusEffects.Find(s => s != null && s.name == "GP_Eikthyr");
            if (template != null) se.m_icon = template.m_icon;
            db.m_StatusEffects.Add(se);
        }
    }
}
