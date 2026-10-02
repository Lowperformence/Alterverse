using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Uses the staff hand's anatomy for both hands, with mirrored skin weights.</summary>
    public sealed class TahlaHandSymmetry : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        public static void Apply(Transform model)
        {
            if (model.GetComponent<TahlaHandSymmetry>() != null) return;
            TahlaHandSymmetry owner = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source = skin.sharedMesh;
                if ((skin.name != "DF_Anatomy" && skin.name != "DF_ArticulatedArmorDetails") || source == null || !source.isReadable) continue;
                Transform[] bones = skin.bones;
                int left = Array.FindIndex(bones, b => b != null && b.name == "HandL");
                int right = Array.FindIndex(bones, b => b != null && b.name == "HandR");
                Matrix4x4[] bind = source.bindposes;
                if (left < 0 || right < 0 || left >= bind.Length || right >= bind.Length) continue;
                Vector3 l = bind[left].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 r = bind[right].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 axis = (r - l).normalized, center = (l + r) * .5f;
                if (axis.sqrMagnitude < .5f) continue;
                BoneWeight[] originalWeights = source.boneWeights;
                if (originalWeights.Length != source.vertexCount) continue;
                int[] remap = new int[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    remap[i] = i;
                    if (bones[i] == null || !bones[i].name.EndsWith("R", StringComparison.Ordinal)) continue;
                    string name = bones[i].name.Substring(0, bones[i].name.Length - 1) + "L";
                    int other = Array.FindIndex(bones, b => b != null && b.name == name);
                    if (other >= 0) remap[i] = other;
                }
                var vertices = new List<Vector3>(source.vertices);
                var normals = new List<Vector3>(source.normals);
                var tangents = new List<Vector4>(source.tangents);
                var colors = new List<Color>(source.colors);
                var weights = new List<BoneWeight>(originalWeights);
                var uvs = new List<Vector4>[8];
                for (int i = 0; i < uvs.Length; i++) { uvs[i] = new List<Vector4>(); source.GetUVs(i, uvs[i]); }
                var copies = new Dictionary<int, int>();
                var submeshes = new List<int>[source.subMeshCount];
                int removed = 0, added = 0;
                for (int sub = 0; sub < submeshes.Length; sub++)
                {
                    int[] triangles = source.GetTriangles(sub);
                    var output = submeshes[sub] = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        if (IsHand(a, left) || IsHand(b, left) || IsHand(c, left)) removed++;
                        else { output.Add(a); output.Add(b); output.Add(c); }
                        if (!(IsHand(a, right) || IsHand(b, right) || IsHand(c, right))) continue;
                        output.Add(Copy(a)); output.Add(Copy(c)); output.Add(Copy(b)); added++;
                    }
                }
                if (removed == 0 || added == 0) continue;
                Mesh mesh = Instantiate(source); mesh.name = source.name + "_TahlaEqualHands";
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices); mesh.boneWeights = weights.ToArray();
                if (normals.Count == vertices.Count) mesh.SetNormals(normals);
                if (tangents.Count == vertices.Count) mesh.SetTangents(tangents);
                if (colors.Count == vertices.Count) mesh.SetColors(colors);
                for (int i = 0; i < uvs.Length; i++) if (uvs[i].Count == vertices.Count) mesh.SetUVs(i, uvs[i]);
                for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
                mesh.RecalculateBounds(); skin.sharedMesh = mesh;
                Bounds bounds = skin.localBounds; bounds.Encapsulate(mesh.bounds); skin.localBounds = bounds;
                if (owner == null) owner = model.gameObject.AddComponent<TahlaHandSymmetry>();
                owner.ownedMeshes.Add(mesh);

                bool IsHand(int vertex, int bone)
                {
                    BoneWeight w = originalWeights[vertex];
                    return (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) +
                        (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0) >= .5f;
                }
                int Copy(int index)
                {
                    if (copies.TryGetValue(index, out int existing)) return existing;
                    int next = vertices.Count; copies.Add(index, next);
                    Vector3 point = vertices[index];
                    vertices.Add(point - 2f * Vector3.Dot(point - center, axis) * axis);
                    if (normals.Count > index) { Vector3 n = normals[index]; normals.Add(n - 2f * Vector3.Dot(n, axis) * axis); }
                    if (tangents.Count > index) { Vector4 t = tangents[index]; Vector3 n = new Vector3(t.x, t.y, t.z); n -= 2f * Vector3.Dot(n, axis) * axis; tangents.Add(new Vector4(n.x, n.y, n.z, -t.w)); }
                    if (colors.Count > index) colors.Add(colors[index]);
                    foreach (var channel in uvs) if (channel.Count > index) channel.Add(channel[index]);
                    BoneWeight w = originalWeights[index];
                    w.boneIndex0 = remap[w.boneIndex0]; w.boneIndex1 = remap[w.boneIndex1];
                    w.boneIndex2 = remap[w.boneIndex2]; w.boneIndex3 = remap[w.boneIndex3];
                    weights.Add(w); return next;
                }
            }
        }
        void OnDestroy() { foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh); }
    }
}
