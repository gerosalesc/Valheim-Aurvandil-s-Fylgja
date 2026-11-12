using System;
using System.Collections.Generic;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Builds Star Platinum using in-game assets: a semi-transparent, bulked floating player model
    /// wearing the Carapace Breastplate, Carapace Legs, Dvergr Circlet, long black flowing hair,
    /// and a crimson scarf, complete with an exciting manifestation VFX entrance.
    /// </summary>
    internal static class StarPlatinum
    {
        private static readonly Color StandPurple = new Color(0.60f, 0.20f, 0.90f, 0.45f);
        private static readonly Color GoldColor = new Color(1.0f, 0.84f, 0.0f, 0.45f);
        private static readonly Color HairColor   = new Color(0f, 0f, 0f, 0.95f);
        private static readonly Color ScarfRed    = new Color(0.85f, 0.16f, 0.12f, 0.85f);
        private static readonly Color EmissionGlow = new Color(0.40f, 0.15f, 0.75f) * 0.45f;

        public static GameObject Build(Player owner)
        {
            var root = new GameObject("StarPlatinum");
            var follower = root.AddComponent<StandFollower>();
            follower.Owner = owner;

            // Bulked scale: Star Platinum is noticeably larger and more imposing than a normal human
            root.transform.localScale = new Vector3(1.22f, 1.22f, 1.22f);

            // Initial position and rotation matching player facing direction (not camera)
            root.transform.position = owner.transform.TransformPoint(follower.IdleOffset);
            root.transform.rotation = owner.transform.rotation;

            bool builtVisual = false;
            try
            {
                builtVisual = TryBuildAssetModel(root, follower, owner);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"StarPlatinum: Failed to build asset model: {ex}");
            }

            if (!builtVisual)
            {
                Plugin.Log?.LogWarning("StarPlatinum: Falling back to procedural primitive model.");
                BuildPrimitiveFallback(root, follower);
            }

            // Remove all physics colliders from Stand so it floats freely without physics interference
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.Destroy(col);
            }

            // --- Stand Aura Point Light ---
            var lightObj = new GameObject("StandAura");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0, 1.35f, 0);
            var aura = lightObj.AddComponent<Light>();
            aura.type = LightType.Point;
            aura.color = new Color(0.70f, 0.25f, 1.0f);
            aura.range = 4.2f;
            aura.intensity = 1.4f;
            aura.shadows = LightShadows.None;
            follower.Aura = aura;

            // --- Audio Source (ORA Barrage) ---
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0.8f;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 35f;
            audioSource.volume = 1.0f;
            follower.StandAudio = audioSource;

            // --- Exciting Manifestation Entrance VFX & Camera Shake ---
            SpawnEntranceVFX(root.transform.position, root.transform.rotation);

            return root;
        }

        private static void SpawnEntranceVFX(Vector3 position, Quaternion rotation)
        {
            try
            {
                if (ZNetScene.instance != null)
                {
                    string[] vfxCandidates = { "vfx_ghost_spawn", "vfx_spawn_large", "vfx_spawn", "vfx_prespawn" };
                    foreach (var name in vfxCandidates)
                    {
                        var prefab = ZNetScene.instance.GetPrefab(name);
                        if (prefab != null)
                        {
                            UnityEngine.Object.Instantiate(prefab, position, rotation);
                            break;
                        }
                    }
                }

                if (GameCamera.instance != null)
                {
                    GameCamera.instance.AddShake(position, 10f, 0.35f, false);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"StarPlatinum: Entrance VFX failed: {ex.Message}");
            }
        }

        private static bool TryBuildAssetModel(GameObject root, StandFollower follower, Player owner)
        {
            // 1. Locate source player Visual hierarchy
            GameObject sourceVisual = null;
            if (Game.instance != null && Game.instance.m_playerPrefab != null)
            {
                var v = Game.instance.m_playerPrefab.transform.Find("Visual");
                if (v != null) sourceVisual = v.gameObject;
            }
            if (sourceVisual == null && owner != null)
            {
                var v = owner.transform.Find("Visual");
                if (v != null) sourceVisual = v.gameObject;
            }

            if (sourceVisual == null)
            {
                Plugin.Log?.LogWarning("StarPlatinum: Could not find source player Visual.");
                return false;
            }

            // 2. Clone the visual hierarchy
            GameObject visual = UnityEngine.Object.Instantiate(sourceVisual, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Remove unnecessary components on the visual root safely
            var animEvent = visual.GetComponent<CharacterAnimEvent>();
            if (animEvent != null)
            {
                animEvent.enabled = false;
                UnityEngine.Object.Destroy(animEvent);
            }

            var lod = visual.GetComponent<LODGroup>();
            if (lod != null)
            {
                lod.enabled = false;
                UnityEngine.Object.Destroy(lod);
            }

            // Strip any existing clothes/equipment and beards attached to source player
            CleanExistingEquipment(visual);

            // 3. Setup Body SkinnedMeshRenderer
            SkinnedMeshRenderer bodyModel = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            if (bodyModel == null)
            {
                Plugin.Log?.LogWarning("StarPlatinum: SkinnedMeshRenderer not found in visual.");
                return false;
            }

            // Ensure base male/muscular mesh and material
            var ownerVis = owner != null ? owner.GetComponent<VisEquipment>() : null;
            if (ownerVis != null && ownerVis.m_models != null && ownerVis.m_models.Length > 0)
            {
                var baseModel = ownerVis.m_models[0];
                if (baseModel.m_mesh != null) bodyModel.sharedMesh = baseModel.m_mesh;
                if (baseModel.m_baseMaterial != null) bodyModel.material = new Material(baseModel.m_baseMaterial);
            }

            // 4. Equip Carapace Armor (Chest only, no legs)
            EquipArmorPiece("ArmorCarapaceChest", bodyModel, visual);

            // 5. Locate Head bone & Equip Golden Dvergr Circlet and Star Platinum Black Hair (No beard)
            var animator = visual.GetComponent<Animator>();
            Transform headBone = FindBone(bodyModel, visual, HumanBodyBones.Head, "Head");

            if (headBone != null)
            {
                EquipHeadPiece("HelmetDverger", headBone, GoldColor);
                EquipHair(bodyModel, headBone, visual);
            }

            // 6. Apply spectral materials (translucent purple body, gold chest, black hair, black fists)
            ApplySpectralStandMaterials(visual);

            // 8. Wire limb bones and animator to StandFollower
            follower.StandAnimator = animator;
            follower.Head  = headBone;
            follower.Torso = FindBone(bodyModel, visual, HumanBodyBones.Chest, "Chest") ?? visual.transform;
            follower.ArmL  = FindBone(bodyModel, visual, HumanBodyBones.LeftLowerArm, "LeftArm");
            follower.ArmR  = FindBone(bodyModel, visual, HumanBodyBones.RightLowerArm, "RightArm");
            follower.FistL = FindBone(bodyModel, visual, HumanBodyBones.LeftHand, "LeftHand");
            follower.FistR = FindBone(bodyModel, visual, HumanBodyBones.RightHand, "RightHand");

            Plugin.Log?.LogInfo("StarPlatinum: Successfully constructed bulked player model with Carapace armor, Dvergr circlet, long black hair, and red scarf.");
            return true;
        }

        private static void CleanExistingEquipment(GameObject visual)
        {
            // Remove any beard objects in hierarchy
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.gameObject.name.ToLower().Contains("beard"))
                {
                    t.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(t.gameObject);
                }
            }

            for (int i = visual.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = visual.transform.GetChild(i);
                string n = child.gameObject.name;
                if (n == "Armature" || n.StartsWith("body") || n.StartsWith("PlayerModel"))
                {
                    continue;
                }
                if (n.Contains("(Clone)") || n.StartsWith("attach_") || child.GetComponent<ItemDrop>() != null)
                {
                    child.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        private static void EquipScarf(SkinnedMeshRenderer bodyModel, GameObject visualRoot)
        {
            if (ObjectDB.instance == null) return;
            GameObject capePrefab = ObjectDB.instance.GetItemPrefab("CapeLinen");
            if (capePrefab == null)
            {
                Plugin.Log?.LogWarning("StarPlatinum: CapeLinen prefab not found in ObjectDB.");
                return;
            }

            var attachedPieces = new List<GameObject>();

            for (int i = 0; i < capePrefab.transform.childCount; i++)
            {
                Transform child = capePrefab.transform.GetChild(i);
                if (!child.gameObject.name.StartsWith("attach_")) continue;
                string text = child.gameObject.name.Substring(7);

                if (text == "skin")
                {
                    if (bodyModel == null) continue;
                    GameObject skinObj = UnityEngine.Object.Instantiate(child.gameObject, bodyModel.transform.parent);
                    skinObj.name = "StandScarf";
                    skinObj.SetActive(true);
                    skinObj.transform.localPosition = Vector3.zero;
                    skinObj.transform.localRotation = Quaternion.identity;

                    foreach (var smr in skinObj.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        smr.rootBone = bodyModel.rootBone;
                        smr.bones = bodyModel.bones;
                    }
                    attachedPieces.Add(skinObj);
                }
                else
                {
                    Transform boneJoint = Utils.FindChild(visualRoot.transform, text);
                    if (boneJoint != null)
                    {
                        GameObject pieceObj = UnityEngine.Object.Instantiate(child.gameObject, boneJoint);
                        pieceObj.name = "StandScarf";
                        pieceObj.SetActive(true);
                        pieceObj.transform.localPosition = Vector3.zero;
                        pieceObj.transform.localRotation = Quaternion.identity;
                        attachedPieces.Add(pieceObj);
                    }
                }
            }

            // Apply scarlet red tint to the scarf
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_Color"), ScarfRed);
            foreach (var piece in attachedPieces)
            {
                foreach (var r in piece.GetComponentsInChildren<Renderer>(true))
                {
                    r.SetPropertyBlock(block);
                    if (r.material != null)
                    {
                        r.material.color = ScarfRed;
                        if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", ScarfRed);
                    }
                }
            }
        }

        private static void EquipArmorPiece(string itemName, SkinnedMeshRenderer bodyModel, GameObject visualRoot)
        {
            if (ObjectDB.instance == null) return;
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemName);
            if (itemPrefab == null)
            {
                Plugin.Log?.LogWarning($"StarPlatinum: Item prefab '{itemName}' not found in ObjectDB.");
                return;
            }

            ItemDrop drop = itemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
            {
                Material armorMat = drop.m_itemData.m_shared.m_armorMaterial;
                if (armorMat != null && bodyModel != null && bodyModel.material != null)
                {
                    if (armorMat.HasProperty("_ChestTex")) bodyModel.material.SetTexture("_ChestTex", armorMat.GetTexture("_ChestTex"));
                    if (armorMat.HasProperty("_ChestBumpMap")) bodyModel.material.SetTexture("_ChestBumpMap", armorMat.GetTexture("_ChestBumpMap"));
                    if (armorMat.HasProperty("_ChestMetal")) bodyModel.material.SetTexture("_ChestMetal", armorMat.GetTexture("_ChestMetal"));

                    if (armorMat.HasProperty("_LegsTex")) bodyModel.material.SetTexture("_LegsTex", armorMat.GetTexture("_LegsTex"));
                    if (armorMat.HasProperty("_LegsBumpMap")) bodyModel.material.SetTexture("_LegsBumpMap", armorMat.GetTexture("_LegsBumpMap"));
                    if (armorMat.HasProperty("_LegsMetal")) bodyModel.material.SetTexture("_LegsMetal", armorMat.GetTexture("_LegsMetal"));
                }
            }

            for (int i = 0; i < itemPrefab.transform.childCount; i++)
            {
                Transform child = itemPrefab.transform.GetChild(i);
                if (!child.gameObject.name.StartsWith("attach_")) continue;
                string text = child.gameObject.name.Substring(7);

                if (text == "skin")
                {
                    if (bodyModel == null) continue;
                    GameObject skinObj = UnityEngine.Object.Instantiate(child.gameObject, bodyModel.transform.parent);
                    skinObj.SetActive(true);
                    skinObj.transform.localPosition = Vector3.zero;
                    skinObj.transform.localRotation = Quaternion.identity;

                    foreach (var smr in skinObj.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        smr.rootBone = bodyModel.rootBone;
                        smr.bones = bodyModel.bones;
                    }
                }
                else
                {
                    Transform boneJoint = Utils.FindChild(visualRoot.transform, text);
                    if (boneJoint != null)
                    {
                        GameObject pieceObj = UnityEngine.Object.Instantiate(child.gameObject, boneJoint);
                        pieceObj.SetActive(true);
                        pieceObj.transform.localPosition = Vector3.zero;
                        pieceObj.transform.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        private static GameObject EquipHeadPiece(string itemName, Transform headBone, Color? overrideColor)
        {
            if (headBone == null || ObjectDB.instance == null) return null;
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemName);
            if (itemPrefab == null)
            {
                Plugin.Log?.LogWarning($"StarPlatinum: Item prefab '{itemName}' not found in ObjectDB.");
                return null;
            }

            Transform attachSource = null;
            for (int i = 0; i < itemPrefab.transform.childCount; i++)
            {
                Transform child = itemPrefab.transform.GetChild(i);
                if (child.name == "attach" || child.name.StartsWith("attach_"))
                {
                    attachSource = child;
                    break;
                }
            }
            if (attachSource == null) return null;

            Transform equipOffset = itemPrefab.transform.Find("equipoffset");
            Vector3 localPos = equipOffset != null ? equipOffset.localPosition : Vector3.zero;
            Quaternion localRot = equipOffset != null ? equipOffset.localRotation : Quaternion.identity;

            GameObject attached = UnityEngine.Object.Instantiate(attachSource.gameObject, headBone);
            attached.name = "StandCirclet";
            attached.SetActive(true);
            attached.transform.localPosition = localPos;
            attached.transform.localRotation = localRot;

            Transform equipedChild = attached.transform.Find("equiped");
            if (equipedChild != null)
            {
                equipedChild.gameObject.SetActive(true);
            }

            if (overrideColor.HasValue)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor(Shader.PropertyToID("_SkinColor"), overrideColor.Value);
                block.SetColor(Shader.PropertyToID("_Color"), overrideColor.Value);
                foreach (var r in attached.GetComponentsInChildren<Renderer>(true))
                {
                    r.SetPropertyBlock(block);
                    if (r.materials != null)
                    {
                        foreach (var mat in r.materials)
                        {
                            if (mat == null) continue;
                            mat.color = overrideColor.Value;
                            if (mat.HasProperty("_Color")) mat.SetColor("_Color", overrideColor.Value);
                            if (mat.HasProperty("_SkinColor")) mat.SetColor("_SkinColor", overrideColor.Value);
                        }
                    }
                }
            }

            return attached;
        }

        private static GameObject EquipHair(SkinnedMeshRenderer bodyModel, Transform headBone, GameObject visualRoot)
        {
            if (ObjectDB.instance == null) return null;

            // Preferred hair style: Painter Curls (Hair19) as requested
            string[] preferredHairs = new string[]
            {
                "Hair19", // Painter Curls
                "Hair14", // Long flowing locks fallback
                "Hair16", // Long wild hair fallback
                "Hair38", // Voluminous flowing hair fallback
                "Hair11", // Wild Viking mane fallback
                "Hair1"   // Classic long hair fallback
            };

            GameObject hairPrefab = null;
            string selectedHairName = null;

            // 1. Direct prefab lookup
            foreach (var hName in preferredHairs)
            {
                var prefab = ObjectDB.instance.GetItemPrefab(hName);
                if (prefab != null)
                {
                    hairPrefab = prefab;
                    selectedHairName = hName == "Hair19" ? "Painter Curls (Hair19)" : hName;
                    break;
                }
            }

            // 2. Search by localization token ($customization_hair19) or name in customizations
            if (hairPrefab == null)
            {
                var allHairs = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Hair");
                foreach (var h in allHairs)
                {
                    if (h == null) continue;
                    if (h.name == "Hair19" || h.m_itemData?.m_shared?.m_name == "$customization_hair19")
                    {
                        hairPrefab = h.gameObject;
                        selectedHairName = "Painter Curls (Hair19)";
                        break;
                    }
                }
            }

            // 3. General fallback if specific ones not found
            if (hairPrefab == null)
            {
                var allHairs = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Hair");
                foreach (var h in allHairs)
                {
                    if (h != null && !h.name.Contains("None") && !h.name.Contains("_"))
                    {
                        hairPrefab = h.gameObject;
                        selectedHairName = h.name;
                        break;
                    }
                }
            }

            if (hairPrefab == null)
            {
                Plugin.Log?.LogWarning("StarPlatinum: Could not find any Hair prefab in ObjectDB.");
                return null;
            }

            Plugin.Log?.LogInfo($"StarPlatinum: Selected hair style '{selectedHairName}' for Star Platinum.");

            GameObject hairObj = null;

            // Check if prefab has attach_skin or attach
            Transform attachSkin = null;
            Transform attachJoint = null;

            for (int i = 0; i < hairPrefab.transform.childCount; i++)
            {
                Transform child = hairPrefab.transform.GetChild(i);
                if (child.name == "attach_skin" || child.name.StartsWith("attach_skin"))
                {
                    attachSkin = child;
                    break;
                }
                if (child.name == "attach" || child.name.StartsWith("attach_") || child.name.StartsWith("attach"))
                {
                    attachJoint = child;
                }
            }

            if (attachSkin != null)
            {
                // Skinned mesh hair rigged to player bones (standard Valheim hair)
                Transform parent = bodyModel != null && bodyModel.transform.parent != null
                    ? bodyModel.transform.parent
                    : visualRoot.transform;

                hairObj = UnityEngine.Object.Instantiate(attachSkin.gameObject, parent);
                hairObj.name = "StandHair";
                hairObj.SetActive(true);
                hairObj.transform.localPosition = Vector3.zero;
                hairObj.transform.localRotation = Quaternion.identity;
                hairObj.transform.localScale = Vector3.one;

                if (bodyModel != null)
                {
                    foreach (var smr in hairObj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        smr.rootBone = bodyModel.rootBone;
                        smr.bones = bodyModel.bones;
                        smr.updateWhenOffscreen = true;
                    }
                }
            }
            else if (attachJoint != null)
            {
                Transform targetJoint = headBone != null ? headBone : visualRoot.transform;
                hairObj = UnityEngine.Object.Instantiate(attachJoint.gameObject, targetJoint);
                hairObj.name = "StandHair";
                hairObj.SetActive(true);
                hairObj.transform.localPosition = Vector3.zero;
                hairObj.transform.localRotation = Quaternion.identity;
                hairObj.transform.localScale = Vector3.one;

                Transform equipOffset = hairPrefab.transform.Find("equipoffset");
                if (equipOffset != null)
                {
                    hairObj.transform.localPosition += equipOffset.localPosition;
                    hairObj.transform.localRotation *= equipOffset.localRotation;
                }
            }
            else
            {
                Transform targetJoint = headBone != null ? headBone : visualRoot.transform;
                hairObj = UnityEngine.Object.Instantiate(hairPrefab, targetJoint);
                hairObj.name = "StandHair";
                hairObj.SetActive(true);
                hairObj.transform.localPosition = Vector3.zero;
                hairObj.transform.localRotation = Quaternion.identity;
                hairObj.transform.localScale = Vector3.one;
            }

            if (hairObj == null) return null;

            // Remove any cloth/physics components that conflict on stand
            foreach (var comp in hairObj.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName.Contains("Cloth") || typeName.Contains("Collider") || typeName.Contains("WindShelter"))
                {
                    UnityEngine.Object.Destroy(comp);
                }
            }

            // Apply solid pitch black hair color (both _SkinColor and _Color)
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_SkinColor"), HairColor);
            block.SetColor(Shader.PropertyToID("_Color"), HairColor);

            foreach (var r in hairObj.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.SetPropertyBlock(block);

                if (r.materials != null)
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat == null) continue;
                        mat.color = HairColor;
                        if (mat.HasProperty("_SkinColor")) mat.SetColor("_SkinColor", HairColor);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", HairColor);

                        // Keep hair crisp and opaque
                        mat.SetFloat("_Mode", 0f);
                        mat.SetInt("_ZWrite", 1);
                        mat.renderQueue = -1;
                    }
                }
            }

            Plugin.Log?.LogInfo($"StarPlatinum: Successfully attached black hair '{selectedHairName}'!");
            return hairObj;
        }

        private static void ApplySpectralStandMaterials(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                string rName = r.name.ToLower();
                string pName = r.transform.parent != null ? r.transform.parent.name.ToLower() : "";

                bool isHair = rName.Contains("hair") || pName.Contains("hair");
                bool isCirclet = rName.Contains("dverger") || pName.Contains("dverger") || rName.Contains("circlet") || pName.Contains("circlet");
                bool isGlove = rName.Contains("glove") || rName.Contains("hand") || rName.Contains("fist") || pName.Contains("glove") || pName.Contains("hand") || pName.Contains("fist");
                bool isChest = rName.Contains("chest") || pName.Contains("chest") || rName.Contains("carapacechest") || pName.Contains("carapacechest");

                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    if (isHair)
                    {
                        // Star Platinum's black hair: Crisp, opaque, jet black mane
                        mat.SetFloat("_Mode", 0f); // Opaque
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        mat.SetInt("_ZWrite", 1);
                        mat.DisableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = -1;

                        mat.color = HairColor;
                        if (mat.HasProperty("_SkinColor")) mat.SetColor("_SkinColor", HairColor);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", HairColor);
                        continue;
                    }

                    if (isCirclet)
                    {
                        // Golden headband / circlet
                        mat.color = GoldColor;
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", GoldColor);
                        continue;
                    }

                    if (isGlove)
                    {
                        // Gloves/Hands/Fists: pitch black
                        mat.color = HairColor;
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", HairColor);
                        if (mat.HasProperty("_SkinColor")) mat.SetColor("_SkinColor", HairColor);
                        continue;
                    }

                    if (isChest)
                    {
                        // Gold chest armor tint (semi-translucent gold)
                        mat.SetFloat("_Mode", 3f);
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 15;

                        mat.color = GoldColor;
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", GoldColor);
                        continue;
                    }

                    // Otherwise: Stand Body (Spectral Purple, Translucent)
                    mat.SetFloat("_Mode", 3f); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 15;

                    if (mat.HasProperty("_Color"))
                    {
                        Color c = mat.GetColor("_Color");
                        c.r = Mathf.Lerp(c.r, StandPurple.r, 0.45f);
                        c.g = Mathf.Lerp(c.g, StandPurple.g, 0.45f);
                        c.b = Mathf.Lerp(c.b, StandPurple.b, 0.45f);
                        c.a = 0.45f;
                        mat.SetColor("_Color", c);
                    }

                    if (mat.HasProperty("_SkinColor"))
                    {
                        mat.SetColor("_SkinColor", StandPurple);
                    }

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", EmissionGlow);
                        mat.EnableKeyword("_EMISSION");
                    }
                }
                r.materials = mats;
            }
        }

        private static Transform FindBone(SkinnedMeshRenderer smr, GameObject visual, HumanBodyBones boneId, string fallbackName)
        {
            var anim = visual.GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                var b = anim.GetBoneTransform(boneId);
                if (b != null) return b;
            }

            if (smr != null && smr.bones != null)
            {
                string lower = fallbackName.ToLower();
                foreach (var b in smr.bones)
                {
                    if (b != null && b.name.ToLower().Contains(lower)) return b;
                }
            }

            return Utils.FindChild(visual.transform, fallbackName);
        }

        private static void BuildPrimitiveFallback(GameObject root, StandFollower follower)
        {
            var goldTrim = new Color(0.95f, 0.82f, 0.22f);
            follower.Torso = Part(root, PrimitiveType.Capsule, "Torso", new Vector3(0, 1.15f, 0), new Vector3(0.55f, 0.45f, 0.35f), StandPurple);
            follower.Head = Part(root, PrimitiveType.Sphere, "Head", new Vector3(0, 1.75f, 0), new Vector3(0.35f, 0.40f, 0.35f), StandPurple);
            follower.ArmL = Part(root, PrimitiveType.Capsule, "ArmL", new Vector3(-0.48f, 1.10f, 0.05f), new Vector3(0.18f, 0.36f, 0.18f), StandPurple);
            follower.ArmR = Part(root, PrimitiveType.Capsule, "ArmR", new Vector3(0.48f, 1.10f, 0.05f), new Vector3(0.18f, 0.36f, 0.18f), StandPurple);
            follower.FistL = Part(root, PrimitiveType.Sphere, "FistL", new Vector3(-0.48f, 0.68f, 0.15f), new Vector3(0.25f, 0.25f, 0.25f), StandPurple);
            follower.FistR = Part(root, PrimitiveType.Sphere, "FistR", new Vector3(0.48f, 0.68f, 0.15f), new Vector3(0.25f, 0.25f, 0.25f), StandPurple);
        }

        private static Transform Part(GameObject parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

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
