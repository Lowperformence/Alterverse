using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Replaces only the mesh/materials in Brakk's existing, hand-skinned weapon renderers.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkFistAppearance : MonoBehaviour
    {
        [Serializable] sealed class Submesh { public int[] triangles; }
        [Serializable] sealed class Hand
        {
            public string side;
            public float[] vertices, normals, uv, pivot, legacyModelSize;
            public Submesh[] submeshes;
        }
        [Serializable] sealed class Surface
        {
            public string name;
            public float[] color;
            public float metallic, smoothness;
        }
        [Serializable] sealed class Geometry
        {
            public int version;
            public string revision;
            public Hand[] hands;
            public Surface[] materials;
        }
        sealed class Original
        {
            public SkinnedMeshRenderer renderer;
            public Mesh mesh;
            public Material[] materials;
            public Bounds bounds;
            public bool enabled, updateWhenOffscreen;
        }
        static Geometry cachedGeometry;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Original> originals = new List<Original>();
        public const string Revision = "brakk-forge-grip-v2-legacy-socket-20261012";

        public static void Apply(Transform model, HeroId hero)
        {
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkFistAppearance>() != null) return;
            Geometry data = LoadGeometry();
            if (data == null) return;
            var skins = new SkinnedMeshRenderer[2];
            SkinnedMeshRenderer[] candidates = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
            {
                string name = "DF_Gauntlet" + data.hands[i].side;
                foreach (SkinnedMeshRenderer candidate in candidates)
                    if (candidate.name == name) { skins[i] = candidate; break; }
                if (skins[i] == null)
                {
                    Debug.LogError("DF: Forge Grip cannot find the original weapon renderer " + name + ".", model);
                    return;
                }
            }
            var owner = model.gameObject.AddComponent<BrakkFistAppearance>();
            try
            {
                foreach (Surface surface in data.materials)
                {
                    Color color = new Color(surface.color[0], surface.color[1], surface.color[2], 1f);
                    bool glow = surface.name == "Amber channel";
                    Material material = RuntimeShaderLibrary.Create(color, false, surface.metallic,
                        surface.smoothness, glow ? color * .65f : Color.black);
                    if (material == null) throw new InvalidOperationException("Forge Grip shader is missing.");
                    material.name = "DF_Brakk_FGV2_" + surface.name;
                    if (glow) material.EnableKeyword("_EMISSION");
                    owner.materials.Add(material);
                }
                // Prepare BOTH replacements before touching either original renderer.
                for (int i = 0; i < skins.Length; i++)
                {
                    SkinnedMeshRenderer skin = skins[i];
                    owner.originals.Add(new Original { renderer = skin, mesh = skin.sharedMesh,
                        materials = skin.sharedMaterials, bounds = skin.localBounds,
                        enabled = skin.enabled, updateWhenOffscreen = skin.updateWhenOffscreen });
                    owner.meshes.Add(CreateMesh(data.hands[i], skin, model));
                }
                Material[] surfaces = owner.materials.ToArray();
                for (int i = 0; i < skins.Length; i++)
                {
                    // Keep the old weapon's object, position, rig, bind poses, layer and name.
                    skins[i].sharedMesh = owner.meshes[i];
                    skins[i].sharedMaterials = surfaces;
                    skins[i].enabled = true;
                    // Let Unity update the skin bounds during the existing hand animation.
                    skins[i].updateWhenOffscreen = true;
                }
                HeroVisualDiagnostics.Trace("DF: Brakk Forge Grip legacy socket: both original weapon renderers reused; " +
                    "2x V2 geometry / " + owner.meshes[0].vertexCount + " vertices per fist / " + Revision, model);
            }
            catch (Exception error)
            {
                owner.Release();
                Destroy(owner);
                Debug.LogError("DF: Forge Grip replacement failed: " + error.Message, model);
            }
        }

        static Geometry LoadGeometry()
        {
            if (cachedGeometry != null) return cachedGeometry;
            TextAsset asset = Resources.Load<TextAsset>("BrakkFists/Model");
            if (asset == null) { Debug.LogError("DF: BrakkFists/Model is missing."); return null; }
            try
            {
                Geometry data;
                using (var zip = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                using (var reader = new StreamReader(zip)) data = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
                if (!Valid(data)) throw new InvalidDataException("Forge Grip geometry does not match its contract.");
                cachedGeometry = data;
                return data;
            }
            catch (Exception error) { Debug.LogError("DF: Could not read Forge Grip: " + error.Message); return null; }
        }

        static bool Valid(Geometry data)
        {
            if (data == null || data.version != 1 || data.revision != Revision || data.hands == null ||
                data.hands.Length != 2 || data.materials == null || data.materials.Length != 5) return false;
            var sides = new HashSet<string>();
            foreach (Surface material in data.materials)
            {
                if (material == null || material.color == null || material.color.Length != 3 ||
                    !Finite(material.metallic) || !Finite(material.smoothness)) return false;
                foreach (float c in material.color) if (!Finite(c)) return false;
            }
            foreach (Hand hand in data.hands)
            {
                if (hand == null || (hand.side != "L" && hand.side != "R") || !sides.Add(hand.side) ||
                    hand.vertices == null || hand.vertices.Length == 0 || hand.vertices.Length % 3 != 0 ||
                    hand.normals == null || hand.normals.Length != hand.vertices.Length || hand.uv == null ||
                    hand.pivot == null || hand.pivot.Length != 3 || hand.legacyModelSize == null || hand.legacyModelSize.Length != 3 ||
                    hand.uv.Length != hand.vertices.Length / 3 * 2 || hand.submeshes == null ||
                    hand.submeshes.Length != data.materials.Length) return false;
                foreach (float v in hand.vertices) if (!Finite(v)) return false;
                foreach (float n in hand.normals) if (!Finite(n)) return false;
                foreach (float uv in hand.uv) if (!Finite(uv)) return false;
                foreach (float pivot in hand.pivot) if (!Finite(pivot)) return false;
                foreach (float size in hand.legacyModelSize) if (!Finite(size) || size <= 0f) return false;
                int count = hand.vertices.Length / 3;
                foreach (Submesh submesh in hand.submeshes)
                {
                    if (submesh == null || submesh.triangles == null || submesh.triangles.Length % 3 != 0) return false;
                    foreach (int index in submesh.triangles) if (index < 0 || index >= count) return false;
                }
            }
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        // Compose local transforms only: the portrait stage is 12,000 units from the origin.
        // World matrices and BakeMesh cannot define a reliable attachment there.
        static Matrix4x4 RelativeMatrix(Transform part, Transform ancestor)
        {
            Matrix4x4 result = Matrix4x4.identity;
            for (Transform current = part; current != ancestor; current = current.parent)
            {
                if (current == null) throw new InvalidOperationException("Original fist is outside the model hierarchy.");
                result = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * result;
            }
            return result;
        }

        static Mesh CreateMesh(Hand data, SkinnedMeshRenderer skin, Transform model)
        {
            Mesh source = skin.sharedMesh;
            if (source == null || !source.isReadable || source.vertexCount == 0)
                throw new InvalidOperationException("Original fist mesh is not readable.");
            Transform[] bones = skin.bones;
            int handIndex = Array.FindIndex(bones, bone => bone != null && bone.name == "Hand" + data.side);
            Matrix4x4[] bind = source.bindposes;
            BoneWeight[] oldWeights = source.boneWeights;
            if (handIndex < 0 || handIndex >= bind.Length || oldWeights.Length != source.vertexCount)
                throw new InvalidOperationException("Original fist has no anatomical hand binding.");
            foreach (BoneWeight weight in oldWeights)
                if (weight.boneIndex0 != handIndex || weight.weight0 < .999f ||
                    weight.weight1 > .001f || weight.weight2 > .001f || weight.weight3 > .001f)
                    throw new InvalidOperationException("Original fist is not rigidly bound to its hand.");

            Matrix4x4 toModel = RelativeMatrix(skin.transform, model);
            if (!Finite(toModel.determinant) || Mathf.Abs(toModel.determinant) < 1e-12f)
                throw new InvalidOperationException("Original fist has an invalid local transform.");
            Matrix4x4 toMesh = toModel.inverse;
            Vector3[] oldPoints = source.vertices;
            Bounds oldLocal = new Bounds(oldPoints[0], Vector3.zero);
            Bounds oldModel = new Bounds(toModel.MultiplyPoint3x4(oldPoints[0]), Vector3.zero);
            foreach (Vector3 point in oldPoints)
            {
                oldLocal.Encapsulate(point);
                oldModel.Encapsulate(toModel.MultiplyPoint3x4(point));
            }
            var referenceSize = new Vector3(data.legacyModelSize[0], data.legacyModelSize[1], data.legacyModelSize[2]);
            // Calibrate FBX units against the actual old weapon, rather than assuming metres.
            float units = oldModel.size.magnitude / referenceSize.magnitude;
            if (!Finite(units) || units <= 0f) throw new InvalidOperationException("Original fist has invalid dimensions.");
            int count = data.vertices.Length / 3;
            var points = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            var weights = new BoneWeight[count];
            var pivot = new Vector3(data.pivot[0], data.pivot[1], data.pivot[2]);
            Matrix4x4 normalMatrix = toMesh.inverse.transpose;
            for (int i = 0; i < count; i++)
            {
                int p = i * 3;
                points[i] = toMesh.MultiplyVector(new Vector3(data.vertices[p], data.vertices[p + 1], data.vertices[p + 2]) - pivot) * units;
                normals[i] = normalMatrix.MultiplyVector(new Vector3(data.normals[p], data.normals[p + 1], data.normals[p + 2])).normalized;
                if (!Finite(points[i]) || !Finite(normals[i])) throw new InvalidOperationException("Invalid replacement vertex.");
                uv[i] = new Vector2(data.uv[i * 2], data.uv[i * 2 + 1]);
                weights[i] = new BoneWeight { boneIndex0 = handIndex, weight0 = 1f };
            }
            Bounds replacement = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < count; i++) replacement.Encapsulate(points[i]);
            float sizeRatio = replacement.size.magnitude / oldLocal.size.magnitude;
            if (!Finite(sizeRatio) || sizeRatio < .35f || sizeRatio > 3f)
                throw new InvalidOperationException("Replacement fist dimensions do not match the old weapon's units.");
            Vector3 shift = oldLocal.center - replacement.center;
            for (int i = 0; i < count; i++) points[i] += shift;
            var mesh = new Mesh { name = "DF_Brakk_ForgeGripV2_" + data.side,
                indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            try
            {
                mesh.vertices = points; mesh.normals = normals; mesh.uv = uv;
                mesh.bindposes = bind; mesh.boneWeights = weights;
                mesh.subMeshCount = data.submeshes.Length;
                for (int sub = 0; sub < data.submeshes.Length; sub++)
                {
                    int[] indices = (int[])data.submeshes[sub].triangles.Clone();
                    if (toMesh.determinant < 0f)
                        for (int i = 0; i < indices.Length; i += 3)
                        { int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
                    mesh.SetTriangles(indices, sub, false);
                }
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                return mesh;
            }
            catch { Destroy(mesh); throw; }
        }

        void Release()
        {
            foreach (Original original in originals)
                if (original.renderer != null)
                {
                    original.renderer.sharedMesh = original.mesh;
                    original.renderer.sharedMaterials = original.materials;
                    original.renderer.localBounds = original.bounds;
                    original.renderer.enabled = original.enabled;
                    original.renderer.updateWhenOffscreen = original.updateWhenOffscreen;
                }
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in materials) if (material != null) Destroy(material);
            originals.Clear(); meshes.Clear(); materials.Clear();
        }
        void OnDestroy() => Release();
    }
}
