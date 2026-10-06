using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Removes Brakk's gold forearm plates and extra hand decorations.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkForearmTrim : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedSkinMaterials = new List<Material>();
        Material black;

        public static void Apply(Transform model, HeroId hero)
        {
            // Nerok uses the same imported Rook asset: only alter Brakk's instance.
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkForearmTrim>() != null) return;
            BrakkForearmTrim owner = model.gameObject.AddComponent<BrakkForearmTrim>();
            SlimCalves(model, owner);
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
                int handL = Array.FindIndex(skin.bones, b => b != null && b.name == "HandL");
                int handR = Array.FindIndex(skin.bones, b => b != null && b.name == "HandR");
                if (left < 0 && right < 0 && (!details || handL < 0 && handR < 0)) continue;
                BoneWeight[] weights = source.boneWeights;
                if (weights.Length != source.vertexCount) continue;

                var kept = new List<int>[source.subMeshCount];
                var blackForearm = new List<int>();
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
                        bool onHand = (Attached(a, handL) && Attached(b, handL) && Attached(c, handL)) ||
                            (Attached(a, handR) && Attached(b, handR) && Attached(c, handR));
                        // The six imported gold pieces are in Anatomy. Additional
                        // wrist/knuckle plates are generated on the hand bones.
                        if ((anatomy && gold && onForearm) || (details && (onForearm || onHand)) ||
                            (techDetails && onForearm))
                        {
                            removed++;
                            continue;
                        }
                        List<int> target = anatomy && onForearm ? blackForearm : kept[sub];
                        target.Add(a); target.Add(b); target.Add(c);
                    }
                }
                if (removed == 0 && blackForearm.Count == 0) continue;
                if (owner == null)
                {
                    owner = model.gameObject.AddComponent<BrakkForearmTrim>();
                }
                if (blackForearm.Count > 0 && owner.black == null)
                {
                    owner.black = RuntimeShaderLibrary.Create(new Color(.025f, .025f, .025f), metallic: .15f, smoothness: .32f);
                    if (owner.black == null) { Debug.LogError("DF: Brakk forearm black shader is missing.", model); return; }
                    owner.black.name = "DF_Brakk_BlackForearms";
                }
                Mesh recolored = Instantiate(source);
                recolored.name = source.name + "_BrakkWithoutGoldPlates";
                recolored.subMeshCount = kept.Length + (blackForearm.Count > 0 ? 1 : 0);
                for (int sub = 0; sub < kept.Length; sub++) recolored.SetTriangles(kept[sub], sub, false);
                if (blackForearm.Count > 0) recolored.SetTriangles(blackForearm, kept.Length, false);
                recolored.RecalculateBounds();
                if (blackForearm.Count > 0)
                {
                    Array.Resize(ref materials, kept.Length + 1);
                    materials[kept.Length] = owner.black;
                    skin.sharedMaterials = materials;
                }
                skin.sharedMesh = recolored;
                owner.ownedMeshes.Add(recolored);
                Debug.Log("DF: Brakk removed " + removed + " plate triangles and colored " +
                    (blackForearm.Count / 3) + " remaining forearm triangles black on " + skin.name, model);

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
            if (black != null) Destroy(black);
        }
    }
}
