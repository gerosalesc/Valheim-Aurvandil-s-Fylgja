using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>Placeholder Star Platinum model built from Unity primitives (purple humanoid, black hair).</summary>
    internal static class StarPlatinum
    {
        private static readonly Color Purple = new Color(0.45f, 0.25f, 0.75f);
        private static readonly Color Hair = new Color(0.05f, 0.05f, 0.1f);

        public static GameObject Build()
        {
            var root = new GameObject("StarPlatinum");
            Part(root, PrimitiveType.Capsule, "Torso", new Vector3(0, 1.1f, 0), new Vector3(0.6f, 0.5f, 0.35f), Purple);
            Part(root, PrimitiveType.Sphere, "Head", new Vector3(0, 1.9f, 0), new Vector3(0.38f, 0.42f, 0.38f), Purple);
            Part(root, PrimitiveType.Sphere, "Hair", new Vector3(0, 2.05f, -0.05f), new Vector3(0.42f, 0.25f, 0.42f), Hair);
            Part(root, PrimitiveType.Capsule, "ArmL", new Vector3(-0.5f, 1.15f, 0.1f), new Vector3(0.2f, 0.45f, 0.2f), Purple);
            Part(root, PrimitiveType.Capsule, "ArmR", new Vector3(0.5f, 1.15f, 0.1f), new Vector3(0.2f, 0.45f, 0.2f), Purple);
            Part(root, PrimitiveType.Sphere, "FistL", new Vector3(-0.5f, 0.6f, 0.15f), new Vector3(0.25f, 0.25f, 0.25f), Purple);
            Part(root, PrimitiveType.Sphere, "FistR", new Vector3(0.5f, 0.6f, 0.15f), new Vector3(0.25f, 0.25f, 0.25f), Purple);
            Part(root, PrimitiveType.Capsule, "LegL", new Vector3(-0.2f, 0.35f, 0), new Vector3(0.25f, 0.4f, 0.25f), Purple);
            Part(root, PrimitiveType.Capsule, "LegR", new Vector3(0.2f, 0.35f, 0), new Vector3(0.25f, 0.4f, 0.25f), Purple);
            return root;
        }

        private static void Part(GameObject root, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>()); // purely visual for now
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Standard")) { color = color };
        }
    }
}
