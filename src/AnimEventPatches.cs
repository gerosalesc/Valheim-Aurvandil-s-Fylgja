using HarmonyLib;

namespace StandsOfValheim
{
    /// <summary>
    /// Prevents CharacterAnimEvent.Awake from crashing on Star Platinum's cloned visual model,
    /// since the Stand does not have a Character or ZNetView root component.
    /// </summary>
    [HarmonyPatch(typeof(CharacterAnimEvent), "Awake")]
    internal static class AnimEventPatches
    {
        [HarmonyPrefix]
        private static bool Prefix(CharacterAnimEvent __instance)
        {
            if (__instance != null && __instance.GetComponentInParent<StandFollower>() != null)
            {
                return false; // Skip on Stand
            }
            return true;
        }
    }
}
