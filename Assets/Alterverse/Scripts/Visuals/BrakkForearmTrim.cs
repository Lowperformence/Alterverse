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
        Material black;

        public static void Apply(Transform model, HeroId hero)
        {
            // Nerok uses the same imported Rook asset: only alter Brakk's instance.
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkForearmTrim>() != null) return;
            BrakkForearmTrim owner = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool anatomy = skin.name == "DF_Anatomy";
                bool details = skin.name == "DF_ArticulatedArmorDetails";
                if (!anatomy && !details) continue;
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
                        if (anatomy && gold && onForearm || details && (onForearm || onHand))
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

        void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            if (black != null) Destroy(black);
        }
    }
}
