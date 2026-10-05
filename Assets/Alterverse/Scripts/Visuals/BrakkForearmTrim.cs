using System;
using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Removes only Brakk's three raised detail plates on each forearm.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkForearmTrim : MonoBehaviour
    {
        Mesh ownedMesh;

        public static void Apply(Transform model, HeroId hero)
        {
            // Nerok shares the Rook FBX. Never edit the imported/shared mesh.
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkForearmTrim>() != null) return;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.name != "DF_ArticulatedArmorDetails") continue;
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
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        if (Attached(a, left) && Attached(b, left) && Attached(c, left)) { leftTriangles++; continue; }
                        if (Attached(a, right) && Attached(b, right) && Attached(c, right)) { rightTriangles++; continue; }
                        kept[sub].Add(a); kept[sub].Add(b); kept[sub].Add(c);
                    }
                }
                // Older imports can contain fewer plates when silhouette sampling
                // skipped one. All forearm-only faces in this dedicated detail mesh
                // are the requested raised plates; the main arm skin is a separate mesh.
                if (leftTriangles == 0 && rightTriangles == 0) continue;
                var owner = model.gameObject.AddComponent<BrakkForearmTrim>();
                owner.ownedMesh = Instantiate(source);
                owner.ownedMesh.name = source.name + "_BrakkNoForearmPlates";
                for (int sub = 0; sub < kept.Length; sub++) owner.ownedMesh.SetTriangles(kept[sub], sub, false);
                skin.sharedMesh = owner.ownedMesh;
                return;

                bool Attached(int vertex, int bone)
                    => weights[vertex].boneIndex0 == bone && weights[vertex].weight0 > .999f;
            }
        }

        void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }
    }
}
