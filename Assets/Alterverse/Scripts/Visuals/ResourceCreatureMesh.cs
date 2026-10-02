using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Original mesh geometry attached to named articulated pivots.</summary>
    public sealed class ResourceCreatureMesh : MonoBehaviour
    {
        [Serializable] public sealed class Submesh { public int[] triangles; }
        [Serializable] public sealed class Joint { public string path, parent; public float[] position; }
        [Serializable] public sealed class Data
        {
            public int version; public string[] bonePaths;
            public float[] vertices, normals; public int[] boneIndices;
            public Submesh[] submeshes; public Joint[] joints;
        }
        private static readonly Dictionary<string, Data> cache = new Dictionary<string, Data>();
        private Mesh mesh;
        private readonly List<Material> materials = new List<Material>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cache.Clear();
        public static Data Load(string resource)
        {
            if (cache.TryGetValue(resource, out Data data)) return data;
            var asset = Resources.Load<TextAsset>(resource + "/Model");
            if (asset == null) return null;
            try
            {
                using (var zip = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                using (var reader = new StreamReader(zip)) data = JsonUtility.FromJson<Data>(reader.ReadToEnd());
                if (data == null || data.version != 1 || data.vertices == null || data.normals == null ||
                    data.bonePaths == null || data.boneIndices == null || data.submeshes == null ||
                    data.vertices.Length != data.normals.Length || data.vertices.Length != data.boneIndices.Length * 3) return null;
                foreach (int bone in data.boneIndices) if (bone < 0 || bone >= data.bonePaths.Length) return null;
                foreach (Submesh sub in data.submeshes)
                {
                    if (sub == null || sub.triangles == null || sub.triangles.Length % 3 != 0) return null;
                    foreach (int index in sub.triangles) if (index < 0 || index >= data.boneIndices.Length) return null;
                }
                cache.Add(resource, data); return data;
            }
            catch (Exception e) { Debug.LogError("DF: creature resource " + resource + ": " + e.Message); return null; }
        }
        public static ResourceCreatureMesh Apply(Transform root, string resource, Color[] colors)
        {
            Data data = Load(resource);
            if (data == null || colors.Length != data.submeshes.Length || RuntimeShaderLibrary.Lit == null) return null;
            Transform[] bones = new Transform[data.bonePaths.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = string.IsNullOrEmpty(data.bonePaths[i]) ? root : root.Find(data.bonePaths[i]);
                if (bones[i] == null) { Debug.LogError("DF: missing creature joint " + data.bonePaths[i]); return null; }
            }
            Renderer[] old = root.GetComponentsInChildren<Renderer>(true);
            var owner = root.gameObject.AddComponent<ResourceCreatureMesh>();
            owner.Build(root, data, bones, colors);
            foreach (Renderer renderer in old) renderer.enabled = false;
            return owner;
        }
        private void Build(Transform root, Data data, Transform[] bones, Color[] colors)
        {
            Matrix4x4[] frames = new Matrix4x4[bones.Length], normals = new Matrix4x4[bones.Length], binds = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                frames[i] = root.worldToLocalMatrix * bones[i].localToWorldMatrix;
                normals[i] = frames[i].inverse.transpose;
                binds[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            }
            int count = data.boneIndices.Length;
            var vertices = new Vector3[count]; var normal = new Vector3[count]; var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                int bone = data.boneIndices[i], j = i * 3;
                vertices[i] = frames[bone].MultiplyPoint3x4(new Vector3(data.vertices[j], data.vertices[j+1], data.vertices[j+2]));
                normal[i] = normals[bone].MultiplyVector(new Vector3(data.normals[j], data.normals[j+1], data.normals[j+2])).normalized;
                weights[i] = new BoneWeight { boneIndex0 = bone, weight0 = 1f };
            }
            mesh = new Mesh { name = "DF_ReferenceCreatureMesh", indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.normals = normal; mesh.boneWeights = weights; mesh.bindposes = binds;
            mesh.subMeshCount = colors.Length;
            for (int i = 0; i < colors.Length; i++)
            {
                mesh.SetTriangles(data.submeshes[i].triangles, i, false);
                Material m = RuntimeShaderLibrary.Create(colors[i], colors[i].a < .99f, i == 2 ? .7f : .25f, i == 0 ? .25f : .42f, i == 3 ? colors[i] * .25f : Color.black);
                m.name = "DF_ReferenceCreatureSurface_" + i; if (i == 3) m.EnableKeyword("_EMISSION"); materials.Add(m);
            }
            mesh.RecalculateBounds();
            var renderer = root.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.bones = bones; renderer.rootBone = root; renderer.sharedMaterials = materials.ToArray();
            Bounds bounds = mesh.bounds; bounds.Expand(bounds.size * .40f); renderer.localBounds = bounds;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        }
        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
