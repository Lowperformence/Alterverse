using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Fits Tahla's existing swept white hair farther forward over the forehead.</summary>
    [DisallowMultipleComponent]
    public sealed class TahlaHairline : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();

        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<TahlaHairline>() != null) return;
            Transform head = null, chest = null;
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "Head") head = bone;
                if (bone.name == "Chest") chest = bone;
            }
            if (head == null || chest == null) return;
            Vector3 origin = model.InverseTransformPoint(head.position);
            // Tahla's imported bind rig has a canonical 0.22m Chest-to-Head segment.
            float unit = Vector3.Distance(origin, model.InverseTransformPoint(chest.position)) / .22f;
            if (unit < .001f) return;
            TahlaHairline owner = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source = skin.sharedMesh;
                if (skin.name != "DF_Anatomy" || source == null || !source.isReadable) continue;
                int bone = Array.IndexOf(skin.bones, head);
                if (bone < 0 || bone >= source.bindposes.Length) continue;
                var vertices = source.vertices; var weights = source.boneWeights; var slots = skin.sharedMaterials;
                if (vertices.Length != weights.Length) continue;
                Matrix4x4 toModel = model.worldToLocalMatrix * head.localToWorldMatrix * source.bindposes[bone];
                Matrix4x4 toMesh = toModel.inverse;
                var hair = new bool[vertices.Length];
                var scalp = new List<Vector3>();
                for (int sub = 0; sub < source.subMeshCount && sub < slots.Length; sub++)
                {
                    if (slots[sub] == null) continue;
                    bool isHair = slots[sub].name.StartsWith("Tahla white swept hair", StringComparison.OrdinalIgnoreCase);
                    bool isSkin = slots[sub].name.StartsWith("Skin", StringComparison.OrdinalIgnoreCase);
                    if (!isHair && !isSkin) continue;
                    int[] triangles = source.GetTriangles(sub);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        if (isHair)
                        {
                            for (int j = 0; j < 3; j++) if (HeadWeight(weights[triangles[i + j]], bone) >= .9f) hair[triangles[i + j]] = true;
                        }
                        else if (HeadWeight(weights[triangles[i]], bone) >= .9f &&
                            HeadWeight(weights[triangles[i + 1]], bone) >= .9f && HeadWeight(weights[triangles[i + 2]], bone) >= .9f)
                        {
                            for (int j = 0; j < 3; j++) scalp.Add(toModel.MultiplyPoint3x4(vertices[triangles[i + j]]));
                        }
                    }
                }
                bool changed = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!hair[i]) continue;
                    Vector3 p = toModel.MultiplyPoint3x4(vertices[i]);
                    float height = (p.y - origin.y) / unit;
                    if (height < .298f) continue; // Keep the original white brows below the hairline.
                    float front = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.024f, .045f, (p.z - origin.z) / unit));
                    float lower = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.345f, .410f, height));
                    float amount = front * lower;
                    if (amount <= .001f) continue;
                    p.y = Mathf.Max(p.y - .045f * unit * amount, origin.y + .300f * unit);
                    p.z += .045f * unit * amount;
                    // Fit the front edge above the real forehead, blending gently into the swept sides.
                    if (TryScalpFront(scalp, p, out float z))
                        p.z = Mathf.Max(p.z, Mathf.Lerp(p.z, z + .004f * unit, front));
                    vertices[i] = toMesh.MultiplyPoint3x4(p); changed = true;
                }
                if (!changed) continue;
                if (owner == null) owner = model.gameObject.AddComponent<TahlaHairline>();
                Mesh copy = Instantiate(source); copy.name = source.name + "_TahlaForwardHairline";
                copy.vertices = vertices; copy.RecalculateNormals(); copy.RecalculateBounds();
                Vector3[] originalNormals = source.normals, normals = copy.normals;
                if (originalNormals.Length == normals.Length)
                {
                    for (int i = 0; i < normals.Length; i++) if (!hair[i]) normals[i] = originalNormals[i];
                    copy.normals = normals;
                }
                owner.ownedMeshes.Add(copy); skin.sharedMesh = copy;
                Bounds bounds = skin.localBounds; bounds.Encapsulate(copy.bounds); skin.localBounds = bounds;
            }
        }

        static float HeadWeight(BoneWeight w, int bone) =>
            (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) +
            (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);

        static bool TryScalpFront(List<Vector3> scalp, Vector3 p, out float front)
        {
            front = float.NegativeInfinity;
            for (int i = 0; i < scalp.Count; i += 3)
            {
                Vector3 a = scalp[i], b = scalp[i + 1], c = scalp[i + 2];
                float area = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(area) < 1e-10f) continue;
                float u = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / area;
                float v = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / area;
                if (u < -.0001f || v < -.0001f || u + v > 1.0001f) continue;
                front = Mathf.Max(front, u * a.z + v * b.z + (1 - u - v) * c.z);
            }
            return !float.IsNegativeInfinity(front);
        }

        void OnDestroy() { foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh); }
    }
}
