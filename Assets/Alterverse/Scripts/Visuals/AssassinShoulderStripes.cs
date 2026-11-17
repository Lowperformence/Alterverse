using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    [DisallowMultipleComponent]
    public sealed class AssassinShoulderStripes : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        Material material;

        public static void Apply(Transform model)
        {
            if (model.GetComponent<AssassinShoulderStripes>() != null) return;
            SkinnedMeshRenderer skin = null;
            foreach (var candidate in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (candidate.name == "DF_Anatomy") { skin = candidate; break; }
            if (skin == null || skin.sharedMesh == null || !skin.sharedMesh.isReadable) return;
            var owner = model.gameObject.AddComponent<AssassinShoulderStripes>();
            Color violet = new Color(.70f, .12f, 1f, 1f);
            owner.material = RuntimeShaderLibrary.Create(violet, false, .28f, .68f, violet * 2.4f);
            if (owner.material == null) return;
            owner.material.name = "DF_ZhaevShoulderGlow";
            owner.material.EnableKeyword("_EMISSION");

            // Measure the authored shoulder armor in bind space per angular sector so the energy inlays hug the pauldron contour.
            Mesh source = skin.sharedMesh;
            Vector3[] original = source.vertices;
            BoneWeight[] weights = source.boneWeights;
            Matrix4x4[] bind = source.bindposes;
            foreach (string side in new[] { "L", "R" })
            {
                int index = Array.FindIndex(skin.bones, b => b != null && b.name == "UpperArm" + side);
                int elbow = Array.FindIndex(skin.bones, b => b != null && b.name == "Forearm" + side);
                if (index < 0 || elbow < 0 || index >= bind.Length || elbow >= bind.Length) continue;
                Transform bone = skin.bones[index];
                Matrix4x4 toLocal = bind[index];
                Vector3 end = toLocal.MultiplyPoint3x4(bind[elbow].inverse.MultiplyPoint3x4(Vector3.zero));
                float length = end.magnitude;
                if (length < .00001f) continue;
                Vector3 axis = end / length;
                Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.z) < .9f ? Vector3.forward : Vector3.right).normalized;
                Vector3 around = Vector3.Cross(axis, across).normalized;
                var points = new List<Vector3>();
                for (int i = 0; i < original.Length && i < weights.Length; i++)
                {
                    BoneWeight w = weights[i];
                    float weight = (w.boneIndex0 == index ? w.weight0 : 0) + (w.boneIndex1 == index ? w.weight1 : 0)
                        + (w.boneIndex2 == index ? w.weight2 : 0) + (w.boneIndex3 == index ? w.weight3 : 0);
                    if (weight >= .45f) points.Add(toLocal.MultiplyPoint3x4(original[i]));
                }

                const int segments = 48;
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                float[] rawRadii = new float[segments];
                float[] smoothRadii = new float[segments];

                foreach (float position in new[] { .13f, .26f })
                {
                    float along = length * position;
                    float fallbackRadius = length * .17f;
                    for (int s = 0; s < segments; s++)
                    {
                        float angle = s * Mathf.PI * 2f / segments;
                        Vector3 dir = (across * Mathf.Cos(angle) + around * Mathf.Sin(angle)).normalized;
                        float r = fallbackRadius;
                        foreach (Vector3 p in points)
                        {
                            float axial = Vector3.Dot(p, axis);
                            if (Mathf.Abs(axial - along) > length * .11f) continue;
                            Vector3 radial = p - axis * axial;
                            float rm = radial.magnitude;
                            if (rm > .001f && Vector3.Dot(radial / rm, dir) > .82f)
                                r = Mathf.Max(r, rm);
                        }
                        rawRadii[s] = r;
                    }
                    // Smooth angular contour so the energy channel follows the pauldron silhouette without spikes
                    for (int s = 0; s < segments; s++)
                    {
                        float sum = 0f, wSum = 0f;
                        for (int k = -3; k <= 3; k++)
                        {
                            float w = 4 - Mathf.Abs(k);
                            sum += rawRadii[(s + k + segments) % segments] * w;
                            wSum += w;
                        }
                        smoothRadii[s] = sum / wSum + length * .0035f;
                    }

                    float halfWidth = length * .0135f;
                    float crestHeight = length * .0045f;
                    for (int i = 0; i < segments; i++)
                    {
                        int next = (i + 1) % segments;
                        float a = i * Mathf.PI * 2f / segments, b = next * Mathf.PI * 2f / segments;
                        Vector3 dirA = across * Mathf.Cos(a) + around * Mathf.Sin(a);
                        Vector3 dirB = across * Mathf.Cos(b) + around * Mathf.Sin(b);
                        float rA = smoothRadii[i], rB = smoothRadii[next];
                        // Subtle chevron dip on the outer shoulder crest
                        float dipA = Mathf.Sin(a * 2f) * length * .008f;
                        float dipB = Mathf.Sin(b * 2f) * length * .008f;

                        int start = vertices.Count;
                        // 3-ring beveled cross-section (top edge, raised glowing crest, bottom edge)
                        vertices.Add(axis * (along + dipA - halfWidth) + dirA * rA);
                        vertices.Add(axis * (along + dipB - halfWidth) + dirB * rB);
                        vertices.Add(axis * (along + dipB) + dirB * (rB + crestHeight));
                        vertices.Add(axis * (along + dipA) + dirA * (rA + crestHeight));
                        vertices.Add(axis * (along + dipA + halfWidth) + dirA * rA);
                        vertices.Add(axis * (along + dipB + halfWidth) + dirB * rB);

                        triangles.Add(start + 0); triangles.Add(start + 1); triangles.Add(start + 2);
                        triangles.Add(start + 0); triangles.Add(start + 2); triangles.Add(start + 3);
                        triangles.Add(start + 3); triangles.Add(start + 2); triangles.Add(start + 5);
                        triangles.Add(start + 3); triangles.Add(start + 5); triangles.Add(start + 4);
                    }
                }
                var mesh = new Mesh { name = "DF_ZhaevShoulderStripes" + side };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                owner.meshes.Add(mesh);
                if (HeadAdornmentSkin.Merge(model, bone, mesh, owner.material)) continue;
                var part = new GameObject(mesh.name); part.layer = model.gameObject.layer;
                part.transform.SetParent(bone, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = owner.material;
            }
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
