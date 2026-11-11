using System;
using UnityEngine;

namespace StandsOfValheim
{
    internal static class StandMaterialManager
    {
        private static Material s_baseMaterial;

        public static Material GetMaterial(Color color)
        {
            if (s_baseMaterial == null)
            {
                s_baseMaterial = FindSafeMaterial();
            }

            Material mat = s_baseMaterial != null ? new Material(s_baseMaterial) : CreateFallbackMaterial();
            if (mat != null)
            {
                mat.color = color;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                // Assign Unity's built-in white texture so any texture multiplier renders our pure color
                mat.mainTexture = Texture2D.whiteTexture;
            }
            return mat;
        }

        private static Material FindSafeMaterial()
        {
            // 1. Borrow from the local player's character mesh (guaranteed valid in Valheim)
            try
            {
                if (Player.m_localPlayer != null)
                {
                    var renderers = Player.m_localPlayer.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null)
                        {
                            Plugin.Log?.LogInfo($"StandMaterialManager: Borrowed material '{r.sharedMaterial.name}' using shader '{r.sharedMaterial.shader.name}'.");
                            return new Material(r.sharedMaterial);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"StandMaterialManager: Could not get player material: {ex.Message}");
            }

            // 2. Try known shader names
            string[] candidateShaders = {
                "Custom/Creature",
                "Custom/Player",
                "Legacy Shaders/Diffuse",
                "Legacy Shaders/Bumped Diffuse",
                "Unlit/Color",
                "Sprites/Diffuse",
                "Sprites/Default",
                "UI/Default"
            };

            foreach (var name in candidateShaders)
            {
                try
                {
                    var s = Shader.Find(name);
                    if (s != null)
                    {
                        Plugin.Log?.LogInfo($"StandMaterialManager: Found shader '{name}' via Shader.Find.");
                        return new Material(s);
                    }
                }
                catch { }
            }

            // 3. Scan all loaded shaders in memory
            try
            {
                foreach (var s in Resources.FindObjectsOfTypeAll<Shader>())
                {
                    if (s != null && !string.IsNullOrEmpty(s.name) && !s.name.StartsWith("Hidden/"))
                    {
                        Plugin.Log?.LogInfo($"StandMaterialManager: Using memory shader '{s.name}'.");
                        return new Material(s);
                    }
                }
            }
            catch { }

            return CreateFallbackMaterial();
        }

        private static Material CreateFallbackMaterial()
        {
            try
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var r = cube.GetComponent<Renderer>();
                Material m = r != null && r.sharedMaterial != null ? new Material(r.sharedMaterial) : null;
                UnityEngine.Object.Destroy(cube);
                if (m != null) return m;
            }
            catch { }

            var fallbackShader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            return fallbackShader != null ? new Material(fallbackShader) : null;
        }
    }
}
