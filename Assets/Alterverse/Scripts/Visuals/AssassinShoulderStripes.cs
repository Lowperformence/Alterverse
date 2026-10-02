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
            Color violet = new Color(.66f, .08f, 1f, 1f);
            owner.material = RuntimeShaderLibrary.Create(violet, false, .15f, .45f, violet * 3f);
            if (owner.material == null) return;
            owner.material.name = "DF_ZhaevShoulderGlow";
            owner.material.EnableKeyword("_EMISSION");
            // Measure the authored shoulder armor in bind space before adding either side.
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
                    if (weight >= .5f) points.Add(toLocal.MultiplyPoint3x4(original[i]));
                }
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                foreach (float position in new[] { .12f, .27f })
                {
                    float along = length * position, radius = length * .20f;
                    foreach (Vector3 p in points)
                        if (Mathf.Abs(Vector3.Dot(p, axis) - along) < length * .15f)
                            radius = Mathf.Max(radius, Vector3.ProjectOnPlane(p, axis).magnitude);
                    radius += length * .014f;
                    float halfWidth = length * .023f;
                    const int segments = 48;
                    for (int i = 0; i < segments; i++)
                    {
                        float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                        Vector3 ra = (across * Mathf.Cos(a) + around * Mathf.Sin(a)) * radius;
                        Vector3 rb = (across * Mathf.Cos(b) + around * Mathf.Sin(b)) * radius;
                        int start = vertices.Count;
                        vertices.Add(axis * (along - halfWidth) + ra); vertices.Add(axis * (along - halfWidth) + rb);
                        vertices.Add(axis * (along + halfWidth) + rb); vertices.Add(axis * (along + halfWidth) + ra);
                        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
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
