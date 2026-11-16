using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Replaces Tahla Venn's rigid hanging vestments with a slim, form-fitting Royal Sanctuary
    /// armor suite covering both the front and back of her torso, waist, arms, thighs, knees, and calves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealerLegArmorAppearance : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private Material[] armorPalette;

        private const int SubIvory = 0;
        private const int SubGold = 1;
        private const int SubJade = 2;
        private const int SubEmerald = 3;

        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<HealerLegArmorAppearance>() != null) return;
            SkinnedMeshRenderer anatomy = null;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == "DF_Anatomy") anatomy = r;
                else if (r.name.StartsWith("DF_ArticulatedArmorDetails", StringComparison.Ordinal))
                    r.enabled = false;
            }
            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            Transform hips = Array.Find(bones, b => b.name == "Hips");
            if (anatomy == null || anatomy.sharedMesh == null || hips == null) return;
            foreach (string side in new[] { "L", "R" })
                foreach (string part in new[] { "Thigh", "Shin", "Foot" })
                    if (Array.Find(bones, b => b.name == part + side) == null) return;

            var owner = model.gameObject.AddComponent<HealerLegArmorAppearance>();
            owner.RemoveVestments(anatomy, hips, model);
            owner.UpgradeBaseSuitMaterials(anatomy);

            Material ivory = RuntimeShaderLibrary.Create(new Color(0.89f, 0.92f, 0.88f, 1f), false, 0.28f, 0.72f, Color.black);
            Material gold = RuntimeShaderLibrary.Create(new Color(0.80f, 0.59f, 0.22f, 1f), false, 0.84f, 0.75f, Color.black);
            Material jade = RuntimeShaderLibrary.Create(new Color(0.075f, 0.18f, 0.15f, 1f), false, 0.45f, 0.58f, Color.black);
            Color emeraldColor = new Color(0.12f, 0.98f, 0.38f, 1f);
            Material emerald = RuntimeShaderLibrary.Create(emeraldColor, false, 0.16f, 0.80f, emeraldColor * 2.4f);
            if (ivory == null || gold == null || jade == null || emerald == null) return;

            ivory.name = "DF_Tahla_PearlIvoryEnamel";
            gold.name = "DF_Tahla_ImperialBurnishedGold";
            jade.name = "DF_Tahla_SanctuaryObsidianJade";
            emerald.name = "DF_Tahla_RadiantEmeraldCore";
            emerald.EnableKeyword("_EMISSION");

            owner.materials.Add(ivory);
            owner.materials.Add(gold);
            owner.materials.Add(jade);
            owner.materials.Add(emerald);
            owner.armorPalette = new[] { ivory, gold, jade, emerald };

            Transform spine = Array.Find(bones, b => b.name == "Spine");
            Transform chest = Array.Find(bones, b => b.name == "Chest");

            // Chest bone is at upper sternum; ribcage runs from Chest down toward Spine.
            if (chest != null && spine != null)
                owner.BuildChestCuirass(anatomy, chest, spine.position - chest.position, model.forward);
            if (spine != null && hips != null)
                owner.BuildSpineCorset(anatomy, spine, hips.position - spine.position, model.forward);
            if (hips != null && spine != null)
                owner.BuildWaistAndTassets(anatomy, hips, hips.position - spine.position, model.forward);

            foreach (string side in new[] { "L", "R" })
            {
                float sign = side == "L" ? -1f : 1f;
                Transform upperArm = Array.Find(bones, b => b.name == "UpperArm" + side);
                Transform forearm = Array.Find(bones, b => b.name == "Forearm" + side);
                Transform hand = Array.Find(bones, b => b.name == "Hand" + side);
                if (upperArm != null && forearm != null)
                    owner.BuildPauldron(anatomy, "DF_TahlaPauldron" + side, upperArm, forearm.position, model.forward, model.right * sign);
                if (forearm != null && hand != null)
                    owner.BuildVambrace(anatomy, "DF_TahlaVambrace" + side, forearm, hand.position, model.forward, model.right * sign);

                Transform thigh = Array.Find(bones, b => b.name == "Thigh" + side);
                Transform shin = Array.Find(bones, b => b.name == "Shin" + side);
                Transform foot = Array.Find(bones, b => b.name == "Foot" + side);
                owner.BuildThighCuisse(anatomy, "DF_HealerThighPlate" + side, thigh, shin.position, model.forward);
                owner.BuildKneePoleyn(anatomy, "DF_HealerKneeGuard" + side, shin, foot.position, model.forward);
                owner.BuildShinGreave(anatomy, "DF_HealerShinPlate" + side, shin, foot.position, model.forward);
            }
        }

        private void UpgradeBaseSuitMaterials(SkinnedMeshRenderer anatomy)
        {
            Material[] slots = anatomy.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                Material src = slots[i];
                if (src == null) continue;
                string n = src.name;
                if (n.StartsWith("Tahla warm ivory silk", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Porcelain", StringComparison.OrdinalIgnoreCase))
                {
                    Material m = new Material(src) { name = n + "_SanctuaryIvory" };
                    Color c = new Color(0.86f, 0.90f, 0.86f, 1f);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    m.color = c;
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.24f);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.68f);
                    materials.Add(m);
                    slots[i] = m;
                    changed = true;
                }
                else if (n.StartsWith("Tahla engraved warm gold", StringComparison.OrdinalIgnoreCase) ||
                         n.StartsWith("Champagne metal", StringComparison.OrdinalIgnoreCase))
                {
                    Material m = new Material(src) { name = n + "_SanctuaryGold" };
                    Color c = new Color(0.82f, 0.61f, 0.23f, 1f);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    m.color = c;
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.85f);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.76f);
                    materials.Add(m);
                    slots[i] = m;
                    changed = true;
                }
                else if (n.StartsWith("Brushed silver", StringComparison.OrdinalIgnoreCase) ||
                         n.StartsWith("Signature enamel", StringComparison.OrdinalIgnoreCase))
                {
                    Material m = new Material(src) { name = n + "_SanctuaryJade" };
                    Color c = new Color(0.11f, 0.24f, 0.20f, 1f);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    m.color = c;
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.52f);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.64f);
                    materials.Add(m);
                    slots[i] = m;
                    changed = true;
                }
            }
            if (changed) anatomy.sharedMaterials = slots;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        private void RemoveVestments(SkinnedMeshRenderer renderer, Transform hips, Transform model)
        {
            Mesh source = renderer.sharedMesh;
            if (!source.isReadable) { Debug.LogError("DF: Healer anatomy must be readable for vestment replacement.", this); return; }
            int n = source.vertexCount;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            var triangles = new int[source.subMeshCount][];
            for (int s = 0; s < triangles.Length; s++)
            {
                triangles[s] = source.GetTriangles(s);
                for (int i = 0; i < triangles[s].Length; i += 3)
                {
                    int a = Find(parent, triangles[s][i]);
                    parent[Find(parent, triangles[s][i + 1])] = a;
                    parent[Find(parent, triangles[s][i + 2])] = a;
                }
            }
            var baked = new Mesh();
            renderer.BakeMesh(baked);
            Vector3[] points = baked.vertices;
            BoneWeight[] weights = source.boneWeights;
            var rigid = new bool[n];
            var below = new bool[n];
            var low = new float[n];
            var high = new float[n];
            for (int i = 0; i < n; i++) { rigid[i] = true; low[i] = float.MaxValue; high[i] = float.MinValue; }
            Transform head = Array.Find(renderer.bones, b => b != null && b.name == "Head");
            if (head == null || weights.Length != n || points.Length != n) { Destroy(baked); return; }
            float unit = Vector3.Distance(hips.position, head.position);
            for (int i = 0; i < n; i++)
            {
                int group = Find(parent, i);
                BoneWeight w = weights[i];
                string name = renderer.bones[w.boneIndex0].name;
                if (w.weight0 < .999f || (name != "Hips" && name != "Chest" && name != "Spine")) rigid[group] = false;
                Vector3 world = renderer.transform.TransformPoint(points[i]);
                float height = Vector3.Dot(world - hips.position, model.up);
                low[group] = Mathf.Min(low[group], height);
                high[group] = Mathf.Max(high[group], height);
                if (height < -unit * .15f) below[group] = true;
            }
            Destroy(baked);
            Mesh copy = Instantiate(source);
            copy.name = "DF_HealerWithoutLongVestments";
            meshes.Add(copy);
            for (int s = 0; s < triangles.Length; s++)
            {
                var keep = new List<int>();
                int[] ids = triangles[s];
                for (int i = 0; i < ids.Length; i += 3)
                {
                    int group = Find(parent, ids[i]);
                    if (rigid[group] && (below[group] || (low[group] < unit * .16f && high[group] - low[group] > unit * .40f))) continue;
                    keep.Add(ids[i]);
                    keep.Add(ids[i + 1]);
                    keep.Add(ids[i + 2]);
                }
                copy.SetTriangles(keep, s, false);
            }
            renderer.sharedMesh = copy;
        }

        private void BuildChestCuirass(SkinnedMeshRenderer anatomy, Transform chest, Vector3 downRibcageAxis, Vector3 modelForward)
        {
            var b = new ArmorBuilder(anatomy, chest, downRibcageAxis, modelForward, 0.36f, 0.32f, 0.44f);
            // 1. Sleek Front Sternum Breastplate & Sanctuary Heart Crystal (fitted flush to ribcage)
            b.CurvedPlate(0.05f, 0.78f, -0.92f, 0.92f, -0.72f, 0.72f, 1.015f, 1.012f, 0.018f, 0.016f, SubIvory, SubGold, 10);
            b.FacetedGem(0.36f, 0f, 1.032f, 0.085f, 0.125f, 0.022f, SubGold, SubEmerald);

            // 2. Contoured Dorsal Backplate & Spinal Aether Nodes (fitted flush to upper back)
            b.CurvedPlate(0.02f, 0.85f, 2.32f, 3.96f, 2.46f, 3.82f, 1.015f, 1.012f, 0.018f, 0.018f, SubIvory, SubGold, 10);
            // Left & right scapula gold trim plates on upper back
            b.CurvedPlate(0.08f, 0.56f, 2.12f, 2.68f, 2.24f, 2.62f, 1.022f, 1.018f, 0.015f, 0.010f, SubGold, SubJade, 6);
            b.CurvedPlate(0.08f, 0.56f, 3.60f, 4.16f, 3.66f, 4.04f, 1.022f, 1.018f, 0.015f, 0.010f, SubGold, SubJade, 6);
            b.FacetedGem(0.32f, Mathf.PI, 1.034f, 0.080f, 0.110f, 0.020f, SubGold, SubEmerald);
            b.FacetedGem(0.64f, Mathf.PI, 1.030f, 0.068f, 0.090f, 0.018f, SubGold, SubEmerald);

            Attach("DF_TahlaRoyalCuirass", chest, b);
        }

        private void BuildSpineCorset(SkinnedMeshRenderer anatomy, Transform spine, Vector3 downWaistAxis, Vector3 modelForward)
        {
            var b = new ArmorBuilder(anatomy, spine, downWaistAxis, modelForward, 0.34f, 0.30f, 0.40f);
            // Sleek front abdominal chevron plate
            b.CurvedPlate(0.10f, 0.85f, -0.82f, 0.82f, -0.64f, 0.64f, 1.014f, 1.012f, 0.016f, 0.014f, SubJade, SubGold, 8);
            // Sleek rear lumbar spine plate + emerald node
            b.CurvedPlate(0.08f, 0.88f, 2.40f, 3.88f, 2.50f, 3.78f, 1.015f, 1.012f, 0.018f, 0.016f, SubIvory, SubGold, 8);
            b.FacetedGem(0.48f, Mathf.PI, 1.032f, 0.072f, 0.105f, 0.020f, SubGold, SubEmerald);
            Attach("DF_TahlaCorsetArmor", spine, b);
        }

        private void BuildWaistAndTassets(SkinnedMeshRenderer anatomy, Transform hips, Vector3 downPelvisAxis, Vector3 modelForward)
        {
            var b = new ArmorBuilder(anatomy, hips, downPelvisAxis, modelForward, 0.38f, 0.36f, 0.48f);
            // Sleek Rear Pelvis Guard Plate with gold border and emerald clasp
            b.CurvedPlate(0.04f, 0.72f, 2.38f, 3.90f, 2.52f, 3.76f, 1.016f, 1.012f, 0.018f, 0.016f, SubIvory, SubGold, 8);
            b.FacetedGem(0.28f, Mathf.PI, 1.034f, 0.085f, 0.105f, 0.022f, SubGold, SubEmerald);

            // Slim Left & Right Hip Faulds (tightly hugging the outer hips)
            for (int side = -1; side <= 1; side += 2)
            {
                float c = side * 1.48f;
                b.CurvedPlate(0.08f, 0.68f, c - 0.52f, c + 0.52f, c - 0.38f, c + 0.38f, 1.018f, 1.014f, 0.018f, 0.015f, SubIvory, SubGold, 8);
                b.FacetedGem(0.32f, c, 1.034f, 0.065f, 0.095f, 0.018f, SubGold, SubEmerald);
            }
            Attach("DF_TahlaWaistAndTassets", hips, b);
        }

        private void BuildPauldron(SkinnedMeshRenderer anatomy, string name, Transform upperArm, Vector3 forearmPos, Vector3 modelForward, Vector3 outwardDir)
        {
            Vector3 axis = forearmPos - upperArm.position;
            Vector3 centerDir = (Vector3.ProjectOnPlane(outwardDir, axis.normalized).normalized * 0.85f +
                                 Vector3.ProjectOnPlane(modelForward, axis.normalized).normalized * 0.52f).normalized;
            var b = new ArmorBuilder(anatomy, upperArm, axis, centerDir, 0.155f, 0.155f, 0.155f);

            // Slim, contoured outer-shoulder cap + rear tricep guard plate
            b.CurvedPlate(0.02f, 0.44f, -0.92f, 0.92f, -0.72f, 0.72f, 1.022f, 1.015f, 0.020f, 0.018f, SubIvory, SubGold, 8);
            b.FacetedGem(0.18f, 0f, 1.042f, 0.068f, 0.095f, 0.018f, SubGold, SubEmerald);
            // Rear tricep armor plate
            b.CurvedPlate(0.28f, 0.72f, 2.48f, 3.80f, 2.58f, 3.70f, 1.018f, 1.012f, 0.018f, 0.014f, SubIvory, SubGold, 6);

            Attach(name, upperArm, b);
        }

        private void BuildVambrace(SkinnedMeshRenderer anatomy, string name, Transform forearm, Vector3 handPos, Vector3 modelForward, Vector3 outwardDir)
        {
            Vector3 axis = handPos - forearm.position;
            Vector3 centerDir = (Vector3.ProjectOnPlane(outwardDir, axis.normalized).normalized * 0.78f +
                                 Vector3.ProjectOnPlane(modelForward, axis.normalized).normalized * 0.62f).normalized;
            var b = new ArmorBuilder(anatomy, forearm, axis, centerDir, 0.125f, 0.125f, 0.125f);

            // Slim outer vambrace + inner/rear forearm bracer plate
            b.CurvedPlate(0.14f, 0.78f, -0.88f, 0.88f, -0.74f, 0.74f, 1.020f, 1.014f, 0.018f, 0.016f, SubIvory, SubGold, 8);
            b.FacetedGem(0.44f, 0f, 1.038f, 0.048f, 0.18f, 0.015f, SubGold, SubEmerald);
            b.CurvedPlate(0.18f, 0.74f, 2.50f, 3.78f, 2.60f, 3.68f, 1.016f, 1.012f, 0.016f, 0.012f, SubJade, SubGold, 6);

            Attach(name, forearm, b);
        }

        private void BuildThighCuisse(SkinnedMeshRenderer anatomy, string name, Transform thigh, Vector3 shinPos, Vector3 modelForward)
        {
            var b = new ArmorBuilder(anatomy, thigh, shinPos - thigh.position, modelForward, 0.145f, 0.145f, 0.145f);
            // 1. Slim, form-fitting Pearl-Ivory Front Cuisse with fine Gold border & Emerald seam
            b.CurvedPlate(0.12f, 0.78f, -0.86f, 0.86f, -0.70f, 0.70f, 1.018f, 1.012f, 0.018f, 0.016f, SubIvory, SubGold, 8);
            b.FacetedGem(0.45f, 0f, 1.036f, 0.042f, 0.22f, 0.014f, SubGold, SubEmerald);
            // 2. Slim Contoured Rear Hamstring Plate (so the back of the thigh is armored too!)
            b.CurvedPlate(0.16f, 0.76f, 2.46f, 3.82f, 2.56f, 3.72f, 1.016f, 1.012f, 0.016f, 0.014f, SubIvory, SubGold, 8);
            b.FacetedGem(0.46f, Mathf.PI, 1.032f, 0.040f, 0.18f, 0.012f, SubGold, SubEmerald);

            Attach(name, thigh, b);
        }

        private void BuildKneePoleyn(SkinnedMeshRenderer anatomy, string name, Transform shin, Vector3 footPos, Vector3 modelForward)
        {
            var b = new ArmorBuilder(anatomy, shin, footPos - shin.position, modelForward, 0.122f, 0.122f, 0.122f);
            // Sleek, low-profile Golden Poleyn Knee Cap + Rear Knee-Joint Guard
            b.CurvedPlate(-0.06f, 0.14f, -0.76f, 0.76f, -0.64f, 0.64f, 1.024f, 1.018f, 0.020f, 0.022f, SubGold, SubIvory, 8);
            b.FacetedGem(0.04f, 0f, 1.048f, 0.052f, 0.080f, 0.016f, SubGold, SubEmerald);
            b.CurvedPlate(-0.04f, 0.12f, 2.56f, 3.72f, 2.62f, 3.66f, 1.018f, 1.014f, 0.016f, 0.012f, SubJade, SubGold, 6);

            Attach(name, shin, b);
        }

        private void BuildShinGreave(SkinnedMeshRenderer anatomy, string name, Transform shin, Vector3 footPos, Vector3 modelForward)
        {
            var b = new ArmorBuilder(anatomy, shin, footPos - shin.position, modelForward, 0.115f, 0.115f, 0.115f);
            // 1. Slim, tapered Pearl-Ivory Front Shin Greave with Gold border & Emerald seam
            b.CurvedPlate(0.18f, 0.86f, -0.84f, 0.84f, -0.68f, 0.68f, 1.018f, 1.012f, 0.018f, 0.016f, SubIvory, SubGold, 8);
            b.FacetedGem(0.50f, 0f, 1.036f, 0.038f, 0.24f, 0.014f, SubGold, SubEmerald);
            // 2. Slim, tapered Rear Calf Greave Plate with Gold trim & Emerald node
            b.CurvedPlate(0.20f, 0.80f, 2.44f, 3.84f, 2.58f, 3.70f, 1.016f, 1.012f, 0.016f, 0.014f, SubIvory, SubGold, 8);
            b.FacetedGem(0.48f, Mathf.PI, 1.032f, 0.038f, 0.20f, 0.012f, SubGold, SubEmerald);

            Attach(name, shin, b);
        }

        private void Attach(string name, Transform bone, ArmorBuilder builder)
        {
            Mesh mesh = builder.ToMesh(name);
            meshes.Add(mesh);
            var go = new GameObject(name) { layer = bone.gameObject.layer };
            go.transform.SetParent(bone, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = armorPalette;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var m in materials) if (m != null) Destroy(m);
        }

        private sealed class ArmorBuilder
        {
            private struct SamplePt
            {
                public float t;
                public float f;
                public float s;
            }

            private readonly Transform bone;
            private readonly Vector3 origin;
            private readonly Vector3 axis;
            private readonly float length;
            private readonly Vector3 down;
            private readonly Vector3 front;
            private readonly Vector3 right;
            private readonly float fallbackFront;
            private readonly float fallbackBack;
            private readonly float fallbackSide;
            private readonly List<SamplePt> samples = new List<SamplePt>();
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] submeshes = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

            public ArmorBuilder(
                SkinnedMeshRenderer anatomy,
                Transform bone,
                Vector3 axisWorld,
                Vector3 forwardRef,
                float defaultFrontRatio,
                float defaultBackRatio,
                float defaultSideRatio)
            {
                this.bone = bone;
                origin = bone.position;
                axis = axisWorld;
                length = Mathf.Max(0.05f, axis.magnitude);
                down = axis / length;
                front = Vector3.ProjectOnPlane(forwardRef, down).normalized;
                if (front.sqrMagnitude < 0.001f) front = Vector3.ProjectOnPlane(Vector3.forward, down).normalized;
                right = Vector3.Cross(down, front).normalized;

                fallbackFront = length * defaultFrontRatio;
                fallbackBack = length * defaultBackRatio;
                fallbackSide = length * defaultSideRatio;

                if (anatomy != null && anatomy.sharedMesh != null && anatomy.sharedMesh.isReadable)
                {
                    int idx = Array.IndexOf(anatomy.bones, bone);
                    Mesh src = anatomy.sharedMesh;
                    if (idx >= 0 && idx < src.bindposes.Length && src.boneWeights.Length == src.vertexCount)
                    {
                        Matrix4x4 bind = src.bindposes[idx];
                        Vector3[] pts = src.vertices;
                        BoneWeight[] bw = src.boneWeights;
                        for (int i = 0; i < pts.Length; i++)
                        {
                            BoneWeight w = bw[i];
                            float inf = (w.boneIndex0 == idx ? w.weight0 : 0f) +
                                        (w.boneIndex1 == idx ? w.weight1 : 0f) +
                                        (w.boneIndex2 == idx ? w.weight2 : 0f) +
                                        (w.boneIndex3 == idx ? w.weight3 : 0f);
                            if (inf < 0.45f) continue;
                            Vector3 worldPt = bone.TransformPoint(bind.MultiplyPoint3x4(pts[i]));
                            Vector3 rel = worldPt - origin;
                            samples.Add(new SamplePt
                            {
                                t = Vector3.Dot(rel, down) / length,
                                f = Vector3.Dot(rel, front),
                                s = Mathf.Abs(Vector3.Dot(rel, right))
                            });
                        }
                    }
                }
            }

            private void GetSliceRadii(float t, out float rFront, out float rBack, out float rSide)
            {
                rFront = fallbackFront * Mathf.Lerp(1.0f, 0.82f, Mathf.Clamp01(t));
                rBack = fallbackBack * Mathf.Lerp(1.0f, 0.82f, Mathf.Clamp01(t));
                rSide = fallbackSide * Mathf.Lerp(1.0f, 0.82f, Mathf.Clamp01(t));
                if (samples.Count < 6) return;

                float maxF = 0f, maxB = 0f, maxS = 0f;
                int hits = 0;
                for (int pass = 0; pass < 2 && hits < 4; pass++)
                {
                    float window = pass == 0 ? 0.18f : 0.45f;
                    maxF = maxB = maxS = 0f;
                    hits = 0;
                    for (int i = 0; i < samples.Count; i++)
                    {
                        SamplePt sp = samples[i];
                        if (Mathf.Abs(sp.t - t) > window) continue;
                        if (sp.f > maxF) maxF = sp.f;
                        if (-sp.f > maxB) maxB = -sp.f;
                        if (sp.s > maxS) maxS = sp.s;
                        hits++;
                    }
                }
                if (hits >= 4)
                {
                    if (maxF > 0.01f) rFront = Mathf.Clamp(maxF, fallbackFront * 0.55f, fallbackFront * 1.15f);
                    if (maxB > 0.01f) rBack = Mathf.Clamp(maxB, fallbackBack * 0.55f, fallbackBack * 1.15f);
                    if (maxS > 0.01f) rSide = Mathf.Clamp(maxS, fallbackSide * 0.55f, fallbackSide * 1.15f);
                }
            }

            private int AddLocal(Vector3 worldPoint)
            {
                vertices.Add(bone.InverseTransformPoint(worldPoint));
                return vertices.Count - 1;
            }

            private void Tri(int a, int b, int c, int sub, Vector3 expectedWorldNormal)
            {
                Vector3 localNormal = bone.InverseTransformDirection(expectedWorldNormal);
                if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), localNormal) < 0f)
                {
                    int tmp = b;
                    b = c;
                    c = tmp;
                }
                submeshes[sub].Add(a);
                submeshes[sub].Add(b);
                submeshes[sub].Add(c);
            }

            private void Quad(int a, int b, int c, int d, int sub, Vector3 expectedWorldNormal)
            {
                Tri(a, b, c, sub, expectedWorldNormal);
                Tri(a, c, d, sub, expectedWorldNormal);
            }

            private Vector3 EllipticalPoint(float t, float angle, float scale, float crestBoost)
            {
                GetSliceRadii(t, out float rf, out float rb, out float rs);
                float ca = Mathf.Cos(angle);
                float sa = Mathf.Sin(angle);
                float rz = (ca >= 0f ? rf : rb) * (scale + crestBoost);
                float rx = rs * (scale + crestBoost * 0.5f);
                return origin + axis * t + front * (ca * rz) + right * (sa * rx);
            }

            private Vector3 RadialDir(float angle) => (front * Mathf.Cos(angle) + right * Mathf.Sin(angle)).normalized;

            public void CurvedPlate(
                float tStart, float tEnd,
                float angleStart0, float angleEnd0,
                float angleStart1, float angleEnd1,
                float scale0, float scale1,
                float thicknessScale, float crestBoost,
                int mainSub, int rimSub, int columns)
            {
                const int rows = 5;
                int ringCols = columns + 1;
                var outer = new int[rows, ringCols];
                var inner = new int[rows, ringCols];

                for (int r = 0; r < rows; r++)
                {
                    float v = r / (float)(rows - 1);
                    float a0 = Mathf.Lerp(angleStart0, angleStart1, v);
                    float a1 = Mathf.Lerp(angleEnd0, angleEnd1, v);
                    float sc = Mathf.Lerp(scale0, scale1, v);
                    for (int c = 0; c <= columns; c++)
                    {
                        float u = c / (float)columns;
                        float ang = Mathf.Lerp(a0, a1, u);
                        float centerFactor = 1f - Mathf.Abs(u * 2f - 1f);
                        float chevronT = Mathf.Lerp(tStart, tEnd, v) + (v > 0.5f ? (v - 0.5f) * 0.04f * centerFactor : 0f);
                        float crest = crestBoost * centerFactor * centerFactor;
                        outer[r, c] = AddLocal(EllipticalPoint(chevronT, ang, sc, crest));
                        inner[r, c] = AddLocal(EllipticalPoint(chevronT, ang, Mathf.Max(0.85f, sc - thicknessScale), crest * 0.6f));
                    }
                }

                for (int r = 0; r < rows - 1; r++)
                {
                    bool borderRow = rimSub >= 0 && (r == 0 || r == rows - 2);
                    for (int c = 0; c < columns; c++)
                    {
                        bool borderCol = rimSub >= 0 && (c == 0 || c == columns - 1);
                        int sub = (borderRow || borderCol) ? rimSub : mainSub;
                        float midAngle = Mathf.Lerp(angleStart0, angleEnd0, (c + 0.5f) / columns);
                        Vector3 nOut = RadialDir(midAngle);
                        Quad(outer[r, c], outer[r, c + 1], outer[r + 1, c + 1], outer[r + 1, c], sub, nOut);
                        Quad(inner[r, c], inner[r + 1, c], inner[r + 1, c + 1], inner[r, c + 1], mainSub, -nOut);
                    }
                }

                int edgeSub = rimSub >= 0 ? rimSub : mainSub;
                for (int c = 0; c < columns; c++)
                {
                    Quad(outer[0, c], inner[0, c], inner[0, c + 1], outer[0, c + 1], edgeSub, -down);
                    Quad(outer[rows - 1, c], outer[rows - 1, c + 1], inner[rows - 1, c + 1], inner[rows - 1, c], edgeSub, down);
                }
                for (int r = 0; r < rows - 1; r++)
                {
                    Vector3 nLeft = -Vector3.Cross(down, RadialDir(angleStart0)).normalized;
                    Vector3 nRight = Vector3.Cross(down, RadialDir(angleEnd0)).normalized;
                    Quad(outer[r, 0], outer[r + 1, 0], inner[r + 1, 0], inner[r, 0], edgeSub, nLeft);
                    Quad(outer[r, columns], inner[r, columns], inner[r + 1, columns], outer[r + 1, columns], edgeSub, nRight);
                }
            }

            public void FacetedGem(float tCenter, float angleCenter, float scaleRatio, float widthRatio, float heightRatio, float depthRatio, int bezelSub, int gemSub)
            {
                Vector3 normal = RadialDir(angleCenter);
                Vector3 tangent = Vector3.Cross(down, normal).normalized;
                Vector3 center = EllipticalPoint(tCenter, angleCenter, scaleRatio, 0f);
                float hw = length * widthRatio * 0.5f;
                float hh = length * heightRatio * 0.5f;
                float d = length * depthRatio;

                Vector2[] dirs =
                {
                    new Vector2(0f, 1f),
                    new Vector2(0.72f, 0.52f),
                    new Vector2(1f, 0f),
                    new Vector2(0.72f, -0.52f),
                    new Vector2(0f, -1f),
                    new Vector2(-0.72f, -0.52f),
                    new Vector2(-1f, 0f),
                    new Vector2(-0.72f, 0.52f)
                };

                var baseRing = new int[8];
                var bezelRing = new int[8];
                var crownRing = new int[8];
                for (int i = 0; i < 8; i++)
                {
                    Vector3 offset = tangent * (dirs[i].x * hw) + down * (dirs[i].y * hh);
                    baseRing[i] = AddLocal(center + offset * 1.15f - normal * (d * 0.25f));
                    bezelRing[i] = AddLocal(center + offset * 0.94f + normal * (d * 0.42f));
                    crownRing[i] = AddLocal(center + offset * 0.56f + normal * (d * 0.85f));
                }
                int apex = AddLocal(center + normal * (d * 1.12f));

                for (int i = 0; i < 8; i++)
                {
                    int next = (i + 1) % 8;
                    Vector3 nSide = (normal + tangent * dirs[i].x + down * dirs[i].y).normalized;
                    Quad(baseRing[i], baseRing[next], bezelRing[next], bezelRing[i], bezelSub, nSide);
                    Quad(bezelRing[i], bezelRing[next], crownRing[next], crownRing[i], gemSub, nSide);
                    Tri(crownRing[i], crownRing[next], apex, gemSub, normal);
                }
            }

            public Mesh ToMesh(string meshName)
            {
                var mesh = new Mesh { name = meshName };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = submeshes.Length;
                for (int i = 0; i < submeshes.Length; i++)
                    mesh.SetTriangles(submeshes[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
