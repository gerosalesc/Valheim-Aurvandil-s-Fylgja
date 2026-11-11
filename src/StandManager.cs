using System.Collections.Generic;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Spawns, despawns, and manages active Stand game objects.
    /// </summary>
    internal static class StandManager
    {
        private static readonly Dictionary<Player, GameObject> Active = new Dictionary<Player, GameObject>();

        public static bool IsActive(Player p) => Active.TryGetValue(p, out var go) && go != null;

        /// <summary>
        /// Checks if the player's hands are completely free (no weapon, shield, or tool in right or left hand).
        /// </summary>
        public static bool AreHandsFree(Player p)
        {
            if (p == null) return false;
            return p.RightItem == null && p.LeftItem == null;
        }

        public static void Summon(Player p)
        {
            Dismiss(p);
            var go = StarPlatinum.Build(p);
            Active[p] = go;
            Plugin.Log?.LogInfo($"StandManager: Star Platinum summoned for {p.GetPlayerName()}");
        }

        public static void Dismiss(Player p)
        {
            if (Active.TryGetValue(p, out var go) && go != null)
            {
                Object.Destroy(go);
            }
            Active.Remove(p);
            Plugin.Log?.LogInfo($"StandManager: Star Platinum dismissed for {p.GetPlayerName()}");
        }

        public static bool TriggerAttack(Player p)
        {
            if (!AreHandsFree(p)) return false;

            if (Active.TryGetValue(p, out var go) && go != null)
            {
                var follower = go.GetComponent<StandFollower>();
                if (follower != null)
                {
                    return follower.TryStartAttack();
                }
            }
            return false;
        }

        public static void TriggerBlockReaction(Player p, bool isParry)
        {
            if (Active.TryGetValue(p, out var go) && go != null)
            {
                var follower = go.GetComponent<StandFollower>();
                if (follower != null)
                {
                    follower.OnBlockHit(isParry);
                }
            }
        }
    }
}
