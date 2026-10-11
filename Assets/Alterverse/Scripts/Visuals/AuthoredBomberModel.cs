using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Blender-authored geometry only; the existing flyover owns all behavior.</summary>
    public static class AuthoredBomberModel
    {
        [Serializable] sealed class Part
        {
            public string name;
            public int material;
            public float[] position, scale, vertices, normals, uv;
            public int[] triangles;
        }
        [Serializable] sealed class Library { public int version; public Part[] parts; }
        static Library library;
        static Mesh[] meshes;
        static bool attempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            ReleaseMeshes();
            library = null;
            attempted = false;
        }

        public static bool TryBuild(Transform visual, Material armor, Material dark, Material energy,
            out Transform engineGlowL, out Transform engineGlowR)
        {
            engineGlowL = engineGlowR = null;
            if (visual == null || armor == null || dark == null || energy == null || !Load()) return false;
            Material[] palette = { armor, dark, energy };
            for (int i = 0; i < library.parts.Length; i++)
            {
                Part part = library.parts[i];
                var go = new GameObject("DF_Bomber_" + part.name);
                go.transform.SetParent(visual, false);
                go.transform.localPosition = V(part.position);
                go.transform.localScale = V(part.scale);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = palette[part.material];
                renderer.shadowCastingMode = part.material == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = false;
                if (part.name == "ThrusterL") engineGlowL = go.transform;
                if (part.name == "ThrusterR") engineGlowR = go.transform;
            }
            return true;
        }

        static bool Load()
        {
            if (attempted) return library != null;
            attempted = true;
            TextAsset asset = Resources.Load<TextAsset>("AuthoredBomber/Model");
            if (asset == null) return false;
            try
            {
                using (var source = new MemoryStream(asset.bytes))
                using (var compressed = new GZipStream(source, CompressionMode.Decompress))
                using (var reader = new StreamReader(compressed))
                    library = JsonUtility.FromJson<Library>(reader.ReadToEnd());
                if (!Valid(library)) throw new InvalidDataException("Invalid bomber geometry export.");
                meshes = new Mesh[library.parts.Length];
                for (int i = 0; i < meshes.Length; i++) meshes[i] = CreateMesh(library.parts[i]);
                foreach (Part part in library.parts)
                {
                    part.vertices = part.normals = part.uv = null;
                    part.triangles = null;
                }
                return true;
            }
            catch (Exception error)
            {
                ReleaseMeshes();
                library = null;
                Debug.LogWarning("DF: Authored bomber art unavailable; using the original visual. " + error.Message);
                return false;
            }
        }

        static bool Valid(Library data)
        {
            if (data == null || data.version != 1 || data.parts == null || data.parts.Length != 5) return false;
            string[] names = { "Hull", "Mechanics", "Energy", "ThrusterL", "ThrusterR" };
            for (int i = 0; i < data.parts.Length; i++)
            {
                Part part = data.parts[i];
                if (part == null || part.name != names[i] || part.material != (i < 3 ? i : 2) ||
                    part.position == null || part.position.Length != 3 || part.scale == null || part.scale.Length != 3 ||
                    part.vertices == null || part.vertices.Length == 0 || part.vertices.Length % 3 != 0 ||
                    part.normals == null || part.normals.Length != part.vertices.Length ||
                    part.uv == null || part.uv.Length != part.vertices.Length / 3 * 2 ||
                    part.triangles == null || part.triangles.Length == 0 || part.triangles.Length % 3 != 0) return false;
                foreach (int index in part.triangles)
                    if (index < 0 || index >= part.vertices.Length / 3) return false;
            }
            return true;
        }

        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

        static Mesh CreateMesh(Part part)
        {
            int count = part.vertices.Length / 3;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(part.vertices[i * 3], part.vertices[i * 3 + 1], part.vertices[i * 3 + 2]);
                normals[i] = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
                uv[i] = new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
            }
            var mesh = new Mesh { name = "DF_BlenderBomber_" + part.name };
            if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = part.triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            // The same immutable five meshes serve every flyover; no per-pass mesh copies.
            mesh.UploadMeshData(true);
            return mesh;
        }

        static void ReleaseMeshes()
        {
            if (meshes == null) return;
            foreach (Mesh mesh in meshes)
                if (mesh != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                    else UnityEngine.Object.DestroyImmediate(mesh);
                }
            meshes = null;
        }
    }
}
