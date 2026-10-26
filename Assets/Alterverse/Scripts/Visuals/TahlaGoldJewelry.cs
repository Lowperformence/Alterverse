using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Adds two small gold wrist bracelets and a fine necklace to the original Tahla rig.</summary>
    [DisallowMultipleComponent]
    public sealed class TahlaGoldJewelry : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        Material gold;

        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<TahlaGoldJewelry>() != null) return;
            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            Transform head = Array.Find(bones, b => b.name == "Head");
            Transform chest = Array.Find(bones, b => b.name == "Chest");
            if (head == null || chest == null) return;
            float unit = Vector3.Distance(model.InverseTransformPoint(head.position),
                model.InverseTransformPoint(chest.position)) / .22f;
            if (unit < .001f) return;
            Material material = RuntimeShaderLibrary.Create(new Color(.72f, .51f, .19f),
                false, .78f, .45f, Color.black);
            if (material == null) return;
            material.name = "DF_Tahla_SubtleGoldJewelry";
            var owner = model.gameObject.AddComponent<TahlaGoldJewelry>();
            owner.gold = material;
            SkinnedMeshRenderer body = Array.Find(model.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                s => s.name == "DF_Anatomy" && s.sharedMesh != null && s.sharedMesh.isReadable);
            foreach (string side in new[] { "L", "R" })
                owner.AddBracelet(model, body, side, unit);
            owner.AddNecklace(model, chest, head, unit);
        }

        void AddBracelet(Transform model, SkinnedMeshRenderer skin, string side, float unit)
        {
            if (skin == null) return;
            Mesh source = skin.sharedMesh;
            int forearm = Array.FindIndex(skin.bones, b => b != null && b.name == "Forearm" + side);
            int hand = Array.FindIndex(skin.bones, b => b != null && b.name == "Hand" + side);
            if (forearm < 0 || hand < 0 || forearm >= source.bindposes.Length || hand >= source.bindposes.Length) return;
            Vector3 start = skin.transform.TransformPoint(source.bindposes[forearm].inverse.MultiplyPoint3x4(Vector3.zero));
            Vector3 end = skin.transform.TransformPoint(source.bindposes[hand].inverse.MultiplyPoint3x4(Vector3.zero));
            Vector3 axis = end - start;
            float length = axis.magnitude;
            if (length < .001f) return;
            axis /= length;
            Vector3 x = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, model.up)) < .9f ? model.up : model.right).normalized;
            Vector3 y = Vector3.Cross(axis, x).normalized;
            Vector3 center = Vector3.Lerp(start, end, .84f);
            Vector3[] vertices = source.vertices;
            BoneWeight[] weights = source.boneWeights;
            if (weights.Length != vertices.Length) return;
            float rx = 0, ry = 0;
            int samples = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (Weight(weights[i], forearm) < .5f) continue;
                Vector3 p = skin.transform.TransformPoint(vertices[i]);
                float along = Vector3.Dot(p - start, axis) / length;
                if (along < .74f || along > .94f) continue;
                rx = Mathf.Max(rx, Mathf.Abs(Vector3.Dot(p - center, x)));
                ry = Mathf.Max(ry, Mathf.Abs(Vector3.Dot(p - center, y)));
                samples++;
            }
            if (samples < 4 || rx < .0001f || ry < .0001f) return;
            // Fitted to the rest mesh and parented to the forearm: no new bones, colliders or motion.
            float worldUnit = unit * model.TransformVector(Vector3.right).magnitude;
            float tube = .004f * worldUnit;
            rx += tube * 1.8f;
            ry += tube * 1.8f;
            Transform parent = skin.bones[forearm];
            const int segments = 32;
            var points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points[i] = parent.InverseTransformPoint(center + x * (Mathf.Cos(angle) * rx) + y * (Mathf.Sin(angle) * ry));
            }
            float localTube = parent.InverseTransformVector(x * tube).magnitude;
            AddLoop(parent, "DF_TahlaGoldBracelet" + side, points, localTube, 8);
        }

        void AddNecklace(Transform model, Transform chest, Transform head, float unit)
        {
            Vector3 neck = model.InverseTransformPoint(head.position);
            const int segments = 48;
            var points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float front = Mathf.Max(0, Mathf.Cos(angle));
                Vector3 p = neck + new Vector3(Mathf.Sin(angle) * .098f,
                    .075f - .050f * front, Mathf.Cos(angle) * (.103f + .067f * front)) * unit;
                points[i] = chest.InverseTransformPoint(model.TransformPoint(p));
            }
            float radius = chest.InverseTransformVector(model.TransformVector(Vector3.right * (.003f * unit))).magnitude;
            AddLoop(chest, "DF_TahlaGoldNecklace", points, radius, 6);
        }

        void AddLoop(Transform parent, string name, Vector3[] path, float radius, int sides)
        {
            var vertices = new List<Vector3>(path.Length * sides);
            var normals = new List<Vector3>(path.Length * sides);
            var triangles = new List<int>(path.Length * sides * 6);
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in path) center += p;
            center /= path.Length;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 tangent = (path[(i + 1) % path.Length] - path[(i + path.Length - 1) % path.Length]).normalized;
                Vector3 radial = Vector3.ProjectOnPlane(path[i] - center, tangent).normalized;
                Vector3 binormal = Vector3.Cross(tangent, radial).normalized;
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    Vector3 normal = radial * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    vertices.Add(path[i] + normal * radius);
                    normals.Add(normal);
                }
            }
            for (int i = 0; i < path.Length; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * sides + j, b = i * sides + (j + 1) % sides;
                    int c = ((i + 1) % path.Length) * sides + j;
                    int d = ((i + 1) % path.Length) * sides + (j + 1) % sides;
                    Face(a, b, d); Face(a, d, c);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            var go = new GameObject(name); go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = gold;

            void Face(int a, int b, int c)
            {
                Vector3 outward = normals[a] + normals[b] + normals[c];
                if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), outward) < 0)
                { int swap = b; b = c; c = swap; }
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
        }

        static float Weight(BoneWeight w, int bone) =>
            (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) +
            (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);

        void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            if (gold != null) Destroy(gold);
        }
    }
}
