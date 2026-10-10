using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Applies Brakk's gold suit, grey shoulder plates and simplified forearms.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkForearmTrim : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedSkinMaterials = new List<Material>();
        Material goldForearms;
        static readonly Color SuitGold = new Color(.82f, .56f, .18f, 1f);

        public static void Apply(Transform model, HeroId hero)
        {
            // Nerok uses the same imported Rook asset: only alter Brakk's instance.
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkForearmTrim>() != null) return;
            BrakkForearmTrim owner = model.gameObject.AddComponent<BrakkForearmTrim>();
            SlimCalves(model, owner);
            RecolorSuit(model, owner);
            RecolorShoulderPlates(model, owner);
            // Clone skin slots on this instance; Nerok shares the imported asset.
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "DF_Anatomy") continue;
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material original = slots[i];
                    if (original == null || !original.name.StartsWith("Skin", StringComparison.OrdinalIgnoreCase)) continue;
                    var copy = new Material(original) { name = original.name + "_BrakkLighter" };
                    Color color = new Color(.68f, .43f, .31f, 1f);
                    if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", color);
                    if (copy.HasProperty("_Color")) copy.SetColor("_Color", color);
                    slots[i] = copy;
                    owner.ownedSkinMaterials.Add(copy);
                }
                renderer.sharedMaterials = slots;
            }
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool anatomy = skin.name == "DF_Anatomy";
                bool details = skin.name == "DF_ArticulatedArmorDetails";
                bool techDetails = skin.name == "DF_LimbTechDetails";
                if (!anatomy && !details && !techDetails) continue;
                Mesh source = skin.sharedMesh;
                if (source == null || !source.isReadable) continue;
                int left = Array.FindIndex(skin.bones, b => b != null && b.name == "ForearmL");
                int right = Array.FindIndex(skin.bones, b => b != null && b.name == "ForearmR");
                int upperL = Array.FindIndex(skin.bones, b => b != null && b.name == "UpperArmL");
                int upperR = Array.FindIndex(skin.bones, b => b != null && b.name == "UpperArmR");
                int handL = Array.FindIndex(skin.bones, b => b != null && b.name == "HandL");
                int handR = Array.FindIndex(skin.bones, b => b != null && b.name == "HandR");
                if (left < 0 && right < 0 && (!details || handL < 0 && handR < 0 && upperL < 0 && upperR < 0)) continue;
                BoneWeight[] weights = source.boneWeights;
                if (weights.Length != source.vertexCount) continue;

                var kept = new List<int>[source.subMeshCount];
                var goldForearm = new List<int>();
                int removed = 0;
                Material[] materials = skin.sharedMaterials;
                for (int sub = 0; sub < kept.Length; sub++)
                {
                    kept[sub] = new List<int>();
                    int[] triangles = source.GetTriangles(sub);
                    bool gold = sub < materials.Length && materials[sub] != null &&
                        materials[sub].name.IndexOf("Champagne metal", StringComparison.OrdinalIgnoreCase) >= 0;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        bool onForearm = (Attached(a, left) && Attached(b, left) && Attached(c, left)) ||
                            (Attached(a, right) && Attached(b, right) && Attached(c, right));
                        bool onUpperArm = (Attached(a, upperL) && Attached(b, upperL) && Attached(c, upperL)) ||
                            (Attached(a, upperR) && Attached(b, upperR) && Attached(c, upperR));
                        bool onHand = (Attached(a, handL) && Attached(b, handL) && Attached(c, handL)) ||
                            (Attached(a, handR) && Attached(b, handR) && Attached(c, handR));
                        // The six imported gold pieces are in Anatomy. Additional
                        // wrist/knuckle plates are generated on the hand bones.
                        // The four bronze overlays are the detail pass's two upper-arm
                        // plates per side. Remove their imported copies as well.
                        if ((anatomy && gold && onForearm) || (details && (onUpperArm || onForearm || onHand)) ||
                            (techDetails && onForearm))
                        {
                            removed++;
                            continue;
                        }
                        List<int> target = anatomy && onForearm ? goldForearm : kept[sub];
                        target.Add(a); target.Add(b); target.Add(c);
                    }
                }
                if (removed == 0 && goldForearm.Count == 0) continue;
                if (owner == null)
                {
                    owner = model.gameObject.AddComponent<BrakkForearmTrim>();
                }
                if (goldForearm.Count > 0 && owner.goldForearms == null)
                {
                    owner.goldForearms = RuntimeShaderLibrary.Create(SuitGold, metallic: .65f, smoothness: .32f);
                    if (owner.goldForearms == null) { Debug.LogError("DF: Brakk forearm gold shader is missing.", model); return; }
                    owner.goldForearms.name = "DF_Brakk_GoldForearms";
                }
                Mesh recolored = Instantiate(source);
                recolored.name = source.name + "_BrakkWithoutGoldPlates";
                recolored.subMeshCount = kept.Length + (goldForearm.Count > 0 ? 1 : 0);
                for (int sub = 0; sub < kept.Length; sub++) recolored.SetTriangles(kept[sub], sub, false);
                if (goldForearm.Count > 0) recolored.SetTriangles(goldForearm, kept.Length, false);
                recolored.RecalculateBounds();
                if (goldForearm.Count > 0)
                {
                    Array.Resize(ref materials, kept.Length + 1);
                    materials[kept.Length] = owner.goldForearms;
                    skin.sharedMaterials = materials;
                }
                skin.sharedMesh = recolored;
                owner.ownedMeshes.Add(recolored);
                Debug.Log("DF: Brakk removed " + removed + " plate triangles and colored " +
                    (goldForearm.Count / 3) + " remaining forearm triangles gold on " + skin.name, model);

                bool Attached(int vertex, int bone)
                {
                    if (bone < 0) return false;
                    BoneWeight w = weights[vertex];
                    float influence = (w.boneIndex0 == bone ? w.weight0 : 0f) +
                        (w.boneIndex1 == bone ? w.weight1 : 0f) +
                        (w.boneIndex2 == bone ? w.weight2 : 0f) +
                        (w.boneIndex3 == bone ? w.weight3 : 0f);
                    return influence > .95f;
                }
            }
        }


        static void RecolorShoulderPlates(Transform model, BrakkForearmTrim owner)
        {
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "DF_Anatomy") continue;
                Mesh source = renderer.sharedMesh;
                if (source == null || !source.isReadable) continue;
                Vector3[] vertices = source.vertices;
                BoneWeight[] weights = source.boneWeights;
                if (weights.Length != vertices.Length) continue;
                int upperL = Array.FindIndex(renderer.bones, b => b != null && b.name == "UpperArmL");
                int upperR = Array.FindIndex(renderer.bones, b => b != null && b.name == "UpperArmR");
                if (upperL < 0 && upperR < 0) continue;

                // Imported shoulder cages share the suit's fabric material. Identify
                // their separate, rigid upper-arm components before splitting a slot;
                // the connected body has several bones and keeps its suit material.
                int[] parent = new int[vertices.Length];
                var referenced = new bool[vertices.Length];
                var triangles = new int[source.subMeshCount][];
                for (int i = 0; i < parent.Length; i++) parent[i] = i;
                int Find(int index)
                {
                    while (parent[index] != index)
                    { parent[index] = parent[parent[index]]; index = parent[index]; }
                    return index;
                }
                void Join(int a, int b)
                {
                    int first = Find(a), second = Find(b);
                    if (first != second) parent[second] = first;
                }
                for (int sub = 0; sub < triangles.Length; sub++)
                {
                    triangles[sub] = source.GetTriangles(sub);
                    int[] indices = triangles[sub];
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                        referenced[a] = referenced[b] = referenced[c] = true;
                        Join(a, b); Join(a, c);
                    }
                }
                // FBX normals and UV seams can split indices along an otherwise
                // connected part. Weld only the lookup, leaving all mesh data intact.
                var coincident = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!referenced[i]) continue;
                    Vector3 p = vertices[i];
                    var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000f),
                        Mathf.RoundToInt(p.y * 100000f), Mathf.RoundToInt(p.z * 100000f));
                    if (coincident.TryGetValue(key, out int other)) Join(i, other);
                    else coincident.Add(key, i);
                }
                float Influence(BoneWeight w, int bone) => bone < 0 ? 0f :
                    (w.boneIndex0 == bone ? w.weight0 : 0f) + (w.boneIndex1 == bone ? w.weight1 : 0f) +
                    (w.boneIndex2 == bone ? w.weight2 : 0f) + (w.boneIndex3 == bone ? w.weight3 : 0f);
                int[] componentArm = new int[vertices.Length];
                for (int i = 0; i < componentArm.Length; i++) componentArm[i] = -2;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!referenced[i]) continue;
                    int root = Find(i);
                    int arm = Influence(weights[i], upperL) > .95f ? upperL :
                        Influence(weights[i], upperR) > .95f ? upperR : -1;
                    if (componentArm[root] == -2) componentArm[root] = arm;
                    else if (componentArm[root] != arm) componentArm[root] = -1;
                }
                Material[] materials = renderer.sharedMaterials;
                var kept = new List<int>[triangles.Length];
                var grey = new List<int>();
                for (int sub = 0; sub < triangles.Length; sub++)
                {
                    kept[sub] = new List<int>();
                    bool suit = sub < materials.Length && materials[sub] != null &&
                        materials[sub].name.StartsWith("Obsidian fabric", StringComparison.OrdinalIgnoreCase);
                    int[] indices = triangles[sub];
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        List<int> target = suit && componentArm[Find(indices[i])] >= 0 ? grey : kept[sub];
                        target.Add(indices[i]); target.Add(indices[i + 1]); target.Add(indices[i + 2]);
                    }
                }
                if (grey.Count == 0) continue;
                Material shoulder = RuntimeShaderLibrary.Create(new Color(.43f, .47f, .50f, 1f),
                    metallic: .55f, smoothness: .30f);
                if (shoulder == null) { Debug.LogError("DF: Brakk shoulder grey shader is missing.", model); continue; }
                shoulder.name = "DF_Brakk_GreyShoulderPlates";
                owner.ownedSkinMaterials.Add(shoulder);
                Mesh mesh = Instantiate(source);
                mesh.name = source.name + "_BrakkGreyShoulders";
                mesh.subMeshCount = kept.Length + 1;
                for (int sub = 0; sub < kept.Length; sub++) mesh.SetTriangles(kept[sub], sub, false);
                mesh.SetTriangles(grey, kept.Length, false);
                Array.Resize(ref materials, kept.Length + 1);
                materials[kept.Length] = shoulder;
                renderer.sharedMesh = mesh; renderer.sharedMaterials = materials;
                owner.ownedMeshes.Add(mesh);
            }
        }

        static void RecolorSuit(Transform model, BrakkForearmTrim owner)
        {
            // Work on this Brakk instance only; Nerok shares the Rook FBX.
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "DF_Anatomy") continue;
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material original = slots[i];
                    if (original == null) continue;
                    string name = original.name.Replace(" (Instance)", "");
                    // Preserve red enamel, skin, silver armor and mechanical details.
                    if (name != "Obsidian fabric" && name != "Obsidian fabric_MatteWeave") continue;
                    var copy = new Material(original) { name = original.name + "_BrakkGold" };
                    if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", SuitGold);
                    if (copy.HasProperty("_Color")) copy.SetColor("_Color", SuitGold);
                    slots[i] = copy;
                    owner.ownedSkinMaterials.Add(copy);
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = slots;
            }
        }

        static void SlimCalves(Transform model, BrakkForearmTrim owner)
        {
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source = renderer.sharedMesh;
                if (source == null || !source.isReadable) continue;
                Vector3[] vertices = source.vertices;
                BoneWeight[] weights = source.boneWeights;
                Matrix4x4[] bind = source.bindposes;
                if (weights.Length != vertices.Length) continue;
                bool changed = false;
                foreach (string side in new[] { "L", "R" })
                {
                    int shin = Array.FindIndex(renderer.bones, b => b != null && b.name == "Shin" + side);
                    int foot = Array.FindIndex(renderer.bones, b => b != null && b.name == "Foot" + side);
                    if (shin < 0 || foot < 0 || shin >= bind.Length || foot >= bind.Length) continue;
                    Vector3 knee = bind[shin].inverse.MultiplyPoint3x4(Vector3.zero);
                    Vector3 axis = bind[foot].inverse.MultiplyPoint3x4(Vector3.zero) - knee;
                    if (axis.sqrMagnitude < 1e-10f) continue;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        BoneWeight w = weights[i];
                        float influence = (w.boneIndex0 == shin ? w.weight0 : 0f) +
                            (w.boneIndex1 == shin ? w.weight1 : 0f) +
                            (w.boneIndex2 == shin ? w.weight2 : 0f) +
                            (w.boneIndex3 == shin ? w.weight3 : 0f);
                        if (influence <= 0f) continue;
                        float t = Vector3.Dot(vertices[i] - knee, axis) / axis.sqrMagnitude;
                        if (t <= 0f || t >= 1f) continue;
                        // Preserve joint seams and length; narrow the calf and its fitted armor together.
                        float taper = Mathf.Clamp01(Mathf.Min(t, 1f - t) / .10f);
                        Vector3 radial = vertices[i] - (knee + axis * t);
                        vertices[i] -= radial * (.20f * influence * taper);
                        changed = true;
                    }
                }
                if (!changed) continue;
                Mesh mesh = Instantiate(source);
                mesh.name = source.name + "_BrakkSlimCalves";
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                renderer.sharedMesh = mesh;
                Bounds bounds = renderer.localBounds;
                bounds.Encapsulate(mesh.bounds);
                renderer.localBounds = bounds;
                owner.ownedMeshes.Add(mesh);
            }
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in ownedSkinMaterials) if (material != null) Destroy(material);
            if (goldForearms != null) Destroy(goldForearms);
        }
    }
}
