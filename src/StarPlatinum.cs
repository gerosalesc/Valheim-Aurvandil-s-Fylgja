using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Builds Star Platinum with stylized stand colors (purple humanoid, gold trim, dark hair)
    /// and attaches limbs to StandFollower for attack animations.
    /// </summary>
    internal static class StarPlatinum
    {
        private static readonly Color StandPurple = new Color(0.55f, 0.22f, 0.85f);
        private static readonly Color HairColor   = new Color(0.08f, 0.08f, 0.12f);
        private static readonly Color GoldTrim    = new Color(0.95f, 0.82f, 0.22f);
        private static readonly Color EyeRed      = new Color(1.0f, 0.20f, 0.20f);

        public static GameObject Build(Player owner)
        {
            var root = new GameObject("StarPlatinum");
            var follower = root.AddComponent<StandFollower>();
            follower.Owner = owner;

            // --- Torso & Belt ---
            follower.Torso = Part(root, PrimitiveType.Capsule, "Torso", new Vector3(0, 1.15f, 0), new Vector3(0.55f, 0.45f, 0.35f), StandPurple);
            Part(root, PrimitiveType.Cylinder, "Belt", new Vector3(0, 0.82f, 0), new Vector3(0.56f, 0.08f, 0.36f), GoldTrim);

            // --- Head, Hair, Headband, Eyes ---
            follower.Head = Part(root, PrimitiveType.Sphere, "Head", new Vector3(0, 1.75f, 0), new Vector3(0.35f, 0.40f, 0.35f), StandPurple);
            Part(follower.Head.gameObject, PrimitiveType.Sphere, "Hair", new Vector3(0, 0.15f, -0.06f), new Vector3(0.42f, 0.28f, 0.42f), HairColor);
            Part(follower.Head.gameObject, PrimitiveType.Cylinder, "Headband", new Vector3(0, 0.04f, 0), new Vector3(0.36f, 0.04f, 0.36f), GoldTrim);
            Part(follower.Head.gameObject, PrimitiveType.Sphere, "EyeL", new Vector3(-0.09f, 0.02f, 0.16f), new Vector3(0.06f, 0.04f, 0.04f), EyeRed);
            Part(follower.Head.gameObject, PrimitiveType.Sphere, "EyeR", new Vector3(0.09f, 0.02f, 0.16f), new Vector3(0.06f, 0.04f, 0.04f), EyeRed);

            // --- Shoulders ---
            Part(root, PrimitiveType.Sphere, "ShoulderL", new Vector3(-0.45f, 1.35f, 0), new Vector3(0.24f, 0.18f, 0.24f), GoldTrim);
            Part(root, PrimitiveType.Sphere, "ShoulderR", new Vector3(0.45f, 1.35f, 0), new Vector3(0.24f, 0.18f, 0.24f), GoldTrim);

            // --- Arms ---
            follower.ArmL = Part(root, PrimitiveType.Capsule, "ArmL", new Vector3(-0.48f, 1.10f, 0.05f), new Vector3(0.18f, 0.36f, 0.18f), StandPurple);
            follower.ArmR = Part(root, PrimitiveType.Capsule, "ArmR", new Vector3(0.48f, 1.10f, 0.05f), new Vector3(0.18f, 0.36f, 0.18f), StandPurple);

            // --- Fists & Glove Trim ---
            follower.FistL = Part(root, PrimitiveType.Sphere, "FistL", new Vector3(-0.48f, 0.68f, 0.15f), new Vector3(0.25f, 0.25f, 0.25f), StandPurple);
            follower.FistR = Part(root, PrimitiveType.Sphere, "FistR", new Vector3(0.48f, 0.68f, 0.15f), new Vector3(0.25f, 0.25f, 0.25f), StandPurple);
            Part(follower.FistL.gameObject, PrimitiveType.Sphere, "TrimL", new Vector3(0, 0.08f, 0), new Vector3(0.22f, 0.08f, 0.22f), GoldTrim);
            Part(follower.FistR.gameObject, PrimitiveType.Sphere, "TrimR", new Vector3(0, 0.08f, 0), new Vector3(0.22f, 0.08f, 0.22f), GoldTrim);

            // --- Legs ---
            follower.LegL = Part(root, PrimitiveType.Capsule, "LegL", new Vector3(-0.20f, 0.35f, 0), new Vector3(0.22f, 0.40f, 0.22f), StandPurple);
            follower.LegR = Part(root, PrimitiveType.Capsule, "LegR", new Vector3(0.20f, 0.35f, 0), new Vector3(0.22f, 0.40f, 0.22f), StandPurple);

            // --- Aura Point Light ---
            var lightObj = new GameObject("StandAura");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0, 1.2f, 0);
            var aura = lightObj.AddComponent<Light>();
            aura.type = LightType.Point;
            aura.color = new Color(0.68f, 0.25f, 1.0f);
            aura.range = 3.5f;
            aura.intensity = 1.3f;
            aura.shadows = LightShadows.None;

            return root;
        }

        private static Transform Part(GameObject parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = StandMaterialManager.GetMaterial(color);
            }

            return go.transform;
        }
    }
}
