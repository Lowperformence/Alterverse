using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Gives every piece attached to Brakk's forearm bones the same silver finish.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkForearmTrim : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        Material silver;

        public static void Apply(Transform model, HeroId hero)
        {
            // Nerok uses the same imported Rook asset: only alter Brakk's instance.
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkForearmTrim>() != null) return;
            BrakkForearmTrim owner = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source = skin.sharedMesh;
                if (source == null || !source.isReadable) continue;
                int left = Array.FindIndex(skin.bones, b => b != null && b.name == "ForearmL");
                int right = Array.FindIndex(skin.bones, b => b != null && b.name == "ForearmR");
                if (left < 0 && right < 0) continue;
                BoneWeight[] weights = source.boneWeights;
                if (weights.Length != source.vertexCount) continue;

                var kept = new List<int>[source.subMeshCount];
                var forearm = new List<int>();
                for (int sub = 0; sub < kept.Length; sub++)
                {
                    kept[sub] = new List<int>();
                    int[] triangles = source.GetTriangles(sub);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        List<int> target = (Attached(a, left) && Attached(b, left) && Attached(c, left)) ||
                            (Attached(a, right) && Attached(b, right) && Attached(c, right)) ? forearm : kept[sub];
                        target.Add(a); target.Add(b); target.Add(c);
                    }
                }
                if (forearm.Count == 0) continue;
                if (owner == null)
                {
                    owner = model.gameObject.AddComponent<BrakkForearmTrim>();
                    owner.silver = RuntimeShaderLibrary.Create(new Color(.59f, .64f, .68f), metallic: .76f, smoothness: .48f);
                    if (owner.silver == null)
                    {
                        Destroy(owner);
                        Debug.LogError("DF: Brakk forearm silver shader is missing.", model);
                        return;
                    }
                    owner.silver.name = "DF_Brakk_SilverForearms";
                }
                Mesh recolored = Instantiate(source);
                recolored.name = source.name + "_BrakkSilverForearms";
                recolored.subMeshCount = kept.Length + 1;
                for (int sub = 0; sub < kept.Length; sub++) recolored.SetTriangles(kept[sub], sub, false);
                recolored.SetTriangles(forearm, kept.Length, false);
                recolored.RecalculateBounds();
                Material[] materials = skin.sharedMaterials;
                Array.Resize(ref materials, kept.Length + 1);
                materials[kept.Length] = owner.silver;
                skin.sharedMesh = recolored;
                skin.sharedMaterials = materials;
                owner.ownedMeshes.Add(recolored);
                Debug.Log("DF: Brakk colored " + (forearm.Count / 3) + " forearm triangles silver on " + skin.name, model);

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
            if (silver != null) Destroy(silver);
        }
    }
}
