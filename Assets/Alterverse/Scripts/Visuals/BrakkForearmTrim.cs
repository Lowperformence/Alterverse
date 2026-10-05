using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Removes Brakk's raised details and original gold forearm plates.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkForearmTrim : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();

        public static void Apply(Transform model, HeroId hero)
        {
            // Nerok shares the Rook FBX. Never edit the imported/shared mesh.
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkForearmTrim>() != null) return;
            BrakkForearmTrim owner = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool original = skin.name == "DF_Anatomy";
                if (!original && skin.name != "DF_ArticulatedArmorDetails") continue;
                Mesh source = skin.sharedMesh;
                if (source == null || !source.isReadable) continue;
                int left = Array.FindIndex(skin.bones, b => b != null && b.name == "ForearmL");
                int right = Array.FindIndex(skin.bones, b => b != null && b.name == "ForearmR");
                if (left < 0 || right < 0) continue;
                BoneWeight[] weights = source.boneWeights;
                if (weights.Length != source.vertexCount) continue;
                var kept = new List<int>[source.subMeshCount];
                int leftTriangles = 0, rightTriangles = 0;
                for (int sub = 0; sub < kept.Length; sub++)
                {
                    kept[sub] = new List<int>();
                    int[] triangles = source.GetTriangles(sub);
                    bool gold = !original || sub < skin.sharedMaterials.Length &&
                        skin.sharedMaterials[sub] != null &&
                        skin.sharedMaterials[sub].name.IndexOf("Champagne metal", StringComparison.OrdinalIgnoreCase) >= 0;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        if (gold && Attached(a, left) && Attached(b, left) && Attached(c, left)) { leftTriangles++; continue; }
                        if (gold && Attached(a, right) && Attached(b, right) && Attached(c, right)) { rightTriangles++; continue; }
                        kept[sub].Add(a); kept[sub].Add(b); kept[sub].Add(c);
                    }
                }
                // The six original gold pieces live in DF_Anatomy, not in the
                // postprocessed detail mesh. Keep every other gold surface intact.
                if (leftTriangles == 0 && rightTriangles == 0) continue;
                if (owner == null) owner = model.gameObject.AddComponent<BrakkForearmTrim>();
                Mesh trimmed = Instantiate(source);
                trimmed.name = source.name + "_BrakkNoForearmPlates";
                for (int sub = 0; sub < kept.Length; sub++) trimmed.SetTriangles(kept[sub], sub, false);
                skin.sharedMesh = trimmed;
                owner.ownedMeshes.Add(trimmed);
                Debug.Log("DF: Brakk removed " + leftTriangles + " left and " + rightTriangles +
                    " right forearm triangles from " + skin.name, model);

                bool Attached(int vertex, int bone)
                    => weights[vertex].boneIndex0 == bone && weights[vertex].weight0 > .999f;
            }
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
