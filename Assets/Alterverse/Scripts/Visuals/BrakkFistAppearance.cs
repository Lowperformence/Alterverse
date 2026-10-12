using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Instance-only Forge Grip replacement. Each solid fist follows its anatomical hand.</summary>
    [DisallowMultipleComponent]
    public sealed class BrakkFistAppearance : MonoBehaviour
    {
        [Serializable] sealed class Submesh { public int[] triangles; }
        [Serializable] sealed class Hand
        {
            public string side;
            public float[] vertices, normals, uv;
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
        static Geometry cachedGeometry;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        readonly List<GameObject> fists = new List<GameObject>();
        public const string Revision = "brakk-forge-grip-v2-20261012";

        public static void Apply(Transform model, HeroId hero)
        {
            if (hero != HeroId.Rook || model == null || model.GetComponent<BrakkFistAppearance>() != null) return;
            Geometry data = LoadGeometry();
            if (data == null) return;
            var old = new List<Renderer>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "DF_GauntletL" || renderer.name == "DF_GauntletR") old.Add(renderer);
            if (old.Count != 2)
            {
                Debug.LogError("DF: Forge Grip requires Brakk's two original gauntlet renderers.", model);
                return;
            }
            var bones = new Transform[2];
            var toHand = new Matrix4x4[2];
            for (int i = 0; i < 2; i++)
            {
                string name = "Hand" + data.hands[i].side;
                foreach (Transform candidate in model.GetComponentsInChildren<Transform>(true))
                    if (candidate.name == name) { bones[i] = candidate; break; }
                if (bones[i] == null || !TryBindMatrix(model, bones[i], out toHand[i]))
                {
                    Debug.LogError("DF: Forge Grip cannot resolve the bind pose for " + name + ".", model);
                    return;
                }
            }
            // Validate all inputs before replacing either existing fist.
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
                for (int i = 0; i < 2; i++) owner.CreateHand(data.hands[i], bones[i], toHand[i], model.gameObject.layer);
                foreach (Renderer renderer in old)
                {
                    renderer.enabled = false;
                    renderer.name = "DF_LegacyGauntlet" + renderer.name[renderer.name.Length - 1];
                }
            }
            catch (Exception error)
            {
                // A failed replacement leaves the original hands visible.
                foreach (GameObject fist in owner.fists) if (fist != null) fist.SetActive(false);
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
                    hand.uv.Length != hand.vertices.Length / 3 * 2 || hand.submeshes == null ||
                    hand.submeshes.Length != data.materials.Length) return false;
                foreach (float v in hand.vertices) if (!Finite(v)) return false;
                foreach (float n in hand.normals) if (!Finite(n)) return false;
                foreach (float uv in hand.uv) if (!Finite(uv)) return false;
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

        static bool TryBindMatrix(Transform model, Transform hand, out Matrix4x4 toHand)
        {
            toHand = Matrix4x4.identity;
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.name.StartsWith("DF_Anatomy", StringComparison.Ordinal) || skin.sharedMesh == null) continue;
                int index = Array.IndexOf(skin.bones, hand);
                Matrix4x4[] bind = skin.sharedMesh.bindposes;
                if (index < 0 || index >= bind.Length) continue;
                // Use the imported skin's bind transform, not FBX bone-axis assumptions or the current animated pose.
                Matrix4x4 handToModel = model.worldToLocalMatrix * skin.transform.localToWorldMatrix * bind[index].inverse;
                if (Mathf.Abs(handToModel.determinant) < 1e-10f) continue;
                toHand = handToModel.inverse;
                return true;
            }
            return false;
        }

        void CreateHand(Hand data, Transform hand, Matrix4x4 toHand, int layer)
        {
            int count = data.vertices.Length / 3;
            var points = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            Matrix4x4 normalMatrix = toHand.inverse.transpose;
            for (int i = 0; i < count; i++)
            {
                int p = i * 3;
                points[i] = toHand.MultiplyPoint3x4(new Vector3(data.vertices[p], data.vertices[p + 1], data.vertices[p + 2]));
                normals[i] = normalMatrix.MultiplyVector(new Vector3(data.normals[p], data.normals[p + 1], data.normals[p + 2])).normalized;
                uv[i] = new Vector2(data.uv[i * 2], data.uv[i * 2 + 1]);
            }
            var mesh = new Mesh { name = "DF_Brakk_ForgeGripV2_" + data.side,
                indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            meshes.Add(mesh);
            mesh.vertices = points; mesh.normals = normals; mesh.uv = uv;
            mesh.subMeshCount = data.submeshes.Length;
            for (int sub = 0; sub < data.submeshes.Length; sub++)
            {
                int[] indices = (int[])data.submeshes[sub].triangles.Clone();
                if (toHand.determinant < 0f)
                    for (int i = 0; i < indices.Length; i += 3)
                    { int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
                mesh.SetTriangles(indices, sub, false);
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            var fist = new GameObject("DF_Gauntlet" + data.side) { layer = layer };
            fists.Add(fist); fist.transform.SetParent(hand, false);
            fist.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = fist.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials.ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        }

        void Release()
        {
            foreach (GameObject fist in fists) if (fist != null) Destroy(fist);
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in materials) if (material != null) Destroy(material);
            fists.Clear(); meshes.Clear(); materials.Clear();
        }
        void OnDestroy() => Release();
    }
}
