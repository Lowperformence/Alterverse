using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Authored utility belt, pouches and tools, rigidly skinned to Brakk's existing Hips bone.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkUtilityBeltAppearance : MonoBehaviour
    {
        [Serializable] sealed class Submesh { public int[] triangles; }
        [Serializable] sealed class Surface
        { public string name; public float[] color; public float metallic, smoothness; }
        [Serializable] sealed class Geometry
        {
            public int version;
            public string revision;
            public float[] vertices, normals, uv, pivot;
            public float referenceRigSpan;
            public Submesh[] submeshes;
            public Surface[] materials;
        }
        const string Revision = "brakk-utility-belt-20261012";
        static Geometry cached;
        Mesh mesh;
        GameObject belt;
        readonly List<Material> materials = new List<Material>();

        public static void Apply(Transform model, HeroId hero)
        {
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkUtilityBeltAppearance>() != null) return;
            Geometry data = Load();
            if (data == null) return;
            SkinnedMeshRenderer anatomy = null;
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name == "DF_Anatomy") { anatomy = skin; break; }
            if (anatomy == null || anatomy.sharedMesh == null || !anatomy.sharedMesh.isReadable)
            { Debug.LogError("DF: Brakk utility belt requires readable anatomy.", model); return; }
            var owner = model.gameObject.AddComponent<BrakkUtilityBeltAppearance>();
            try { owner.Build(data, anatomy, model); }
            catch (Exception error)
            {
                owner.Release(); Destroy(owner);
                Debug.LogError("DF: Brakk utility belt could not be attached: " + error.Message, model);
            }
        }
        static Geometry Load()
        {
            if (cached != null) return cached;
            TextAsset asset = Resources.Load<TextAsset>("BrakkUtilityBelt/Model");
            if (asset == null) { Debug.LogError("DF: BrakkUtilityBelt/Model is missing."); return null; }
            try
            {
                Geometry data;
                using (var stream = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                using (var reader = new StreamReader(stream)) data = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
                if (!Valid(data)) throw new InvalidDataException("Belt data contract is invalid.");
                cached = data; return data;
            }
            catch (Exception error) { Debug.LogError("DF: Could not load Brakk utility belt: " + error.Message); return null; }
        }
        static bool Valid(Geometry d)
        {
            if (d == null || d.version != 1 || d.revision != Revision || d.vertices == null || d.vertices.Length == 0 ||
                d.vertices.Length % 3 != 0 || d.normals == null || d.normals.Length != d.vertices.Length ||
                d.uv == null || d.uv.Length != d.vertices.Length / 3 * 2 || d.pivot == null || d.pivot.Length != 3 ||
                !Finite(d.referenceRigSpan) || d.referenceRigSpan <= 0f || d.materials == null || d.materials.Length != 5 ||
                d.submeshes == null || d.submeshes.Length != d.materials.Length) return false;
            foreach (float[] values in new[] { d.vertices, d.normals, d.uv, d.pivot })
                foreach (float value in values) if (!Finite(value)) return false;
            foreach (Surface surface in d.materials)
            {
                if (surface == null || surface.color == null || surface.color.Length != 3 ||
                    !Finite(surface.metallic) || !Finite(surface.smoothness)) return false;
                foreach (float color in surface.color) if (!Finite(color)) return false;
            }
            foreach (Submesh sub in d.submeshes)
            {
                if (sub == null || sub.triangles == null || sub.triangles.Length % 3 != 0) return false;
                foreach (int index in sub.triangles) if (index < 0 || index >= d.vertices.Length / 3) return false;
            }
            return true;
        }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static Matrix4x4 RelativeMatrix(Transform part, Transform ancestor)
        {
            Matrix4x4 result = Matrix4x4.identity;
            for (Transform current = part; current != ancestor; current = current.parent)
            {
                if (current == null) throw new InvalidOperationException("Anatomy is outside the model hierarchy.");
                result = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * result;
            }
            return result;
        }
        void Build(Geometry data, SkinnedMeshRenderer anatomy, Transform model)
        {
            Transform[] bones = anatomy.bones;
            Matrix4x4[] bind = anatomy.sharedMesh.bindposes;
            int hips = Array.FindIndex(bones, b => b != null && b.name == "Hips");
            int spine = Array.FindIndex(bones, b => b != null && b.name == "Spine");
            if (hips < 0 || spine < 0 || hips >= bind.Length || spine >= bind.Length)
                throw new InvalidOperationException("Hips/Spine bind poses are missing.");
            // Bone bind points calibrate source units without BakeMesh or world-space matrices.
            Matrix4x4 toModel = RelativeMatrix(anatomy.transform, model);
            if (!Finite(toModel.determinant) || Mathf.Abs(toModel.determinant) < 1e-12f)
                throw new InvalidOperationException("Invalid local anatomy transform.");
            Matrix4x4 toMesh = toModel.inverse, normalMatrix = toMesh.inverse.transpose;
            Vector3 nativeHips = bind[hips].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 nativeSpine = bind[spine].inverse.MultiplyPoint3x4(Vector3.zero);
            float units = toModel.MultiplyVector(nativeSpine - nativeHips).magnitude / data.referenceRigSpan;
            if (!Finite(units) || units <= 0f) throw new InvalidOperationException("Invalid imported rig units.");
            Vector3 pivot = new Vector3(data.pivot[0], data.pivot[1], data.pivot[2]);
            int count = data.vertices.Length / 3;
            var points = new Vector3[count]; var normals = new Vector3[count];
            var uv = new Vector2[count]; var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                int p = i * 3;
                points[i] = nativeHips + toMesh.MultiplyVector(new Vector3(data.vertices[p], data.vertices[p + 1], data.vertices[p + 2]) - pivot) * units;
                normals[i] = normalMatrix.MultiplyVector(new Vector3(data.normals[p], data.normals[p + 1], data.normals[p + 2])).normalized;
                if (!Finite(points[i].x) || !Finite(points[i].y) || !Finite(points[i].z))
                    throw new InvalidOperationException("Invalid belt vertex after unit conversion.");
                uv[i] = new Vector2(data.uv[i * 2], data.uv[i * 2 + 1]);
                weights[i] = new BoneWeight { boneIndex0 = hips, weight0 = 1f };
            }
            mesh = new Mesh { name = "DF_BrakkUtilityBelt", indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = points; mesh.normals = normals; mesh.uv = uv;
            mesh.bindposes = bind; mesh.boneWeights = weights; mesh.subMeshCount = data.submeshes.Length;
            for (int sub = 0; sub < data.submeshes.Length; sub++)
            {
                int[] indices = (int[])data.submeshes[sub].triangles.Clone();
                if (toMesh.determinant < 0f)
                    for (int i = 0; i < indices.Length; i += 3)
                    { int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
                mesh.SetTriangles(indices, sub, false);
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            foreach (Surface surface in data.materials)
            {
                Color color = new Color(surface.color[0], surface.color[1], surface.color[2], 1f);
                bool glow = surface.name == "Amber status lens";
                Material material = RuntimeShaderLibrary.Create(color, false, surface.metallic, surface.smoothness, glow ? color * .6f : Color.black);
                if (material == null) throw new InvalidOperationException("Belt shader is missing.");
                material.name = "DF_Brakk_Belt_" + surface.name;
                if (glow) material.EnableKeyword("_EMISSION");
                materials.Add(material);
            }
            belt = new GameObject("DF_BrakkUtilityBelt") { layer = anatomy.gameObject.layer };
            belt.transform.SetParent(anatomy.transform, false);
            var renderer = belt.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterials = materials.ToArray();
            renderer.bones = bones; renderer.rootBone = anatomy.rootBone; renderer.quality = SkinQuality.Bone1;
            renderer.localBounds = anatomy.localBounds; renderer.updateWhenOffscreen = true;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            HeroVisualDiagnostics.Trace("DF: Brakk utility belt attached to Hips: " + count + " vertices / " + Revision, model);
        }
        void Release()
        {
            if (belt != null) { belt.SetActive(false); Destroy(belt); belt = null; }
            if (mesh != null) { Destroy(mesh); mesh = null; }
            foreach (Material material in materials) if (material != null) Destroy(material);
            materials.Clear();
        }
        void OnDestroy() => Release();
    }
}
