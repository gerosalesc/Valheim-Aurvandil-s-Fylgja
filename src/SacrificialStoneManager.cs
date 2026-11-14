using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Registers the custom Star Platinum Sacrificial Stone prefab ("BossStone_StarPlatinum"),
    /// customizes its 3D mesh, textures, cosmic glow, and ItemStand hook for TrophyFallenValkyrie,
    /// and ensures it spawns on the StartTemple circle in both new and existing worlds.
    /// </summary>
    internal static class SacrificialStoneManager
    {
        public const string PrefabName = "BossStone_StarPlatinum";
        public const string BaseStonePrefabName = "BossStone_Yagluth";
        public const string TrophyPrefabName = "TrophyFallenValkyrie";
        public const string StoneWorldKey = "defeated_fallenvalkyrie_sp";

        private static GameObject s_prefabContainer;
        private static GameObject s_stonePrefab;
        private static Mesh s_customStoneMesh;
        private static Material s_customStoneMaterial;
        private static bool s_spawnCheckInProgress;
        private static float s_lastSpawnTime = -999f;

        internal static readonly Color IdleEmissiveColor = new Color(0.26f, 0.18f, 0.36f, 1.0f);
        internal static readonly Color ActiveEmissiveColor = new Color(0.52f, 0.34f, 0.68f, 1.0f);

        public static void RegisterPrefab(ZNetScene zns)
        {
            if (zns == null || zns.m_prefabs == null) return;

            try
            {
                int hash = PrefabName.GetStableHashCode();
                var namedPrefabs = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs")?.GetValue(zns) as Dictionary<int, GameObject>;

                if (s_stonePrefab != null)
                {
                    if (!zns.m_prefabs.Contains(s_stonePrefab))
                    {
                        zns.m_prefabs.Add(s_stonePrefab);
                    }
                    if (namedPrefabs != null) namedPrefabs[hash] = s_stonePrefab;
                    ConfigureStoneInstance(s_stonePrefab);
                    return;
                }

                GameObject basePrefab = zns.GetPrefab(BaseStonePrefabName);
                if (basePrefab == null)
                {
                    basePrefab = zns.m_prefabs.Find(p => p != null && p.name == BaseStonePrefabName);
                }
                if (basePrefab == null)
                {
                    Plugin.Log?.LogWarning($"SacrificialStoneManager: Could not find base prefab '{BaseStonePrefabName}' in ZNetScene.");
                    return;
                }

                if (s_prefabContainer == null)
                {
                    s_prefabContainer = new GameObject("StandsOfValheim_PrefabContainer");
                    UnityEngine.Object.DontDestroyOnLoad(s_prefabContainer);
                    s_prefabContainer.SetActive(false);
                }

                s_stonePrefab = UnityEngine.Object.Instantiate(basePrefab, s_prefabContainer.transform);
                s_stonePrefab.name = PrefabName;
                s_stonePrefab.transform.localPosition = Vector3.zero;
                s_stonePrefab.transform.localRotation = Quaternion.identity;

                if (s_stonePrefab.GetComponent<StarPlatinumBossStone>() == null)
                {
                    s_stonePrefab.AddComponent<StarPlatinumBossStone>();
                }

                ConfigureStoneInstance(s_stonePrefab);

                if (!zns.m_prefabs.Contains(s_stonePrefab))
                {
                    zns.m_prefabs.Add(s_stonePrefab);
                }
                if (namedPrefabs != null) namedPrefabs[hash] = s_stonePrefab;

                Plugin.Log?.LogInfo($"SacrificialStoneManager: Registered '{PrefabName}' in ZNetScene.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"SacrificialStoneManager: Failed to register '{PrefabName}': {ex}");
            }
        }

        public static void RefreshAllConfiguredStones()
        {
            try
            {
                if (s_stonePrefab != null)
                {
                    ConfigureStoneInstance(s_stonePrefab);
                }

                var activeStones = UnityEngine.Object.FindObjectsByType<StarPlatinumBossStone>(FindObjectsSortMode.None);
                if (activeStones != null)
                {
                    foreach (var spStone in activeStones)
                    {
                        if (spStone != null)
                        {
                            ConfigureStoneInstance(spStone.gameObject);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"SacrificialStoneManager: RefreshAllConfiguredStones warning: {ex.Message}");
            }
        }

        public static bool IsStarPlatinumStone(BossStone stone)
        {
            if (stone == null) return false;
            if (stone.GetComponent<StarPlatinumBossStone>() != null) return true;
            string n = stone.gameObject.name;
            return !string.IsNullOrEmpty(n) && n.StartsWith(PrefabName, StringComparison.OrdinalIgnoreCase);
        }

        public static void ConfigureStoneInstance(GameObject stoneRoot)
        {
            if (stoneRoot == null) return;

            var bossStone = stoneRoot.GetComponent<BossStone>();
            if (bossStone == null) return;

            if (stoneRoot.GetComponent<StarPlatinumBossStone>() == null)
            {
                stoneRoot.AddComponent<StarPlatinumBossStone>();
            }

            // 1. Configure BossStone & ItemStand gameplay properties
            bossStone.m_completedMessage = "You have offered the Fallen Valkyrie. Star Platinum awaits!";
            bossStone.m_setsWorldKey = StoneWorldKey;
            bossStone.m_activeEmissiveColor = ActiveEmissiveColor;

            var itemStand = bossStone.m_itemStand ?? stoneRoot.GetComponentInChildren<ItemStand>(true);
            if (itemStand != null)
            {
                bossStone.m_itemStand = itemStand;
                itemStand.m_name = "Star Platinum Sacrificial Stone";
                itemStand.m_canBeRemoved = false;
                itemStand.m_autoAttach = true;

                var trophyDrop = ResolveFallenValkyrieTrophy();
                if (trophyDrop != null)
                {
                    itemStand.m_supportedItems = new List<ItemDrop> { trophyDrop };
                    itemStand.m_supportedTypes = new List<ItemDrop.ItemData.ItemType>();
                }

                var guardianPower = ResolveStarPlatinumStatusEffect();
                if (guardianPower != null)
                {
                    itemStand.m_guardianPower = guardianPower;
                }
            }

            // 2. Apply custom 3D mesh (side-hung chains + central hook) and high-res Star Platinum stone material
            ApplyStoneVisuals(bossStone);
        }

        private static ItemDrop ResolveFallenValkyrieTrophy()
        {
            if (ObjectDB.instance != null)
            {
                var trophyGo = ObjectDB.instance.GetItemPrefab(TrophyPrefabName);
                if (trophyGo == null && ObjectDB.instance.m_items != null)
                {
                    trophyGo = ObjectDB.instance.m_items.Find(i =>
                        i != null && i.name.IndexOf("FallenValkyrie", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                if (trophyGo != null)
                {
                    var drop = trophyGo.GetComponent<ItemDrop>();
                    if (drop != null) return drop;
                }
            }

            if (ZNetScene.instance != null)
            {
                var trophyGo = ZNetScene.instance.GetPrefab(TrophyPrefabName);
                if (trophyGo != null)
                {
                    var drop = trophyGo.GetComponent<ItemDrop>();
                    if (drop != null) return drop;
                }
            }

            return null;
        }

        private static StatusEffect ResolveStarPlatinumStatusEffect()
        {
            if (ObjectDB.instance == null) return null;

            var se = ObjectDB.instance.GetStatusEffect(SE_StarPlatinum.EffectName.GetStableHashCode());
            if (se != null) return se;

            if (ObjectDB.instance.m_StatusEffects != null)
            {
                return ObjectDB.instance.m_StatusEffects.Find(s => s != null && s.name == SE_StarPlatinum.EffectName);
            }

            return null;
        }

        public static void ApplyStoneVisuals(BossStone bossStone)
        {
            if (bossStone == null || bossStone.m_mesh == null) return;

            try
            {
                var meshRenderer = bossStone.m_mesh;
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    if (s_customStoneMesh == null)
                    {
                        s_customStoneMesh = BuildCustomStoneMesh(meshFilter.sharedMesh);
                    }
                    if (s_customStoneMesh != null && meshFilter.sharedMesh != s_customStoneMesh)
                    {
                        meshFilter.sharedMesh = s_customStoneMesh;
                    }
                }

                var sharedMats = meshRenderer.sharedMaterials;
                int matIdx = Mathf.Clamp(bossStone.m_emissiveMaterialIndex, 0, sharedMats.Length - 1);
                if (sharedMats != null && sharedMats.Length > 0 && sharedMats[matIdx] != null)
                {
                    if (s_customStoneMaterial == null)
                    {
                        s_customStoneMaterial = BuildCustomStoneMaterial(sharedMats[matIdx]);
                    }
                    if (s_customStoneMaterial != null)
                    {
                        var newMats = (Material[])sharedMats.Clone();
                        newMats[matIdx] = s_customStoneMaterial;
                        meshRenderer.sharedMaterials = newMats;
                    }
                }

                // Remove any legacy custom point light so only the vanilla BossStone active_effects aura is used
                Transform frontLightTr = bossStone.transform.Find("StarPlatinumStoneLight");
                if (frontLightTr != null)
                {
                    UnityEngine.Object.Destroy(frontLightTr.gameObject);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"SacrificialStoneManager: ApplyStoneVisuals failed: {ex}");
            }
        }

        /// <summary>
        /// Clones the BossStone_Yagluth mesh, keeping SubMesh 0 (stone monolith) and the central 3D trophy hook
        /// intact while shifting the two diagonal V-chains in SubMesh 1 out to hang vertically along the left
        /// and right outer edges of the stone so Star Platinum's carving on the front face is completely unobstructed.
        /// </summary>
        private static Mesh BuildCustomStoneMesh(Mesh origMesh)
        {
            try
            {
                Mesh mesh = UnityEngine.Object.Instantiate(origMesh);
                mesh.name = "BossStone_StarPlatinum_Mesh";

                Vector3[] verts = mesh.vertices;
                if (verts != null && verts.Length > 110 && mesh.subMeshCount >= 2)
                {
                    var sub1Desc = mesh.GetSubMesh(1);
                    int startV = sub1Desc.firstVertex;
                    int endV = startV + sub1Desc.vertexCount;

                    for (int i = startV; i < endV && i < verts.Length; i++)
                    {
                        Vector3 v = verts[i];

                        // Keep the central curved metal hook at (px in [0.085, 0.172], py < 1.56) untouched
                        bool isCentralHook = v.y < 1.56f && v.x >= 0.085f && v.x <= 0.172f;
                        if (isCentralHook) continue;

                        // For the chains descending from the top iron band (py <= 4.22), shift X outward so they
                        // hang vertically down the left and right outer sides of the stone monolith.
                        if (v.y <= 4.22f)
                        {
                            float drop = Mathf.Clamp(4.22f - v.y, 0f, 2.75f);
                            if (v.x < 0.141f)
                            {
                                v.x -= drop * 0.56f;
                            }
                            else
                            {
                                v.x += drop * 0.50f;
                            }
                            verts[i] = v;
                        }
                    }

                    mesh.vertices = verts;

                    // Safeguard: remove any triangle in SubMesh 1 that spans > 0.75 units across the stone
                    int[] tris1 = mesh.GetTriangles(1);
                    if (tris1 != null && tris1.Length >= 3)
                    {
                        var cleanTris = new List<int>(tris1.Length);
                        const float MaxEdgeSqr = 0.75f * 0.75f;
                        for (int t = 0; t + 2 < tris1.Length; t += 3)
                        {
                            int i0 = tris1[t];
                            int i1 = tris1[t + 1];
                            int i2 = tris1[t + 2];
                            Vector3 p0 = verts[i0];
                            Vector3 p1 = verts[i1];
                            Vector3 p2 = verts[i2];
                            if ((p0 - p1).sqrMagnitude <= MaxEdgeSqr &&
                                (p1 - p2).sqrMagnitude <= MaxEdgeSqr &&
                                (p2 - p0).sqrMagnitude <= MaxEdgeSqr)
                            {
                                cleanTris.Add(i0);
                                cleanTris.Add(i1);
                                cleanTris.Add(i2);
                            }
                        }
                        if (cleanTris.Count != tris1.Length)
                        {
                            mesh.SetTriangles(cleanTris, 1);
                        }
                    }

                    mesh.RecalculateBounds();
                }

                return mesh;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"SacrificialStoneManager: Could not reshape stone mesh, using original: {ex.Message}");
                return origMesh;
            }
        }

        /// <summary>
        /// Builds a 1024x1024 custom stone material from bossstone_yagluth_mat with the Star Platinum
        /// runic carving, 8-pointed Y star, horizontal iron bands, and glowing purple/gold cosmic nebula
        /// mapped onto the front stone face anchored to the 3D trophy hook.
        /// </summary>
        private static Material BuildCustomStoneMaterial(Material origMat)
        {
            try
            {
                Material mat = new Material(origMat);
                mat.name = "bossstone_starplatinum_mat";

                Texture2D overlayTex = IconManager.BossStoneTexture;
                if (overlayTex == null)
                {
                    Plugin.Log?.LogWarning("SacrificialStoneManager: BossStoneTexture is null; keeping base stone texture.");
                    return mat;
                }

                const int Size = 1024;
                Texture origMain = origMat.HasProperty("_MainTex") ? origMat.GetTexture("_MainTex") : null;
                Texture origBump = origMat.HasProperty("_BumpMap") ? origMat.GetTexture("_BumpMap") : null;

                Color[] mainPixels = ReadTexturePixels(origMain, Size, Size, new Color(0.28f, 0.30f, 0.31f, 1f));
                Color[] bumpPixels = origBump != null ? ReadTexturePixels(origBump, Size, Size, new Color(0.5f, 0.5f, 1f, 0.5f)) : null;
                Color[] emisPixels = new Color[Size * Size];

                int ovW = overlayTex.width;
                int ovH = overlayTex.height;
                Color[] ovPixels = overlayTex.GetPixels();

                // Exact UV-to-image mapping anchored at the 3D hook bracket (u=0.7316, v=0.2585 <-> x_img=380, y_img=568.5)
                const float UHook = 0.7316f;
                const float VHook = 0.2585f;
                const float XImgHook = 380.0f;
                const float YImgHook = 568.5f;
                const float UvPerImgPx = 0.000646f;

                int xMin = Mathf.Clamp(Mathf.FloorToInt(0.54f * Size), 0, Size - 1);
                int xMax = Mathf.Clamp(Mathf.CeilToInt(0.93f * Size), 0, Size - 1);
                int yMin = Mathf.Clamp(Mathf.FloorToInt(0.08f * Size), 0, Size - 1);
                int yMax = Mathf.Clamp(Mathf.CeilToInt(0.65f * Size), 0, Size - 1);

                for (int y = yMin; y <= yMax; y++)
                {
                    float v = (y + 0.5f) / Size;
                    float yImgFromTop = YImgHook - (v - VHook) / UvPerImgPx;
                    if (yImgFromTop < 30f || yImgFromTop >= ovH - 30f) continue;

                    // Unity Texture2D has y=0 at bottom, whereas yImgFromTop has 0 at top
                    float ovY = (ovH - 1f) - yImgFromTop;

                    for (int x = xMin; x <= xMax; x++)
                    {
                        float u = (x + 0.5f) / Size;
                        float xImg = XImgHook + (u - UHook) / UvPerImgPx;
                        if (xImg < 95f || xImg >= ovW - 85f) continue;

                        Color ov = SampleBilinear(ovPixels, ovW, ovH, xImg, ovY);
                        float alpha = ov.a;
                        if (alpha <= 0.001f) continue;

                        int idx = y * Size + x;
                        Color baseCol = mainPixels[idx];

                        // Match overall stone exposure softly while keeping etched black lines & cosmic glow vibrant
                        Color targetRgb = new Color(ov.r, ov.g, ov.b, baseCol.a);
                        mainPixels[idx] = Color.Lerp(baseCol, targetRgb, alpha);

                        // Extract cosmic purple & gold nebula + 8-pointed star rim for _EmissiveTex without white blowout
                        float lum = 0.299f * ov.r + 0.587f * ov.g + 0.114f * ov.b;
                        float purpleGlow = Mathf.Max(0f, Mathf.Min(ov.r, ov.b) - ov.g * 0.86f);
                        float goldGlow = Mathf.Max(0f, Mathf.Min(ov.r, ov.g) - ov.b * 0.88f);
                        float emisStrength = Mathf.Clamp01((purpleGlow * 2.2f + goldGlow * 2.0f) * alpha);

                        if (emisStrength > 0.02f)
                        {
                            emisPixels[idx] = new Color(
                                ov.r * emisStrength,
                                ov.g * emisStrength,
                                ov.b * emisStrength,
                                1.0f);
                        }

                        // Clean out Yagluth's old normal-map grooves on the front stone face and emboss Star Platinum's etched lines
                        if (bumpPixels != null && v >= 0.15f && v <= 0.56f && u >= 0.57f && u <= 0.90f)
                        {
                            // Sample clean stone normal from the uncarved back/side UV region of the same normal map
                            int cleanX = ((x - xMin) % (Size / 8)) + (Size / 8);
                            int cleanY = ((y - yMin) % (Size / 8)) + (Size / 8);
                            Color cleanNorm = bumpPixels[cleanY * Size + cleanX];

                            // Compute local luminance gradient from overlay to emboss the etched lines
                            Color ovR = SampleBilinear(ovPixels, ovW, ovH, Mathf.Min(ovW - 1f, xImg + 1.5f), ovY);
                            Color ovU = SampleBilinear(ovPixels, ovW, ovH, xImg, Mathf.Min(ovH - 1f, ovY + 1.5f));
                            float lumR = 0.299f * ovR.r + 0.587f * ovR.g + 0.114f * ovR.b;
                            float lumU = 0.299f * ovU.r + 0.587f * ovU.g + 0.114f * ovU.b;
                            float dx = (lumR - lum) * 0.35f * alpha;
                            float dy = (lumU - lum) * 0.35f * alpha;

                            Color embossed = new Color(
                                Mathf.Clamp01(cleanNorm.r + dx),
                                Mathf.Clamp01(cleanNorm.g + dy),
                                cleanNorm.b,
                                Mathf.Clamp01(cleanNorm.a + dx));

                            bumpPixels[idx] = Color.Lerp(bumpPixels[idx], embossed, alpha);
                        }
                    }
                }

                var newMainTex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                newMainTex.name = "StarPlatinumStone_d";
                newMainTex.SetPixels(mainPixels);
                newMainTex.Apply(true, false);
                mat.SetTexture("_MainTex", newMainTex);

                var newEmisTex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                newEmisTex.name = "StarPlatinumStone_e";
                newEmisTex.SetPixels(emisPixels);
                newEmisTex.Apply(true, false);
                if (mat.HasProperty("_EmissiveTex")) mat.SetTexture("_EmissiveTex", newEmisTex);
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", newEmisTex);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", IdleEmissiveColor);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", IdleEmissiveColor);

                if (bumpPixels != null && mat.HasProperty("_BumpMap"))
                {
                    var newBumpTex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                    newBumpTex.name = "StarPlatinumStone_n";
                    newBumpTex.SetPixels(bumpPixels);
                    newBumpTex.Apply(true, false);
                    mat.SetTexture("_BumpMap", newBumpTex);
                }

                Plugin.Log?.LogInfo("SacrificialStoneManager: Built 1024x1024 Star Platinum stone textures (_MainTex, _EmissiveTex, _BumpMap).");
                return mat;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"SacrificialStoneManager: BuildCustomStoneMaterial failed: {ex}");
                return origMat;
            }
        }

        private static Color[] ReadTexturePixels(Texture src, int width, int height, Color fallback)
        {
            if (src != null)
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                try
                {
                    Graphics.Blit(src, rt);
                    RenderTexture.active = rt;
                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                    Color[] pixels = tex.GetPixels();
                    UnityEngine.Object.Destroy(tex);
                    return pixels;
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"SacrificialStoneManager: ReadTexturePixels fallback: {ex.Message}");
                }
                finally
                {
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                }
            }

            Color[] fill = new Color[width * height];
            for (int i = 0; i < fill.Length; i++) fill[i] = fallback;
            return fill;
        }

        private static Color SampleBilinear(Color[] pixels, int w, int h, float fx, float fy)
        {
            fx = Mathf.Clamp(fx, 0f, w - 1.001f);
            fy = Mathf.Clamp(fy, 0f, h - 1.001f);
            int x0 = (int)fx;
            int y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, w - 1);
            int y1 = Mathf.Min(y0 + 1, h - 1);
            float tx = fx - x0;
            float ty = fy - y0;

            Color c00 = pixels[y0 * w + x0];
            Color c10 = pixels[y0 * w + x1];
            Color c01 = pixels[y1 * w + x0];
            Color c11 = pixels[y1 * w + x1];

            Color c0 = Color.LerpUnclamped(c00, c10, tx);
            Color c1 = Color.LerpUnclamped(c01, c11, tx);
            return Color.LerpUnclamped(c0, c1, ty);
        }

        public static void ScheduleStartTempleSpawnCheck(MonoBehaviour host, Vector3 referencePos)
        {
            if (host == null || s_spawnCheckInProgress) return;
            if (Time.time - s_lastSpawnTime < 3.0f) return;

            host.StartCoroutine(SpawnNearStartTempleCoroutine(referencePos));
        }

        private static IEnumerator SpawnNearStartTempleCoroutine(Vector3 referencePos)
        {
            s_spawnCheckInProgress = true;
            yield return new WaitForSeconds(0.6f);

            try
            {
                TrySpawnNearStartTemple(referencePos);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"SacrificialStoneManager: TrySpawnNearStartTemple error: {ex}");
            }
            finally
            {
                s_spawnCheckInProgress = false;
            }
        }

        public static void TrySpawnNearStartTemple(Vector3 referencePos)
        {
            if (ZNetScene.instance == null) return;
            if (ZNet.instance != null && !ZNet.instance.IsServer()) return;

            RegisterPrefab(ZNetScene.instance);
            if (s_stonePrefab == null) return;

            var allStones = UnityEngine.Object.FindObjectsByType<BossStone>(FindObjectsSortMode.None);
            if (allStones == null || allStones.Length == 0) return;

            var templeStones = new List<BossStone>();
            BossStone yagluthStone = null;

            foreach (var stone in allStones)
            {
                if (stone == null) continue;
                float dist = Vector3.Distance(stone.transform.position, referencePos);
                if (dist > 65f) continue;

                if (IsStarPlatinumStone(stone))
                {
                    // Already spawned at this StartTemple!
                    ConfigureStoneInstance(stone.gameObject);
                    return;
                }

                templeStones.Add(stone);
                if (stone.gameObject.name.StartsWith(BaseStonePrefabName, StringComparison.OrdinalIgnoreCase))
                {
                    yagluthStone = stone;
                }
            }

            // Require at least 3 vanilla BossStones nearby to confirm we are at StartTemple
            if (templeStones.Count < 3) return;

            // Compute circle center & average radius of the StartTemple BossStone ring
            Vector3 center = Vector3.zero;
            foreach (var s in templeStones)
            {
                center += s.transform.position;
            }
            center /= templeStones.Count;

            // If the StartTemple Location component is loaded nearby, use its exact horizontal center
            var locations = UnityEngine.Object.FindObjectsByType<Location>(FindObjectsSortMode.None);
            if (locations != null)
            {
                foreach (var loc in locations)
                {
                    if (loc != null &&
                        (loc.gameObject.name.IndexOf("StartTemple", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         loc.gameObject.name.IndexOf("StartPlatform", StringComparison.OrdinalIgnoreCase) >= 0) &&
                        Vector3.Distance(loc.transform.position, center) < 25f)
                    {
                        center.x = loc.transform.position.x;
                        center.z = loc.transform.position.z;
                        break;
                    }
                }
            }

            // Sort existing BossStones by angle around center
            templeStones.Sort((a, b) =>
            {
                float angA = Mathf.Atan2(a.transform.position.z - center.z, a.transform.position.x - center.x);
                float angB = Mathf.Atan2(b.transform.position.z - center.z, b.transform.position.x - center.x);
                return angA.CompareTo(angB);
            });

            int n = templeStones.Count;
            Vector3 bestPos = Vector3.zero;
            float bestScore = float.MinValue;
            bool foundCandidate = false;

            for (int i = 0; i < n; i++)
            {
                BossStone stoneA = templeStones[i];
                BossStone stoneB = templeStones[(i + 1) % n];

                Vector3 posA = stoneA.transform.position;
                Vector3 posB = stoneB.transform.position;

                float angA = Mathf.Atan2(posA.z - center.z, posA.x - center.x);
                float angB = Mathf.Atan2(posB.z - center.z, posB.x - center.x);
                float gap = angB - angA;
                if (gap <= 0f) gap += Mathf.PI * 2f;

                // Need at least ~32 degrees of arc to fit a BossStone comfortably
                if (gap < 32f * Mathf.Deg2Rad) continue;

                float rA = Vector2.Distance(new Vector2(posA.x, posA.z), new Vector2(center.x, center.z));
                float rB = Vector2.Distance(new Vector2(posB.x, posB.z), new Vector2(center.x, center.z));
                float rGap = 0.5f * (rA + rB);
                float yGap = 0.5f * (posA.y + posB.y);

                // Generate candidate angles in this gap:
                // If the gap is very wide (> 75 deg, e.g. entrance stairway arc), also test ~38 deg from either side
                // so we sit naturally next to the ring stones rather than blocking the middle of the entrance steps.
                var candidateAngles = new List<float>();
                if (gap > 75f * Mathf.Deg2Rad)
                {
                    candidateAngles.Add(angA + 38f * Mathf.Deg2Rad);
                    candidateAngles.Add(angB - 38f * Mathf.Deg2Rad);
                    candidateAngles.Add(angA + gap * 0.5f);
                }
                else
                {
                    candidateAngles.Add(angA + gap * 0.5f);
                }

                foreach (float candAng in candidateAngles)
                {
                    Vector3 candPos = new Vector3(
                        center.x + Mathf.Cos(candAng) * rGap,
                        yGap,
                        center.z + Mathf.Sin(candAng) * rGap);

                    float minDistToStone = float.MaxValue;
                    foreach (var s in templeStones)
                    {
                        float d = Vector3.Distance(candPos, s.transform.position);
                        if (d < minDistToStone) minDistToStone = d;
                    }
                    if (minDistToStone < 3.6f) continue;

                    // Check for any static obstacle collider (like StartTemple's smaller standing menhirs) at chest/head height
                    bool blockedByPillar = false;
                    var cols = Physics.OverlapSphere(candPos + Vector3.up * 2.8f, 1.25f);
                    if (cols != null)
                    {
                        foreach (var col in cols)
                        {
                            if (col == null) continue;
                            if (col.GetComponentInParent<BossStone>() != null) continue;
                            if (col.bounds.max.y > yGap + 1.5f)
                            {
                                blockedByPillar = true;
                                break;
                            }
                        }
                    }

                    float score = minDistToStone * 2.0f + gap * 3.0f;
                    if (blockedByPillar) score -= 50f;
                    if (stoneA == yagluthStone || stoneB == yagluthStone) score += 2.5f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = candPos;
                        foundCandidate = true;
                    }
                }
            }

            if (!foundCandidate)
            {
                // Fallback: place slightly outside the ring next to BossStone_Yagluth
                BossStone refStone = yagluthStone ?? templeStones[0];
                Vector3 radial = refStone.transform.position - center;
                radial.y = 0f;
                if (radial.sqrMagnitude < 0.1f) radial = Vector3.forward * 11f;
                Quaternion rotStep = Quaternion.Euler(0f, 34f, 0f);
                Vector3 offsetRadial = rotStep * radial;
                bestPos = new Vector3(center.x + offsetRadial.x, refStone.transform.position.y, center.z + offsetRadial.z);
            }

            // Compute rotation so the front trophy hook faces directly toward the StartTemple center
            Vector3 toCenter = center - bestPos;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude < 0.001f) toCenter = Vector3.forward;
            toCenter.Normalize();

            Vector3 localHookDir = new Vector3(0.212f, 0f, -1.004f).normalized;
            var prefabStand = s_stonePrefab.GetComponentInChildren<ItemStand>(true);
            if (prefabStand != null)
            {
                Vector3 localStand = s_stonePrefab.transform.InverseTransformPoint(prefabStand.transform.position);
                localStand.y = 0f;
                if (localStand.sqrMagnitude > 0.01f) localHookDir = localStand.normalized;
            }

            Quaternion bestRot = Quaternion.LookRotation(toCenter, Vector3.up) *
                                 Quaternion.Inverse(Quaternion.LookRotation(localHookDir, Vector3.up));

            s_lastSpawnTime = Time.time;
            GameObject spawned = UnityEngine.Object.Instantiate(s_stonePrefab, bestPos, bestRot);
            ConfigureStoneInstance(spawned);

            Plugin.Log?.LogInfo($"SacrificialStoneManager: Spawned '{PrefabName}' at StartTemple position {bestPos} facing center {center}.");
        }
    }

    /// <summary>
    /// Component attached to every BossStone_StarPlatinum instance to keep its visuals,
    /// emission glow, and ItemStand trophy/power bindings active across zone loads.
    /// </summary>
    public class StarPlatinumBossStone : MonoBehaviour
    {
        private BossStone m_bossStone;

        private void Awake()
        {
            m_bossStone = GetComponent<BossStone>();
            SacrificialStoneManager.ConfigureStoneInstance(gameObject);
        }

        private void Start()
        {
            m_bossStone = GetComponent<BossStone>();
            SacrificialStoneManager.ConfigureStoneInstance(gameObject);
            UpdateEmissionAndLight();
        }

        public void UpdateEmissionAndLight()
        {
            if (m_bossStone == null) m_bossStone = GetComponent<BossStone>();
            if (m_bossStone == null) return;

            bool active = m_bossStone.IsActivated();
            Color targetEmis = active
                ? SacrificialStoneManager.ActiveEmissiveColor
                : SacrificialStoneManager.IdleEmissiveColor;

            if (m_bossStone.m_mesh != null && m_bossStone.m_mesh.materials != null &&
                m_bossStone.m_emissiveMaterialIndex >= 0 &&
                m_bossStone.m_emissiveMaterialIndex < m_bossStone.m_mesh.materials.Length)
            {
                Material mat = m_bossStone.m_mesh.materials[m_bossStone.m_emissiveMaterialIndex];
                if (mat != null)
                {
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", targetEmis);
                    if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", targetEmis);
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class SacrificialStonePatches
    {
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        [HarmonyPostfix]
        private static void ZNetScene_Awake_Postfix(ZNetScene __instance)
        {
            SacrificialStoneManager.RegisterPrefab(__instance);
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        [HarmonyPostfix]
        private static void ObjectDB_Awake_Postfix()
        {
            SacrificialStoneManager.RefreshAllConfiguredStones();
        }

        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        [HarmonyPostfix]
        private static void ObjectDB_CopyOtherDB_Postfix()
        {
            SacrificialStoneManager.RefreshAllConfiguredStones();
        }

        [HarmonyPatch(typeof(Location), "Awake")]
        [HarmonyPostfix]
        private static void Location_Awake_Postfix(Location __instance)
        {
            if (__instance == null) return;
            string name = __instance.gameObject.name;
            if (!string.IsNullOrEmpty(name) &&
                (name.IndexOf("StartTemple", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("StartPlatform", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                SacrificialStoneManager.ScheduleStartTempleSpawnCheck(__instance, __instance.transform.position);
            }
        }

        [HarmonyPatch(typeof(BossStone), "Start")]
        [HarmonyPostfix]
        private static void BossStone_Start_Postfix(BossStone __instance)
        {
            if (__instance == null) return;

            if (SacrificialStoneManager.IsStarPlatinumStone(__instance))
            {
                SacrificialStoneManager.ConfigureStoneInstance(__instance.gameObject);
                __instance.GetComponent<StarPlatinumBossStone>()?.UpdateEmissionAndLight();
            }
            else
            {
                SacrificialStoneManager.ScheduleStartTempleSpawnCheck(__instance, __instance.transform.position);
            }
        }

        [HarmonyPatch(typeof(BossStone), "UpdateVisual")]
        [HarmonyPostfix]
        private static void BossStone_UpdateVisual_Postfix(BossStone __instance)
        {
            if (__instance != null && SacrificialStoneManager.IsStarPlatinumStone(__instance))
            {
                __instance.GetComponent<StarPlatinumBossStone>()?.UpdateEmissionAndLight();
            }
        }
    }
}
