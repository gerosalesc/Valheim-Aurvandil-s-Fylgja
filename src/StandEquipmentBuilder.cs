using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StandsOfValheim
{
    /// <summary>
    /// Builds Star Platinum's custom armor and accessories rigged onto the Stand's humanoid skeleton:
    /// golden shoulder pads, wrapped crimson red scarf, white loincloth, black combat boots, black
    /// gauntlets/gloves, and a golden forehead circlet, without heavy torso/leg armor covering his
    /// purple musculature.
    /// </summary>
    internal static class StandEquipmentBuilder
    {
        public static readonly Color GoldArmorColor      = new Color(0.98f, 0.78f, 0.18f, 1.0f);
        public static readonly Color GoldEmission        = new Color(0.32f, 0.22f, 0.02f);
        public static readonly Color ScarfRedColor       = new Color(0.84f, 0.12f, 0.12f, 1.0f);
        public static readonly Color ScarfEmission       = new Color(0.14f, 0.01f, 0.01f);
        public static readonly Color LeatherBlackColor   = new Color(0.07f, 0.07f, 0.09f, 1.0f);
        public static readonly Color LeatherEmission     = new Color(0.02f, 0.01f, 0.04f);
        public static readonly Color LoinclothWhiteColor = new Color(0.95f, 0.94f, 0.98f, 1.0f);
        public static readonly Color LoinclothEmission   = new Color(0.08f, 0.07f, 0.11f);

        private static Material s_goldMaterial;
        private static Material s_scarfMaterial;
        private static Material s_leatherBlackMaterial;
        private static Material s_loinclothMaterial;

        public static void EquipStandOutfit(GameObject visual, SkinnedMeshRenderer bodyModel, Transform headBone, Func<HumanBodyBones, string, Transform> findBone)
        {
            if (visual == null) return;

            // Golden Shoulder Pads (anchored on UpperArm bones so they swing with punches and blocks)
            Transform leftUpperArm  = findBone(HumanBodyBones.LeftUpperArm, "LeftArm");
            Transform leftLowerArm  = findBone(HumanBodyBones.LeftLowerArm, "LeftForeArm");
            Transform rightUpperArm = findBone(HumanBodyBones.RightUpperArm, "RightArm");
            Transform rightLowerArm = findBone(HumanBodyBones.RightLowerArm, "RightForeArm");
            Transform upperChest    = findBone(HumanBodyBones.UpperChest, "Spine2") ?? findBone(HumanBodyBones.Chest, "Chest");
            Transform neckBone      = findBone(HumanBodyBones.Neck, "Neck");

            AttachShoulderPad(visual, leftUpperArm, leftLowerArm, upperChest, false);
            AttachShoulderPad(visual, rightUpperArm, rightLowerArm, upperChest, true);

            // Wrapped Crimson Red Scarf around neck base & short upper-back tails (not a long cape)
            AttachRedScarf(visual, upperChest, neckBone);

            // Black Gloves & Wrist Cuffs
            Transform leftHand    = findBone(HumanBodyBones.LeftHand, "LeftHand");
            Transform leftMiddle  = findBone(HumanBodyBones.LeftMiddleProximal, "LeftHandMiddle1");
            Transform rightHand   = findBone(HumanBodyBones.RightHand, "RightHand");
            Transform rightMiddle = findBone(HumanBodyBones.RightMiddleProximal, "RightHandMiddle1");

            AttachGlove(visual, leftLowerArm, leftHand, leftMiddle, "StandGlove_L");
            AttachGlove(visual, rightLowerArm, rightHand, rightMiddle, "StandGlove_R");

            // Black Boots (knee-high boot shaft on LowerLeg + foot/toe box on Foot)
            Transform leftLowerLeg  = findBone(HumanBodyBones.LeftLowerLeg, "LeftLeg");
            Transform leftFoot      = findBone(HumanBodyBones.LeftFoot, "LeftFoot");
            Transform leftToes      = findBone(HumanBodyBones.LeftToes, "LeftToeBase");
            Transform rightLowerLeg = findBone(HumanBodyBones.RightLowerLeg, "RightLeg");
            Transform rightFoot     = findBone(HumanBodyBones.RightFoot, "RightFoot");
            Transform rightToes     = findBone(HumanBodyBones.RightToes, "RightToeBase");

            AttachBoot(visual, leftLowerLeg, leftFoot, leftToes, "StandBoot_L");
            AttachBoot(visual, rightLowerLeg, rightFoot, rightToes, "StandBoot_R");

            // White Loincloth (wrapped waist sash + front & back panels anchored on Hips)
            Transform hips       = findBone(HumanBodyBones.Hips, "Hips");
            Transform spine      = findBone(HumanBodyBones.Spine, "Spine");
            Transform leftThigh  = findBone(HumanBodyBones.LeftUpperLeg, "LeftUpLeg");
            Transform rightThigh = findBone(HumanBodyBones.RightUpperLeg, "RightUpLeg");

            AttachLoincloth(visual, hips, spine, leftThigh, rightThigh, leftLowerLeg);

            if (Plugin.Log != null)
            {
                Vector3 sampleBoneScale = hips != null ? hips.lossyScale : visual.transform.lossyScale;
                Plugin.Log.LogInfo($"StandEquipmentBuilder: Equipped outfit (visualScale={visual.transform.lossyScale}, boneScale={sampleBoneScale}).");
            }
        }

        private static void AttachShoulderPad(GameObject visual, Transform upperArm, Transform lowerArm, Transform chest, bool isRight)
        {
            if (upperArm == null) return;

            Vector3 chestPos = chest != null ? chest.position : visual.transform.position + visual.transform.up * 1.35f;
            Vector3 armDown = (lowerArm != null)
                ? (lowerArm.position - upperArm.position).normalized
                : -visual.transform.up;

            // True outward direction from torso center through shoulder, projected perpendicular to arm axis
            Vector3 outward = Vector3.ProjectOnPlane(upperArm.position - chestPos, visual.transform.up);
            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = isRight ? visual.transform.right : -visual.transform.right;
            }
            outward.Normalize();

            Vector3 padUp = (visual.transform.up * 0.88f + outward * 0.25f).normalized;
            Vector3 padForward = Vector3.ProjectOnPlane(visual.transform.forward, outward);
            if (padForward.sqrMagnitude < 0.0001f) padForward = visual.transform.forward;
            padForward.Normalize();

            // Guarantee right-handed orthonormal frame where +X is ALWAYS outward (away from torso)
            Vector3 axisY = Vector3.ProjectOnPlane(padUp, outward).normalized;
            Vector3 axisZ = Vector3.Cross(outward, axisY).normalized; // +forward on Right, -forward on Left (front-back symmetric)
            Quaternion worldRot = Quaternion.LookRotation(axisZ, axisY);

            // Anchor directly at upperArm shoulder joint; mesh curvature wraps flush over the deltoid cap
            var go = CreateGearObject(isRight ? "StandShoulderPad_R" : "StandShoulderPad_L", BuildShoulderPadMesh(), GetGoldMaterial());
            PlaceAnchoredPiece(visual, go, upperArm, upperArm.position, worldRot);
        }

        private static void AttachRedScarf(GameObject visual, Transform upperChest, Transform neckBone)
        {
            Transform parent = upperChest ?? visual.transform;
            Vector3 neckAnchor;
            if (neckBone != null && upperChest != null)
            {
                neckAnchor = Vector3.Lerp(upperChest.position, neckBone.position, 0.72f);
            }
            else if (neckBone != null)
            {
                neckAnchor = neckBone.position - visual.transform.up * (0.045f * GetScaleFactor(visual));
            }
            else
            {
                neckAnchor = visual.transform.position + visual.transform.up * (1.46f * GetScaleFactor(visual));
            }

            Quaternion worldRot = Quaternion.LookRotation(visual.transform.forward, visual.transform.up);
            var go = CreateGearObject("StandScarf", BuildScarfMesh(), GetScarfMaterial());
            PlaceAnchoredPiece(visual, go, parent, neckAnchor, worldRot);
        }

        private static void AttachGlove(GameObject visual, Transform lowerArm, Transform hand, Transform middleBone, string namePrefix)
        {
            if (hand == null) return;

            Vector3 forwardWorld;
            if (middleBone != null && (middleBone.position - hand.position).sqrMagnitude > 0.00001f)
            {
                forwardWorld = (middleBone.position - hand.position).normalized;
            }
            else if (hand.childCount > 0 && (hand.GetChild(0).position - hand.position).sqrMagnitude > 0.00001f)
            {
                forwardWorld = (hand.GetChild(0).position - hand.position).normalized;
            }
            else if (lowerArm != null && (hand.position - lowerArm.position).sqrMagnitude > 0.00001f)
            {
                forwardWorld = (hand.position - lowerArm.position).normalized;
            }
            else
            {
                forwardWorld = -visual.transform.up;
            }

            // Pick a clean stable up reference perpendicular to hand axis
            Vector3 upHint = Vector3.ProjectOnPlane(visual.transform.forward, forwardWorld);
            if (upHint.sqrMagnitude < 0.001f) upHint = Vector3.ProjectOnPlane(visual.transform.right, forwardWorld);
            upHint.Normalize();

            float scale = GetScaleFactor(visual);
            float measuredPalmLen = middleBone != null
                ? Vector3.Distance(hand.position, middleBone.position) / scale
                : 0.085f;
            float fistLength = Mathf.Clamp(measuredPalmLen * 2.0f, 0.145f, 0.195f);

            Quaternion handWorldRot = Quaternion.LookRotation(forwardWorld, upHint);
            var gloveGo = CreateGearObject(namePrefix + "_Fist", BuildGloveFistMesh(fistLength), GetLeatherBlackMaterial());
            PlaceAnchoredPiece(visual, gloveGo, hand, hand.position, handWorldRot);

            // Forearm gauntlet wrist cuff overlapping cleanly at the wrist joint
            if (lowerArm != null)
            {
                Vector3 armAxis = (hand.position - lowerArm.position).normalized; // wrist = +Y if we point Y toward hand
                Vector3 armFwd = Vector3.ProjectOnPlane(visual.transform.forward, armAxis);
                if (armFwd.sqrMagnitude < 0.001f) armFwd = Vector3.ProjectOnPlane(visual.transform.right, armAxis);
                armFwd.Normalize();

                Quaternion cuffWorldRot = Quaternion.LookRotation(armFwd, armAxis);
                var cuffGo = CreateGearObject(namePrefix + "_Cuff", BuildWristCuffMesh(0.115f), GetLeatherBlackMaterial());
                PlaceAnchoredPiece(visual, cuffGo, lowerArm, hand.position, cuffWorldRot);
            }
        }

        private static void AttachBoot(GameObject visual, Transform lowerLeg, Transform foot, Transform toes, string namePrefix)
        {
            if (lowerLeg == null || foot == null) return;

            float scale = GetScaleFactor(visual);
            float shinLen = Vector3.Distance(foot.position, lowerLeg.position) / scale;
            shinLen = Mathf.Clamp(shinLen, 0.28f, 0.55f);

            // Boot shaft on LowerLeg (+Y pointing up from ankle toward knee)
            Vector3 shinUp = (lowerLeg.position - foot.position).normalized;
            Vector3 shinFwd = Vector3.ProjectOnPlane(visual.transform.forward, shinUp).normalized;
            if (shinFwd.sqrMagnitude < 0.001f) shinFwd = visual.transform.forward;

            Quaternion shaftRot = Quaternion.LookRotation(shinFwd, shinUp);
            float shaftHeight = shinLen * 0.76f;
            var shaftGo = CreateGearObject(namePrefix + "_Shaft", BuildBootShaftMesh(shaftHeight), GetLeatherBlackMaterial());
            PlaceAnchoredPiece(visual, shaftGo, lowerLeg, foot.position, shaftRot);

            // Boot foot & toe box on Foot (+Z forward toward toes, +Y up)
            Vector3 toeVec = toes != null
                ? Vector3.ProjectOnPlane(toes.position - foot.position, visual.transform.up)
                : visual.transform.forward * (0.14f * scale);

            float footLen = toeVec.magnitude / scale;
            Vector3 footFwd = toeVec.sqrMagnitude > 0.0001f ? toeVec.normalized : visual.transform.forward;
            float toeTipZ = Mathf.Clamp(footLen + 0.082f, 0.20f, 0.26f);
            float ankleHeight = Mathf.Clamp(visual.transform.InverseTransformPoint(foot.position).y, 0.065f, 0.130f);

            Quaternion footRot = Quaternion.LookRotation(footFwd, visual.transform.up);
            var footGo = CreateGearObject(namePrefix + "_Foot", BuildBootFootMesh(toeTipZ, ankleHeight), GetLeatherBlackMaterial());
            PlaceAnchoredPiece(visual, footGo, foot, foot.position, footRot);
        }

        private static void AttachLoincloth(
            GameObject visual,
            Transform hips,
            Transform spine,
            Transform leftThigh,
            Transform rightThigh,
            Transform leftLowerLeg)
        {
            Transform parent = hips ?? visual.transform;
            float scale = GetScaleFactor(visual);

            Vector3 hipMid;
            float halfHipSpan;
            if (leftThigh != null && rightThigh != null)
            {
                hipMid = 0.5f * (leftThigh.position + rightThigh.position);
                float span = Vector3.ProjectOnPlane(rightThigh.position - leftThigh.position, visual.transform.up).magnitude / scale;
                halfHipSpan = Mathf.Clamp(span * 0.5f, 0.075f, 0.130f);
            }
            else
            {
                hipMid = parent.position;
                halfHipSpan = 0.095f;
            }

            float thighLen = (leftThigh != null && leftLowerLeg != null)
                ? Vector3.Distance(leftThigh.position, leftLowerLeg.position) / scale
                : 0.40f;
            thighLen = Mathf.Clamp(thighLen, 0.32f, 0.48f);

            float hipToSpine = spine != null
                ? Vector3.Dot(spine.position - hipMid, visual.transform.up) / scale
                : 0.13f;
            hipToSpine = Mathf.Clamp(hipToSpine, 0.09f, 0.18f);

            Quaternion worldRot = Quaternion.LookRotation(visual.transform.forward, visual.transform.up);
            var go = CreateGearObject("StandLoincloth", BuildLoinclothMesh(halfHipSpan, hipToSpine, thighLen), GetLoinclothMaterial());
            PlaceAnchoredPiece(visual, go, parent, hipMid, worldRot);
        }

        private static float GetScaleFactor(GameObject visual)
        {
            float s = visual.transform.lossyScale.y;
            return s > 0.01f ? s : 1f;
        }

        private static GameObject CreateGearObject(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static void PlaceAnchoredPiece(GameObject visual, GameObject piece, Transform parentBone, Vector3 worldPos, Quaternion worldRot)
        {
            piece.transform.SetParent(parentBone, false);
            piece.transform.localPosition = parentBone.InverseTransformPoint(worldPos);
            piece.transform.localRotation = Quaternion.Inverse(parentBone.rotation) * worldRot;

            // Valheim's Armature uses 100x Blender-FBX bone scaling; normalize localScale so
            // our 1:1-meter meshes match visual.lossyScale instead of blowing up 100x.
            Vector3 targetScale = visual.transform.lossyScale;
            Vector3 boneScale = parentBone.lossyScale;
            float sx = Mathf.Abs(boneScale.x) > 0.0001f ? targetScale.x / boneScale.x : 1f;
            float sy = Mathf.Abs(boneScale.y) > 0.0001f ? targetScale.y / boneScale.y : 1f;
            float sz = Mathf.Abs(boneScale.z) > 0.0001f ? targetScale.z / boneScale.z : 1f;
            piece.transform.localScale = new Vector3(sx, sy, sz);
        }

        // ----------------------------------------------------------------------
        // Shared Materials
        // ----------------------------------------------------------------------

        public static Material GetGoldMaterial()
        {
            if (s_goldMaterial == null)
            {
                s_goldMaterial = CreateCrispMaterial(GoldArmorColor, metallic: 0.85f, glossiness: 0.72f, GoldEmission);
            }
            return s_goldMaterial;
        }

        public static Material GetScarfMaterial()
        {
            if (s_scarfMaterial == null)
            {
                s_scarfMaterial = CreateCrispMaterial(ScarfRedColor, metallic: 0.05f, glossiness: 0.24f, ScarfEmission);
            }
            return s_scarfMaterial;
        }

        public static Material GetLeatherBlackMaterial()
        {
            if (s_leatherBlackMaterial == null)
            {
                s_leatherBlackMaterial = CreateCrispMaterial(LeatherBlackColor, metallic: 0.18f, glossiness: 0.44f, LeatherEmission);
            }
            return s_leatherBlackMaterial;
        }

        public static Material GetLoinclothMaterial()
        {
            if (s_loinclothMaterial == null)
            {
                s_loinclothMaterial = CreateCrispMaterial(LoinclothWhiteColor, metallic: 0.02f, glossiness: 0.18f, LoinclothEmission);
            }
            return s_loinclothMaterial;
        }

        public static void ApplyGoldTint(Material mat)
        {
            if (mat == null) return;
            ApplyMaterialProperties(mat, GoldArmorColor, metallic: 0.85f, glossiness: 0.72f, new Color(0.42f, 0.28f, 0.03f));
        }

        private static Shader FindValheimOpaqueShader()
        {
            // Never pick Unity's stripped "Standard" or "Diffuse" shaders in Valheim's Vulkan build.
            string[] preferred = { "Custom/Creature", "Custom/Piece" };
            foreach (var name in preferred)
            {
                var sh = Shader.Find(name);
                if (sh != null && sh.isSupported)
                {
                    return sh;
                }
            }

            if (ObjectDB.instance != null)
            {
                string[] itemPrefabs = { "ArmorBronzeChest", "HelmetDverger", "ShieldBronzeBuckler", "ArmorIronChest", "CapeLinen" };
                foreach (var prefabName in itemPrefabs)
                {
                    var prefab = ObjectDB.instance.GetItemPrefab(prefabName);
                    if (prefab == null) continue;
                    foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    {
                        var sh = r != null && r.sharedMaterial != null ? r.sharedMaterial.shader : null;
                        if (sh != null && sh.isSupported && sh.name != "Standard" && sh.name != "Custom/Player")
                        {
                            return sh;
                        }
                    }
                }
            }

            foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>())
            {
                if (sh != null && sh.isSupported && (sh.name == "Custom/Creature" || sh.name == "Custom/Piece"))
                {
                    return sh;
                }
            }

            return null;
        }

        private static Material CreateCrispMaterial(Color color, float metallic, float glossiness, Color emission)
        {
            Material mat = null;
            Shader shader = FindValheimOpaqueShader();
            if (shader != null)
            {
                mat = new Material(shader);
                Plugin.Log?.LogInfo($"StandEquipmentBuilder: Creating equipment material using shader '{shader.name}'.");
            }
            else
            {
                Plugin.Log?.LogWarning("StandEquipmentBuilder: Falling back to StandMaterialManager for equipment material.");
                mat = StandMaterialManager.GetMaterial(color);
            }

            if (mat != null)
            {
                mat.mainTexture = Texture2D.whiteTexture;
                ApplyMaterialProperties(mat, color, metallic, glossiness, emission);
            }
            return mat;
        }

        private static void ApplyMaterialProperties(Material mat, Color color, float metallic, float glossiness, Color emission)
        {
            mat.color = color;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", glossiness);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", glossiness);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
            }
        }

        // ----------------------------------------------------------------------
        // Sculpted Geometry Builders
        // ----------------------------------------------------------------------

        private static Mesh BuildShoulderPadMesh()
        {
            var mesh = new Mesh { name = "StandShoulderPadMesh" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // 1. Main arched sweeping golden pauldron wing (thick curved armor shell)
            // Control stations from inner trap (-0.045 X) out to proud winged tip (+0.178 X)
            var wingStations = new (float x, float y, float zHalfWidth, float halfThick)[]
            {
                (-0.045f,  0.042f, 0.088f, 0.011f),
                ( 0.015f,  0.064f, 0.112f, 0.013f),
                ( 0.078f,  0.056f, 0.118f, 0.014f),
                ( 0.135f,  0.040f, 0.104f, 0.012f),
                ( 0.178f,  0.056f, 0.072f, 0.008f),
            };
            AppendArmorShellStrip(verts, tris, wingStations, 5, ridgeHeight: 0.012f);

            // 2. Lower overlapping deltoid guard lame curving smoothly below the outer edge
            var lowerStations = new (float x, float y, float zHalfWidth, float halfThick)[]
            {
                ( 0.068f,  0.024f, 0.098f, 0.010f),
                ( 0.118f, -0.028f, 0.094f, 0.011f),
                ( 0.132f, -0.078f, 0.076f, 0.009f),
            };
            AppendArmorShellStrip(verts, tris, lowerStations, 5, ridgeHeight: 0.006f);

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildScarfMesh()
        {
            var mesh = new Mesh { name = "StandScarfMesh" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // 1. Primary folded crimson collar loop wrapping neck/clavicles
            var outerRings = new List<Vector3[]>();
            int segs = 20;
            int tubeSegs = 8;
            for (int i = 0; i < segs; i++)
            {
                float theta = (i / (float)segs) * Mathf.PI * 2f;
                float cx = 0.122f * Mathf.Sin(theta);
                float cz = 0.134f * Mathf.Cos(theta);
                float cy = -0.030f * Mathf.Cos(theta) + 0.007f * Mathf.Sin(theta * 3f);
                Vector3 center = new Vector3(cx, cy, cz);
                Vector3 radialOut = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta)).normalized;
                Vector3 up = Vector3.up;

                float rHoriz = 0.030f + 0.005f * Mathf.Cos(theta);
                float rVert = 0.036f + 0.004f * Mathf.Cos(theta * 2f);
                outerRings.Add(CreateEllipseRing(center, radialOut, up, rHoriz, rVert, tubeSegs));
            }
            AppendClosedLoopTorus(verts, tris, outerRings);

            // 2. Inner upper neck fold loop for layered scarf volume
            var innerRings = new List<Vector3[]>();
            for (int i = 0; i < segs; i++)
            {
                float theta = (i / (float)segs) * Mathf.PI * 2f;
                float cx = 0.104f * Mathf.Sin(theta);
                float cz = 0.112f * Mathf.Cos(theta);
                float cy = 0.022f - 0.014f * Mathf.Cos(theta);
                Vector3 center = new Vector3(cx, cy, cz);
                Vector3 radialOut = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta)).normalized;
                innerRings.Add(CreateEllipseRing(center, radialOut, Vector3.up, 0.020f, 0.024f, tubeSegs));
            }
            AppendClosedLoopTorus(verts, tris, innerRings);

            // 3. Tucked front chest drape fold (short, stops above chest center)
            var frontDrape = new List<Vector3[]>();
            frontDrape.Add(CreateEllipseRing(new Vector3(0f, -0.035f, 0.138f), Vector3.right, Vector3.forward, 0.048f, 0.018f, 8));
            frontDrape.Add(CreateEllipseRing(new Vector3(0.010f, -0.095f, 0.140f), Vector3.right, Vector3.forward, 0.042f, 0.014f, 8));
            frontDrape.Add(CreateEllipseRing(new Vector3(0.018f, -0.150f, 0.122f), Vector3.right, Vector3.forward, 0.026f, 0.008f, 8));
            AppendLoftedRings(verts, tris, frontDrape, capStart: true, capEnd: true);

            // 4. Two short sculpted upper-back scarf tails (NOT a long cape!)
            for (int side = -1; side <= 1; side += 2)
            {
                var tail = new List<Vector3[]>();
                Vector3 rightDir = new Vector3(0.92f, 0f, side * 0.25f).normalized;
                Vector3 thickDir = Vector3.Cross(rightDir, Vector3.up).normalized;

                tail.Add(CreateEllipseRing(new Vector3(side * 0.045f, -0.010f, -0.128f), rightDir, thickDir, 0.045f, 0.014f, 8));
                tail.Add(CreateEllipseRing(new Vector3(side * 0.075f, -0.095f, -0.175f), rightDir, thickDir, 0.042f, 0.011f, 8));
                tail.Add(CreateEllipseRing(new Vector3(side * 0.105f, -0.205f, -0.205f), rightDir, thickDir, 0.036f, 0.008f, 8));
                tail.Add(CreateEllipseRing(new Vector3(side * 0.125f, -0.300f, -0.220f), rightDir, thickDir, 0.022f, 0.005f, 8));
                AppendLoftedRings(verts, tris, tail, capStart: true, capEnd: true);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildGloveFistMesh(float fistLength)
        {
            var mesh = new Mesh { name = "StandGloveFistMesh" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            int segs = 10;
            var rings = new List<Vector3[]>
            {
                CreateEllipseRing(new Vector3(0f, 0f, -0.015f), Vector3.right, Vector3.up, 0.056f, 0.048f, segs),
                CreateEllipseRing(new Vector3(0f, 0.004f, fistLength * 0.28f), Vector3.right, Vector3.up, 0.062f, 0.052f, segs),
                CreateEllipseRing(new Vector3(0f, 0.005f, fistLength * 0.56f), Vector3.right, Vector3.up, 0.065f, 0.056f, segs),
                CreateEllipseRing(new Vector3(0f, 0.002f, fistLength * 0.84f), Vector3.right, Vector3.up, 0.058f, 0.050f, segs),
                CreateEllipseRing(new Vector3(0f, -0.002f, fistLength), Vector3.right, Vector3.up, 0.038f, 0.034f, segs),
            };
            AppendLoftedRings(verts, tris, rings, capStart: true, capEnd: true);

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildWristCuffMesh(float length)
        {
            var mesh = new Mesh { name = "StandWristCuffMesh" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            int segs = 10;
            var rings = new List<Vector3[]>
            {
                CreateEllipseRing(new Vector3(0f,  0.015f, 0f), Vector3.right, Vector3.forward, 0.060f, 0.056f, segs),
                CreateEllipseRing(new Vector3(0f, -length * 0.55f, 0f), Vector3.right, Vector3.forward, 0.066f, 0.060f, segs),
                CreateEllipseRing(new Vector3(0f, -length * 0.90f, 0f), Vector3.right, Vector3.forward, 0.072f, 0.066f, segs),
                CreateEllipseRing(new Vector3(0f, -length, 0f), Vector3.right, Vector3.forward, 0.067f, 0.062f, segs),
            };
            AppendLoftedRings(verts, tris, rings, capStart: true, capEnd: true);

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildBootShaftMesh(float height)
        {
            var mesh = new Mesh { name = "StandBootShaftMesh" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            int segs = 10;
            var rings = new List<Vector3[]>
            {
                CreateEllipseRing(new Vector3(0f, -0.025f, 0f), Vector3.right, Vector3.forward, 0.062f, 0.070f, segs),
                CreateEllipseRing(new Vector3(0f, height * 0.25f, -0.002f), Vector3.right, Vector3.forward, 0.068f, 0.076f, segs),
                CreateEllipseRing(new Vector3(0f, height * 0.62f, -0.008f), Vector3.right, Vector3.forward, 0.082f, 0.092f, segs),
                CreateEllipseRing(new Vector3(0f, height * 0.88f, -0.004f), Vector3.right, Vector3.forward, 0.079f, 0.088f, segs),
                CreateEllipseRing(new Vector3(0f, height * 0.96f, 0f), Vector3.right, Vector3.forward, 0.088f, 0.096f, segs),
                CreateEllipseRing(new Vector3(0f, height, 0f), Vector3.right, Vector3.forward, 0.082f, 0.090f, segs),
            };
            AppendLoftedRings(verts, tris, rings, capStart: true, capEnd: true);

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildBootFootMesh(float toeTipZ, float ankleHeight)
        {
            var mesh = new Mesh { name = "StandBootFootMesh" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            float soleY = -(ankleHeight + 0.006f);
            float toeSoleY = soleY + 0.004f;

            // Build boot foot profile along Z axis from heel (-0.075) to rounded armored toe cap (+toeTipZ)
            var stations = new (float z, float yBottom, float yTop, float halfW)[]
            {
                (-0.075f,          soleY,    0.032f, 0.054f), // Heel back
                (-0.015f,          soleY,    0.058f, 0.064f), // Ankle / arch
                (toeTipZ * 0.38f,  soleY,    0.030f, 0.066f), // Instep / bridge
                (toeTipZ * 0.76f,  soleY,   -0.004f, 0.064f), // Ball of foot
                (toeTipZ,          toeSoleY,-0.026f, 0.045f), // Toe box tip
            };

            int segs = 10;
            var rings = new List<Vector3[]>();
            foreach (var st in stations)
            {
                float cy = 0.5f * (st.yBottom + st.yTop);
                float ry = 0.5f * (st.yTop - st.yBottom);
                rings.Add(CreateBootSectionRing(st.z, st.yBottom, st.yTop, st.halfW, cy, ry, segs));
            }
            AppendLoftedRings(verts, tris, rings, capStart: true, capEnd: true);

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildLoinclothMesh(float halfHipSpan, float hipToSpine, float thighLen)
        {
            var mesh = new Mesh { name = "StandLoinclothMesh" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            float waistY = Mathf.Clamp(hipToSpine * 0.54f, 0.048f, 0.088f);
            float waistRx = halfHipSpan + 0.084f;
            float hipRx = halfHipSpan + 0.090f;
            float flapDrop = Mathf.Clamp(thighLen * 0.78f, 0.27f, 0.35f);

            // 1. Two-layer folded white waist sash wrapping snugly around hips/pelvis
            AppendWaistFoldLoop(verts, tris, waistRx, rz: 0.136f, zOffset: -0.006f, baseY: waistY + 0.024f, frontDip: 0.014f, rHoriz: 0.017f, rVert: 0.026f);
            AppendWaistFoldLoop(verts, tris, hipRx,   rz: 0.142f, zOffset: -0.008f, baseY: waistY - 0.024f, frontDip: 0.018f, rHoriz: 0.019f, rVert: 0.036f);

            // 2. Front folded sash overlap band where the front apron anchors
            var sashTieStations = new (float y, float zCenter, float xHalfWidth, float halfThick, float bowZ, float vTipDrop)[]
            {
                (waistY + 0.030f, 0.144f, 0.092f, 0.010f, -0.026f, 0.000f),
                (waistY + 0.002f, 0.152f, 0.100f, 0.012f, -0.026f, 0.000f),
                (waistY - 0.026f, 0.150f, 0.096f, 0.010f, -0.024f, 0.000f),
            };
            AppendClothPanelStrip(verts, tris, sashTieStations, arcSteps: 7, faceDir: +1f, pleatAmp: 0.002f);

            // 3. Sculpted Front Loincloth Panel draping cleanly in front of thighs with V-point hem
            var frontApron = new (float y, float zCenter, float xHalfWidth, float halfThick, float bowZ, float vTipDrop)[]
            {
                (waistY + 0.010f,   0.146f, 0.088f, 0.008f, -0.024f, 0.000f),
                (waistY - 0.038f,   0.154f, 0.096f, 0.009f, -0.022f, 0.000f),
                (-0.040f,           0.160f, 0.100f, 0.009f, -0.018f, 0.000f),
                (-flapDrop * 0.36f, 0.156f, 0.092f, 0.008f, -0.014f, 0.004f),
                (-flapDrop * 0.68f, 0.154f, 0.086f, 0.007f, -0.010f, 0.010f),
                (-flapDrop * 0.90f, 0.160f, 0.094f, 0.006f, -0.008f, 0.018f),
                (-flapDrop,         0.164f, 0.082f, 0.005f, -0.006f, 0.028f),
            };
            AppendClothPanelStrip(verts, tris, frontApron, arcSteps: 7, faceDir: +1f, pleatAmp: 0.0035f);

            // 4. Symmetric Back Loincloth Panel clearing glutes and draping behind upper thighs
            var backApron = new (float y, float zCenter, float xHalfWidth, float halfThick, float bowZ, float vTipDrop)[]
            {
                (waistY + 0.010f,   -0.148f, 0.090f, 0.008f, 0.024f, 0.000f),
                (waistY - 0.038f,   -0.160f, 0.098f, 0.009f, 0.022f, 0.000f),
                (-0.045f,           -0.168f, 0.104f, 0.009f, 0.018f, 0.000f),
                (-flapDrop * 0.36f, -0.162f, 0.094f, 0.008f, 0.014f, 0.004f),
                (-flapDrop * 0.68f, -0.158f, 0.088f, 0.007f, 0.010f, 0.010f),
                (-flapDrop * 0.90f, -0.164f, 0.096f, 0.006f, 0.008f, 0.018f),
                (-flapDrop,         -0.168f, 0.084f, 0.005f, 0.006f, 0.026f),
            };
            AppendClothPanelStrip(verts, tris, backApron, arcSteps: 7, faceDir: -1f, pleatAmp: 0.0035f);

            // 5. Center crotch under-band joining front & back aprons neatly between inner thighs
            float underHalfW = Mathf.Min(halfHipSpan * 0.48f, 0.046f);
            var underBand = new List<Vector3[]>
            {
                CreateEllipseRing(new Vector3(0f, -0.020f,  0.134f), Vector3.right, new Vector3(0f, 0.7f, -0.7f).normalized, underHalfW * 1.25f, 0.011f, 8),
                CreateEllipseRing(new Vector3(0f, -0.062f,  0.065f), Vector3.right, new Vector3(0f, 0.9f, -0.4f).normalized, underHalfW,         0.011f, 8),
                CreateEllipseRing(new Vector3(0f, -0.074f, -0.005f), Vector3.right, Vector3.up,                              underHalfW * 0.85f, 0.011f, 8),
                CreateEllipseRing(new Vector3(0f, -0.064f, -0.068f), Vector3.right, new Vector3(0f, 0.9f,  0.4f).normalized, underHalfW,         0.011f, 8),
                CreateEllipseRing(new Vector3(0f, -0.020f, -0.142f), Vector3.right, new Vector3(0f, 0.7f,  0.7f).normalized, underHalfW * 1.28f, 0.011f, 8),
            };
            AppendLoftedRings(verts, tris, underBand, capStart: true, capEnd: true);

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendWaistFoldLoop(
            List<Vector3> verts,
            List<int> tris,
            float rx,
            float rz,
            float zOffset,
            float baseY,
            float frontDip,
            float rHoriz,
            float rVert)
        {
            const int segs = 24;
            const int tubeSegs = 8;
            var rings = new List<Vector3[]>(segs);

            for (int i = 0; i < segs; i++)
            {
                float theta = (i / (float)segs) * Mathf.PI * 2f;
                float cx = rx * Mathf.Sin(theta);
                float cz = zOffset + rz * Mathf.Cos(theta);
                float cy = baseY - frontDip * Mathf.Cos(theta) + 0.004f * Mathf.Cos(theta * 3f);

                Vector3 center = new Vector3(cx, cy, cz);
                Vector3 radialOut = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta)).normalized;
                rings.Add(CreateEllipseRing(center, radialOut, Vector3.up, rHoriz, rVert, tubeSegs));
            }

            AppendClosedLoopTorus(verts, tris, rings);
        }

        private static void AppendClothPanelStrip(
            List<Vector3> verts,
            List<int> tris,
            (float y, float zCenter, float xHalfWidth, float halfThick, float bowZ, float vTipDrop)[] stations,
            int arcSteps,
            float faceDir,
            float pleatAmp)
        {
            if (stations.Length < 2) return;

            int ringSize = arcSteps * 2;
            int baseVertIdx = verts.Count;

            for (int s = 0; s < stations.Length; s++)
            {
                var st = stations[s];
                var ring = new Vector3[ringSize];

                // Outer face left-to-right (-X to +X)
                for (int j = 0; j < arcSteps; j++)
                {
                    float u = (j / (float)(arcSteps - 1)) * 2f - 1f;
                    float x = u * st.xHalfWidth;
                    float y = st.y - st.vTipDrop * (1f - Mathf.Abs(u));
                    float pleat = pleatAmp * Mathf.Cos(u * Mathf.PI * 2f);
                    float z = st.zCenter + st.bowZ * (u * u) + faceDir * (st.halfThick + pleat);
                    ring[j] = new Vector3(x, y, z);
                }

                // Inner face returning right-to-left (+X to -X)
                for (int j = 0; j < arcSteps; j++)
                {
                    float u = 1f - (j / (float)(arcSteps - 1)) * 2f;
                    float x = u * st.xHalfWidth * 0.94f;
                    float y = st.y - st.vTipDrop * (1f - Mathf.Abs(u));
                    float z = st.zCenter + st.bowZ * (u * u) - faceDir * st.halfThick;
                    ring[arcSteps + j] = new Vector3(x, y, z);
                }

                verts.AddRange(ring);
            }

            Vector3 outerHint = new Vector3(0f, 0f, faceDir);
            Vector3 innerHint = new Vector3(0f, 0f, -faceDir);

            for (int r = 0; r < stations.Length - 1; r++)
            {
                int rowA = baseVertIdx + r * ringSize;
                int rowB = baseVertIdx + (r + 1) * ringSize;

                for (int i = 0; i < ringSize; i++)
                {
                    int next = (i + 1) % ringSize;
                    int a = rowA + i;
                    int b = rowA + next;
                    int c = rowB + next;
                    int d = rowB + i;

                    Vector3 hint;
                    if (i < arcSteps - 1) hint = outerHint;
                    else if (i == arcSteps - 1) hint = Vector3.right;
                    else if (i < ringSize - 1) hint = innerHint;
                    else hint = Vector3.left;

                    EmitTriangleOutward(verts, tris, a, b, c, hint);
                    EmitTriangleOutward(verts, tris, a, c, d, hint);
                }
            }

            // Bridge fabric thickness cleanly across top & bottom hems (avoids pinched/concave center fan)
            int lastRowOffset = baseVertIdx + (stations.Length - 1) * ringSize;
            for (int j = 0; j < arcSteps - 1; j++)
            {
                int topF0 = baseVertIdx + j;
                int topF1 = baseVertIdx + j + 1;
                int topB0 = baseVertIdx + (ringSize - 1 - j);
                int topB1 = baseVertIdx + (ringSize - 2 - j);
                EmitTriangleOutward(verts, tris, topF0, topF1, topB1, Vector3.up);
                EmitTriangleOutward(verts, tris, topF0, topB1, topB0, Vector3.up);

                int botF0 = lastRowOffset + j;
                int botF1 = lastRowOffset + j + 1;
                int botB0 = lastRowOffset + (ringSize - 1 - j);
                int botB1 = lastRowOffset + (ringSize - 2 - j);
                EmitTriangleOutward(verts, tris, botF0, botB1, botF1, Vector3.down);
                EmitTriangleOutward(verts, tris, botF0, botB0, botB1, Vector3.down);
            }
        }

        // ----------------------------------------------------------------------
        // Reusable Mesh / Ring / Outward-Normal Helpers
        // ----------------------------------------------------------------------

        private static Vector3[] CreateEllipseRing(Vector3 center, Vector3 axisU, Vector3 axisV, float rU, float rV, int count)
        {
            var pts = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f;
                pts[i] = center + axisU * (Mathf.Cos(a) * rU) + axisV * (Mathf.Sin(a) * rV);
            }
            return pts;
        }

        private static Vector3[] CreateBootSectionRing(float z, float yBot, float yTop, float halfW, float cy, float ry, int count)
        {
            var pts = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);
                float x = cos * halfW;
                // Flatten bottom slightly so the boot sole sits sturdy on ground
                float y = sin >= 0f ? cy + sin * ry : Mathf.Lerp(cy, yBot, Mathf.Clamp01(-sin * 1.35f));
                pts[i] = new Vector3(x, y, z);
            }
            return pts;
        }

        private static void AppendArmorShellStrip(
            List<Vector3> verts,
            List<int> tris,
            (float x, float y, float zHalfWidth, float halfThick)[] stations,
            int arcSteps,
            float ridgeHeight)
        {
            // Build closed cross-section rings around Z arc + top/bottom thickness at each X station
            var rings = new List<Vector3[]>();
            for (int s = 0; s < stations.Length; s++)
            {
                var st = stations[s];
                int ringSize = arcSteps * 2;
                var ring = new Vector3[ringSize];

                // Top arc from -Z to +Z
                for (int j = 0; j < arcSteps; j++)
                {
                    float u = (j / (float)(arcSteps - 1)) * 2f - 1f; // -1..+1 across Z
                    float z = u * st.zHalfWidth;
                    float archDrop = -0.024f * (u * u);
                    float centerCrest = ridgeHeight * (1f - Mathf.Abs(u));
                    ring[j] = new Vector3(st.x, st.y + st.halfThick + archDrop + centerCrest, z);
                }

                // Bottom arc returning +Z to -Z
                for (int j = 0; j < arcSteps; j++)
                {
                    float u = 1f - (j / (float)(arcSteps - 1)) * 2f; // +1..-1 across Z
                    float z = u * st.zHalfWidth * 0.94f;
                    float archDrop = -0.022f * (u * u);
                    ring[arcSteps + j] = new Vector3(st.x, st.y - st.halfThick + archDrop, z);
                }

                rings.Add(ring);
            }

            AppendLoftedRings(verts, tris, rings, capStart: true, capEnd: true);
        }

        private static void AppendLoftedRings(List<Vector3> verts, List<int> tris, List<Vector3[]> rings, bool capStart, bool capEnd)
        {
            if (rings.Count < 2) return;
            int n = rings[0].Length;
            int baseVertIdx = verts.Count;

            for (int r = 0; r < rings.Count; r++)
            {
                verts.AddRange(rings[r]);
            }

            for (int r = 0; r < rings.Count - 1; r++)
            {
                int rowA = baseVertIdx + r * n;
                int rowB = baseVertIdx + (r + 1) * n;
                Vector3 centroid = 0.5f * (RingCenter(rings[r]) + RingCenter(rings[r + 1]));

                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    int a = rowA + i;
                    int b = rowA + next;
                    int c = rowB + next;
                    int d = rowB + i;

                    Vector3 outward = (0.25f * (verts[a] + verts[b] + verts[c] + verts[d])) - centroid;
                    EmitTriangleOutward(verts, tris, a, b, c, outward);
                    EmitTriangleOutward(verts, tris, a, c, d, outward);
                }
            }

            if (capStart)
            {
                Vector3 capCenter = RingCenter(rings[0]);
                Vector3 nextCenter = RingCenter(rings[1]);
                Vector3 outward = (capCenter - nextCenter).normalized;
                int centerIdx = verts.Count;
                verts.Add(capCenter);
                int row0 = baseVertIdx;
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    EmitTriangleOutward(verts, tris, centerIdx, row0 + next, row0 + i, outward);
                }
            }

            if (capEnd)
            {
                int lastR = rings.Count - 1;
                Vector3 capCenter = RingCenter(rings[lastR]);
                Vector3 prevCenter = RingCenter(rings[lastR - 1]);
                Vector3 outward = (capCenter - prevCenter).normalized;
                int centerIdx = verts.Count;
                verts.Add(capCenter);
                int rowLast = baseVertIdx + lastR * n;
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    EmitTriangleOutward(verts, tris, centerIdx, rowLast + i, rowLast + next, outward);
                }
            }
        }

        private static void AppendClosedLoopTorus(List<Vector3> verts, List<int> tris, List<Vector3[]> rings)
        {
            if (rings.Count < 3) return;
            int count = rings.Count;
            int n = rings[0].Length;
            int baseVertIdx = verts.Count;

            for (int r = 0; r < count; r++)
            {
                verts.AddRange(rings[r]);
            }

            for (int r = 0; r < count; r++)
            {
                int rNext = (r + 1) % count;
                int rowA = baseVertIdx + r * n;
                int rowB = baseVertIdx + rNext * n;
                Vector3 centroid = 0.5f * (RingCenter(rings[r]) + RingCenter(rings[rNext]));

                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    int a = rowA + i;
                    int b = rowA + next;
                    int c = rowB + next;
                    int d = rowB + i;

                    Vector3 outward = (0.25f * (verts[a] + verts[b] + verts[c] + verts[d])) - centroid;
                    EmitTriangleOutward(verts, tris, a, b, c, outward);
                    EmitTriangleOutward(verts, tris, a, c, d, outward);
                }
            }
        }

        private static Vector3 RingCenter(Vector3[] ring)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < ring.Length; i++) sum += ring[i];
            return sum / ring.Length;
        }

        private static void EmitTriangleOutward(List<Vector3> verts, List<int> tris, int i0, int i1, int i2, Vector3 outwardHint)
        {
            Vector3 normal = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
            if (Vector3.Dot(normal, outwardHint) >= 0f)
            {
                tris.Add(i0);
                tris.Add(i1);
                tris.Add(i2);
            }
            else
            {
                tris.Add(i0);
                tris.Add(i2);
                tris.Add(i1);
            }
        }
    }
}
