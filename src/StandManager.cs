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
            var go = AurvandilsFylgja.Build(p);
            Active[p] = go;
            Plugin.Log?.LogInfo($"StandManager: Aurvandil's Fylgja summoned for {p.GetPlayerName()}");
        }

        public static void Dismiss(Player p)
        {
            if (Active.TryGetValue(p, out var go) && go != null)
            {
                Object.Destroy(go);
            }
            Active.Remove(p);
            Plugin.Log?.LogInfo($"StandManager: Aurvandil's Fylgja dismissed for {p.GetPlayerName()}");
        }

        public static StandFollower.AttackStartStatus TriggerAttack(Player p, bool secondary = true, bool allowStaminaFlash = true)
        {
            if (!AreHandsFree(p)) return StandFollower.AttackStartStatus.Unavailable;

            if (Active.TryGetValue(p, out var go) && go != null)
            {
                var follower = go.GetComponent<StandFollower>();
                if (follower != null)
                {
                    return secondary
                        ? follower.TryStartAttack(allowStaminaFlash)
                        : follower.TryStartSinglePunch(allowStaminaFlash);
                }
            }
            return StandFollower.AttackStartStatus.Unavailable;
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
