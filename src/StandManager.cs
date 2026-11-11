using System.Collections.Generic;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>Spawns/despawns the stand visuals. Lifetime is driven by <see cref="SE_StarPlatinum"/>.</summary>
    internal static class StandManager
    {
        private static readonly Dictionary<Player, GameObject> Active = new Dictionary<Player, GameObject>();

        public static void Summon(Player p)
        {
            Dismiss(p);
            var go = StarPlatinum.Build();
            go.AddComponent<StandFollower>().Owner = p;
            Active[p] = go;
        }

        public static void Dismiss(Player p)
        {
            if (Active.TryGetValue(p, out var go) && go != null) Object.Destroy(go);
            Active.Remove(p);
        }
    }
}
