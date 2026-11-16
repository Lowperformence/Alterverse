using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Replaces Tahla Venn's rigid hanging vestments with a full, multi-layered Royal Sanctuary
    /// Valkyrie armor suite (sculpted elliptical cuirass, high gorget, dorsal aether wings,
    /// abdominal chevron lames, waist cincture, 3-tier hip tassets, lotus pauldrons, ornate vambraces,
    /// chevron cuisses, winged knee poleyns, and fluted greaves) fitted to her exact anatomy contours.
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

            Material ivory = RuntimeShaderLibrary.Create(new Color(0.89f, 0.92f, 0.88f, 1f), false, 0.30f, 0.74f, Color.black);
            Material gold = RuntimeShaderLibrary.Create(new Color(0.80f, 0.59f, 0.22f, 1f), false, 0.85f, 0.76f, Color.black);
            Material jade = RuntimeShaderLibrary.Create(new Color(0.075f, 0.18f, 0.15f, 1f), false, 0.46f, 0.60f, Color.black);
            Color emeraldColor = new Color(0.12f, 0.98f, 0.38f, 1f);
            Material emerald = RuntimeShaderLibrary.Create(emeraldColor, false, 0.16f, 0.80f, emeraldColor * 2.6f);
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
            Transform head = Array.Find(bones, b => b.name == "Head");

            if (chest != null && head != null)
                owner.BuildChestCuirass(anatomy, chest, head.position, model.forward, model.right);
            if (spine != null && chest != null)
                owner.BuildSpineCorset(anatomy, spine, chest.position, model.forward);
            if (hips != null && spine != null)
                owner.BuildWaistAndTassets(anatomy, hips, spine.position, model.forward);

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
                owner.BuildThighCuisse(anatomy, "DF_HealerThighPlate" + side, thigh, shin.position, model.forward, sign);
                owner.BuildKneePoleyn(anatomy, "DF_HealerKneeGuard" + side, shin, foot.position, model.forward, sign);
                owner.BuildShinGreave(anatomy, "DF_HealerShinPlate" + side, shin, foot.position, model.forward, sign);
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

        private void BuildChestCuirass(SkinnedMeshRenderer anatomy, Transform chest, Vector3 headPos, Vector3 modelForward, Vector3 modelRight)
        {
            var b = new ArmorBuilder(anatomy, chest, headPos - chest.position, modelForward, 0.44f, 0.38f, 0.52f);
            // 1. Sanctuary-jade structural under-chassis wrapping torso
            b.CurvedPlate(0.05f, 0.75f, -2.05f, 2.05f, -1.92f, 1.92f, 1.03f, 1.07f, 0.045f, 0.035f, SubJade, -1, 14);
            // 2. Sculpted V-tapered pearl-ivory Valkyrie breastplate with gold border trim
            b.CurvedPlate(0.10f, 0.71f, -1.24f, 1.24f, -1.56f, 1.56f, 1.07f, 1.13f, 0.055f, 0.075f, SubIvory, SubGold, 12);
            // 3. Diagonal golden pectoral filigree arches
            b.CurvedPlate(0.45f, 0.61f, -1.46f, -0.18f, -1.52f, -0.32f, 1.14f, 1.17f, 0.040f, 0.020f, SubGold, -1, 8);
            b.CurvedPlate(0.45f, 0.61f, 0.18f, 1.46f, 0.32f, 1.52f, 1.14f, 1.17f, 0.040f, 0.020f, SubGold, -1, 8);
            // 4. High Royal Gorget / Collar in ivory & burnished gold
            b.CurvedPlate(0.72f, 0.92f, -2.35f, 2.35f, -2.20f, 2.20f, 0.76f, 0.84f, 0.048f, 0.030f, SubIvory, SubGold, 14);
            // 5. Central Sanctuary Heart Emerald Crystal in a golden diamond bezel
            b.FacetedGem(0.42f, 0f, 1.18f, 0.12f, 0.17f, 0.048f, SubGold, SubEmerald);
            // 6. Sculpted Dorsal Backplate with 3 golden spinal chevrons
            b.CurvedPlate(0.12f, 0.73f, 2.15f, 4.13f, 2.02f, 4.26f, 1.06f, 1.11f, 0.050f, 0.055f, SubIvory, SubGold, 10);
            for (int i = 0; i < 3; i++)
            {
                float t = 0.24f + i * 0.17f;
                b.FacetedGem(t, Mathf.PI, 1.14f, 0.09f, 0.075f, 0.032f, SubGold, SubEmerald);
            }
            // 7. Twin Dorsal Valkyrie Aether Wings
            for (int s = -1; s <= 1; s += 2)
                b.DorsalWing(s, modelRight);

            Attach("DF_TahlaRoyalCuirass", chest, b);
        }

        private void BuildSpineCorset(SkinnedMeshRenderer anatomy, Transform spine, Vector3 chestPos, Vector3 modelForward)
        {
            var b = new ArmorBuilder(anatomy, spine, chestPos - spine.position, modelForward, 0.56f, 0.48f, 0.64f);
            for (int i = 0; i < 3; i++)
            {
                float t0 = 0.08f + i * 0.27f;
                float t1 = t0 + 0.28f;
                float s0 = 1.04f + i * 0.03f;
                float s1 = s0 + 0.03f;
                int mat = i % 2 == 0 ? SubJade : SubIvory;
                b.CurvedPlate(t0, t1, -1.55f, 1.55f, -1.68f, 1.68f, s0, s1, 0.050f, 0.055f, mat, SubGold, 10);
            }
            b.FacetedGem(0.50f, 0f, 1.15f, 0.09f, 0.15f, 0.038f, SubGold, SubEmerald);
            Attach("DF_TahlaCorsetArmor", spine, b);
        }

        private void BuildWaistAndTassets(SkinnedMeshRenderer anatomy, Transform hips, Vector3 spinePos, Vector3 modelForward)
        {
            Vector3 downAxis = hips.position - spinePos;
            var b = new ArmorBuilder(anatomy, hips, downAxis, modelForward, 0.68f, 0.62f, 0.82f);

            // 1. 360-degree Imperial Gold Waist Cincture Belt around the pelvis
            b.CurvedPlate(-0.22f, 0.16f, -3.14159f, 3.14159f, -3.14159f, 3.14159f, 1.04f, 1.08f, 0.055f, 0.035f, SubGold, SubJade, 20);
            b.FacetedGem(-0.03f, 0f, 1.12f, 0.16f, 0.19f, 0.048f, SubGold, SubEmerald);
            b.FacetedGem(-0.03f, Mathf.PI, 1.12f, 0.15f, 0.16f, 0.042f, SubGold, SubEmerald);

            // 2. Left & Right 3-Tier Articulated Valkyrie Hip Tassets / Faulds
            for (int side = -1; side <= 1; side += 2)
            {
                float centerAngle = side * 1.32f;
                for (int tier = 0; tier < 3; tier++)
                {
                    float t0 = 0.14f + tier * 0.28f;
                    float t1 = t0 + 0.30f;
                    float halfSpan0 = 0.76f - tier * 0.11f;
                    float halfSpan1 = halfSpan0 - 0.08f;
                    float s0 = 1.08f + tier * 0.04f;
                    float s1 = s0 + 0.03f;
                    b.CurvedPlate(
                        t0, t1,
                        centerAngle - halfSpan0, centerAngle + halfSpan0,
                        centerAngle - halfSpan1, centerAngle + halfSpan1,
                        s0, s1, 0.050f, 0.045f,
                        tier == 1 ? SubJade : SubIvory, SubGold, 8);
                }
                b.FacetedGem(0.32f, centerAngle, 1.16f, 0.11f, 0.15f, 0.040f, SubGold, SubEmerald);
            }
            Attach("DF_TahlaWaistAndTassets", hips, b);
        }

        private void BuildPauldron(SkinnedMeshRenderer anatomy, string name, Transform upperArm, Vector3 forearmPos, Vector3 modelForward, Vector3 outwardDir)
        {
            Vector3 axis = forearmPos - upperArm.position;
            Vector3 centerDir = (Vector3.ProjectOnPlane(outwardDir, axis.normalized).normalized * 0.85f +
                                 Vector3.ProjectOnPlane(modelForward, axis.normalized).normalized * 0.52f).normalized;
            var b = new ArmorBuilder(anatomy, upperArm, axis, centerDir, 0.22f, 0.22f, 0.22f);

            // 1. Sanctuary-jade inner shoulder base pad
            b.CurvedPlate(-0.10f, 0.48f, -1.42f, 1.42f, -1.18f, 1.18f, 1.16f, 1.06f, 0.075f, 0.065f, SubJade, -1, 10);
            // 2. Sculpted Pearl-Ivory Lotus Pauldron Dome with Imperial Gold border
            b.CurvedPlate(-0.08f, 0.42f, -1.32f, 1.32f, -1.04f, 1.04f, 1.24f, 1.12f, 0.090f, 0.110f, SubIvory, SubGold, 10);
            // 3. Flared Golden Valkyrie Upper Shoulder Crest
            b.CurvedPlate(-0.16f, 0.18f, -0.96f, 0.96f, -0.78f, 0.78f, 1.38f, 1.28f, 0.095f, 0.135f, SubGold, SubIvory, 8);
            // 4. Lower Overlapping Bicep Guard Lame
            b.CurvedPlate(0.40f, 0.72f, -1.12f, 1.12f, -0.95f, 0.95f, 1.08f, 1.02f, 0.070f, 0.065f, SubIvory, SubGold, 8);
            // 5. Faceted Emerald Shoulder Cabochon Gem
            b.FacetedGem(0.14f, 0f, 1.38f, 0.11f, 0.15f, 0.038f, SubGold, SubEmerald);

            Attach(name, upperArm, b);
        }

        private void BuildVambrace(SkinnedMeshRenderer anatomy, string name, Transform forearm, Vector3 handPos, Vector3 modelForward, Vector3 outwardDir)
        {
            Vector3 axis = handPos - forearm.position;
            Vector3 centerDir = (Vector3.ProjectOnPlane(outwardDir, axis.normalized).normalized * 0.78f +
                                 Vector3.ProjectOnPlane(modelForward, axis.normalized).normalized * 0.62f).normalized;
            var b = new ArmorBuilder(anatomy, forearm, axis, centerDir, 0.17f, 0.17f, 0.17f);

            // 1. Sanctuary-jade under-bracer
            b.CurvedPlate(0.08f, 0.86f, -1.48f, 1.48f, -1.35f, 1.35f, 1.08f, 0.96f, 0.070f, 0.050f, SubJade, -1, 10);
            // 2. Sculpted Pearl-Ivory Vambrace with raised Burnished Gold border
            b.CurvedPlate(0.12f, 0.80f, -1.26f, 1.26f, -1.10f, 1.10f, 1.15f, 1.02f, 0.085f, 0.095f, SubIvory, SubGold, 10);
            // 3. Swept Golden Elbow Guard Peak
            b.CurvedPlate(-0.04f, 0.22f, -0.72f, 0.72f, -0.48f, 0.48f, 1.24f, 1.18f, 0.090f, 0.120f, SubGold, -1, 6);
            // 4. Royal Golden Wrist Cuff Ring
            b.CurvedPlate(0.78f, 0.92f, -2.50f, 2.50f, -2.50f, 2.50f, 1.04f, 1.00f, 0.075f, 0.040f, SubGold, SubJade, 12);
            // 5. Forearm Emerald Aether Channel
            b.FacetedGem(0.46f, 0f, 1.20f, 0.070f, 0.28f, 0.026f, SubGold, SubEmerald);

            Attach(name, forearm, b);
        }

        private void BuildThighCuisse(SkinnedMeshRenderer anatomy, string name, Transform thigh, Vector3 shinPos, Vector3 modelForward, float sideSign)
        {
            var b = new ArmorBuilder(anatomy, thigh, shinPos - thigh.position, modelForward, 0.185f, 0.185f, 0.185f);
            // 1. Deep Sanctuary-Jade structural under-cuisse
            b.CurvedPlate(0.06f, 0.84f, -1.42f, 1.42f, -1.24f, 1.24f, 1.05f, 0.94f, 0.075f, 0.065f, SubJade, -1, 10);
            // 2. Sculpted Pearl-Ivory Chevron Cuisse with Burnished Gold perimeter border
            b.CurvedPlate(0.10f, 0.80f, -1.24f, 1.24f, -1.02f, 1.02f, 1.11f, 0.99f, 0.095f, 0.115f, SubIvory, SubGold, 10);
            // 3. Upper Overlapping Articulation Lame
            b.CurvedPlate(0.08f, 0.26f, -1.32f, 1.32f, -1.26f, 1.26f, 1.16f, 1.12f, 0.085f, 0.090f, SubIvory, SubGold, 8);
            // 4. Outer Thigh Golden Filigree Flange
            float flangeCenter = sideSign * 0.88f;
            b.CurvedPlate(0.20f, 0.72f, flangeCenter - 0.35f, flangeCenter + 0.35f, flangeCenter - 0.28f, flangeCenter + 0.28f, 1.15f, 1.04f, 0.075f, 0.075f, SubGold, -1, 6);
            // 5. Central Golden Spine & Emerald Energy Inlay
            b.FacetedGem(0.46f, 0f, 1.18f, 0.062f, 0.32f, 0.025f, SubGold, SubEmerald);

            Attach(name, thigh, b);
        }

        private void BuildKneePoleyn(SkinnedMeshRenderer anatomy, string name, Transform shin, Vector3 footPos, Vector3 modelForward, float sideSign)
        {
            var b = new ArmorBuilder(anatomy, shin, footPos - shin.position, modelForward, 0.155f, 0.155f, 0.155f);
            // 1. Sanctuary-jade knee backing pad
            b.CurvedPlate(-0.10f, 0.20f, -1.30f, 1.30f, -1.16f, 1.16f, 1.08f, 1.02f, 0.085f, 0.080f, SubJade, -1, 10);
            // 2. Diamond-crowned Burnished Gold Poleyn Knee Shell with Ivory inner face
            b.CurvedPlate(-0.08f, 0.18f, -1.12f, 1.12f, -0.94f, 0.94f, 1.16f, 1.10f, 0.105f, 0.135f, SubGold, SubIvory, 10);
            // 3. Swept Outer Knee Wing-Fin
            float wingAngle = sideSign * 0.96f;
            b.CurvedPlate(-0.11f, 0.14f, wingAngle - 0.32f, wingAngle + 0.32f, wingAngle - 0.22f, wingAngle + 0.22f, 1.26f, 1.18f, 0.090f, 0.110f, SubGold, -1, 6);
            // 4. Central Faceted Emerald Knee Jewel
            b.FacetedGem(0.04f, 0f, 1.28f, 0.076f, 0.115f, 0.030f, SubGold, SubEmerald);

            Attach(name, shin, b);
        }

        private void BuildShinGreave(SkinnedMeshRenderer anatomy, string name, Transform shin, Vector3 footPos, Vector3 modelForward, float sideSign)
        {
            var b = new ArmorBuilder(anatomy, shin, footPos - shin.position, modelForward, 0.148f, 0.148f, 0.148f);
            // 1. Sanctuary-jade structural greave liner + calf guard
            b.CurvedPlate(0.18f, 0.92f, -1.48f, 1.48f, -1.32f, 1.32f, 1.05f, 0.88f, 0.075f, 0.065f, SubJade, -1, 10);
            b.CurvedPlate(0.24f, 0.82f, 2.25f, 4.03f, 2.35f, 3.93f, 1.02f, 0.86f, 0.075f, 0.060f, SubIvory, SubGold, 8);
            // 2. Fluted Pearl-Ivory Front Greave with raised Imperial Gold perimeter border
            b.CurvedPlate(0.20f, 0.90f, -1.26f, 1.26f, -1.08f, 1.08f, 1.12f, 0.95f, 0.095f, 0.125f, SubIvory, SubGold, 10);
            // 3. Two Golden Calf-Strap Rings
            b.CurvedPlate(0.34f, 0.40f, -3.14159f, 3.14159f, -3.14159f, 3.14159f, 1.07f, 1.04f, 0.070f, 0.030f, SubGold, -1, 14);
            b.CurvedPlate(0.66f, 0.72f, -3.14159f, 3.14159f, -3.14159f, 3.14159f, 0.98f, 0.95f, 0.070f, 0.030f, SubGold, -1, 14);
            // 4. Armored Golden Ankle Cuff Ring
            b.CurvedPlate(0.85f, 0.95f, -2.20f, 2.20f, -2.20f, 2.20f, 0.98f, 0.94f, 0.085f, 0.065f, SubGold, SubJade, 12);
            // 5. Central Shin Crest Emerald Seam
            b.FacetedGem(0.52f, 0f, 1.18f, 0.052f, 0.34f, 0.022f, SubGold, SubEmerald);

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
            private readonly Transform bone;
            private readonly Vector3 origin;
            private readonly Vector3 axis;
            private readonly float length;
            private readonly Vector3 down;
            private readonly Vector3 front;
            private readonly Vector3 right;
            private readonly float frontRadius;
            private readonly float backRadius;
            private readonly float sideRadius;
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

                frontRadius = length * defaultFrontRatio;
                backRadius = length * defaultBackRatio;
                sideRadius = length * defaultSideRatio;

                if (anatomy != null && anatomy.sharedMesh != null && anatomy.sharedMesh.isReadable)
                {
                    int idx = Array.IndexOf(anatomy.bones, bone);
                    Mesh src = anatomy.sharedMesh;
                    if (idx >= 0 && idx < src.bindposes.Length && src.boneWeights.Length == src.vertexCount)
                    {
                        Matrix4x4 bind = src.bindposes[idx];
                        Vector3[] pts = src.vertices;
                        BoneWeight[] bw = src.boneWeights;
                        float maxF = 0f, maxB = 0f, maxS = 0f;
                        int count = 0;
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
                            float f = Vector3.Dot(rel, front);
                            float s = Mathf.Abs(Vector3.Dot(rel, right));
                            if (f > maxF) maxF = f;
                            if (-f > maxB) maxB = -f;
                            if (s > maxS) maxS = s;
                            count++;
                        }
                        if (count >= 8)
                        {
                            frontRadius = Mathf.Clamp(maxF, length * defaultFrontRatio * 0.72f, length * defaultFrontRatio * 1.45f);
                            backRadius = Mathf.Clamp(maxB, length * defaultBackRatio * 0.72f, length * defaultBackRatio * 1.45f);
                            sideRadius = Mathf.Clamp(maxS, length * defaultSideRatio * 0.72f, length * defaultSideRatio * 1.45f);
                        }
                    }
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
                float ca = Mathf.Cos(angle);
                float sa = Mathf.Sin(angle);
                float rz = (ca >= 0f ? frontRadius : backRadius) * (scale + crestBoost);
                float rx = sideRadius * (scale + crestBoost * 0.5f);
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
                const int rows = 4;
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
                        float chevronT = Mathf.Lerp(tStart, tEnd, v) + (v > 0.5f ? (v - 0.5f) * 0.06f * centerFactor : 0f);
                        float crest = crestBoost * centerFactor * centerFactor;
                        outer[r, c] = AddLocal(EllipticalPoint(chevronT, ang, sc, crest));
                        inner[r, c] = AddLocal(EllipticalPoint(chevronT, ang, Mathf.Max(0.25f, sc - thicknessScale), crest * 0.7f));
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
                    baseRing[i] = AddLocal(center + offset * 1.18f - normal * (d * 0.25f));
                    bezelRing[i] = AddLocal(center + offset * 0.96f + normal * (d * 0.45f));
                    crownRing[i] = AddLocal(center + offset * 0.58f + normal * (d * 0.88f));
                }
                int apex = AddLocal(center + normal * (d * 1.18f));

                for (int i = 0; i < 8; i++)
                {
                    int next = (i + 1) % 8;
                    Vector3 nSide = (normal + tangent * dirs[i].x + down * dirs[i].y).normalized;
                    Quad(baseRing[i], baseRing[next], bezelRing[next], bezelRing[i], bezelSub, nSide);
                    Quad(bezelRing[i], bezelRing[next], crownRing[next], crownRing[i], gemSub, nSide);
                    Tri(crownRing[i], crownRing[next], apex, gemSub, normal);
                }
            }

            public void DorsalWing(int side, Vector3 modelRight)
            {
                Vector3 back = -front;
                Vector3 lat = (modelRight * side).normalized;
                Vector3 rootPos = origin + axis * 0.50f + back * (backRadius * 1.08f) + lat * (sideRadius * 0.48f);
                Vector3 tipPos = rootPos + axis * 0.42f + back * (length * 0.34f) + lat * (sideRadius * 0.85f);
                Vector3 lowPos = rootPos - axis * 0.26f + back * (length * 0.24f) + lat * (sideRadius * 0.46f);
                Vector3 midPos = (rootPos + tipPos + lowPos) / 3f + back * (length * 0.08f);
                Vector3 thick = Vector3.Cross(tipPos - rootPos, lowPos - rootPos).normalized * (length * 0.028f);

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 n = thick.normalized * s;
                    int r = AddLocal(rootPos + thick * (0.4f * s));
                    int t = AddLocal(tipPos + thick * (0.25f * s));
                    int l = AddLocal(lowPos + thick * (0.3f * s));
                    int m = AddLocal(midPos + thick * (1.15f * s));
                    Tri(r, t, m, SubGold, n);
                    Tri(t, l, m, SubIvory, n);
                    Tri(l, r, m, SubEmerald, n);
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
