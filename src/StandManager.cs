using System.Collections.Generic;
using UnityEngine;

namespace StandsOfValheim
{
    internal static class StandManager
    {
        private static readonly Dictionary<Player, GameObject> Active = new Dictionary<Player, GameObject>();

        public static bool IsActive(Player p) => Active.TryGetValue(p, out var go) && go != null;

        public static void Summon(Player p)
        {
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
