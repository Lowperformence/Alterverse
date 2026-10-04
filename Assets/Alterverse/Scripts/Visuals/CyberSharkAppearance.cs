using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Nerok's Blender-authored cyber shark and anchor, fitted to the existing skeleton.</summary>
    [DisallowMultipleComponent]
    public sealed class CyberSharkAppearance : MonoBehaviour
    {
        [Serializable] private sealed class Anchor { public float[] head, tail; }
        [Serializable] private sealed class Marker { public string name, bone; public float[] position; }
        [Serializable] private sealed class Submesh { public int[] triangles; }
        [Serializable] private sealed class Geometry
        {
            public int version;
            public string[] boneNames;
            public Anchor[] anchors;
            public float[] vertices, normals;
            public int[] boneIndices, groups;
            public int[] tailVertices;
            public float[] tailPivot;
            public Marker[] markers;
            public Submesh[] submeshes;
        }
        private static Geometry geometry;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly Dictionary<string, Transform> markers = new Dictionary<string, Transform>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Transform tail;
        private Quaternion tailRest;
        private Vector3 tailAxis;
        private float tailTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => geometry = null;

        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<CyberSharkAppearance>() != null) return;
            if (geometry == null)
            {
                TextAsset asset = Resources.Load<TextAsset>("CyberShark/Model");
                if (asset == null) { Debug.LogError("DF: Cyber Shark model resource is missing."); return; }
                try
                {
                    using (var stream = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                    using (var reader = new StreamReader(stream)) geometry = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
                }
                catch (Exception error) { Debug.LogError("DF: Cyber Shark resource: " + error.Message); return; }
            }
            if (!Valid(geometry)) { Debug.LogError("DF: Cyber Shark geometry is invalid."); return; }
            SkinnedMeshRenderer anatomy = null;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "DF_Anatomy") { anatomy = renderer; break; }
            if (anatomy == null || RuntimeShaderLibrary.Lit == null) return;
            var appearance = model.gameObject.AddComponent<CyberSharkAppearance>();
            if (!appearance.Build(model, anatomy)) Destroy(appearance);
        }

        private static bool Valid(Geometry data)
        {
            if (data == null || data.version != 1 || data.vertices == null || data.normals == null ||
                data.vertices.Length % 3 != 0 || data.vertices.Length != data.normals.Length ||
                data.groups == null || data.groups.Length != data.vertices.Length / 3 || data.boneIndices == null || data.boneIndices.Length != data.vertices.Length / 3 ||
                data.boneNames == null || data.anchors == null || data.anchors.Length != data.boneNames.Length ||
                data.submeshes == null || data.submeshes.Length != 6) return false;
            foreach (Anchor a in data.anchors)
                if (a == null || a.head == null || a.tail == null || a.head.Length != 3 || a.tail.Length != 3) return false;
            foreach (int group in data.groups) if (group < 0 || group > 2) return false;
            foreach (int b in data.boneIndices) if (b < 0 || b >= data.boneNames.Length) return false;
            foreach (Submesh sub in data.submeshes)
            {
                if (sub == null || sub.triangles == null || sub.triangles.Length % 3 != 0) return false;
                foreach (int index in sub.triangles) if (index < 0 || index >= data.boneIndices.Length) return false;
            }
            return true;
        }

        private bool Build(Transform model, SkinnedMeshRenderer anatomy)
        {
            Geometry data = geometry;
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            Transform[] bones = new Transform[data.boneNames.Length];
            for (int i = 0; i < bones.Length; i++) bones[i] = Array.Find(all, b => b.name == data.boneNames[i]);
            SkinnedMeshRenderer left = null, right = null;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Brakk's shared animation rig names its hand renderers Gauntlet, not Weapon.
                if (renderer.name == "DF_WeaponL" || renderer.name == "DF_GauntletL") left = renderer;
                if (renderer.name == "DF_WeaponR" || renderer.name == "DF_GauntletR") right = renderer;
            }
            if (right == null) { Debug.LogError("DF: Cyber Shark right hand renderer missing on shared rig.", this); return false; }
            int[] map = new int[data.boneNames.Length];
            Vector3[] heads = new Vector3[map.Length];
            var lookup = new Dictionary<string, int>();
            for (int i = 0; i < map.Length; i++)
            {
                int index = Array.FindIndex(bones, b => b != null && b.name == data.boneNames[i]);
                if (index < 0) { Debug.LogError("DF: Cyber Shark bone missing: " + data.boneNames[i], this); return false; }
                map[i] = index;
                heads[i] = model.InverseTransformPoint(bones[index].position);
                lookup.Add(data.boneNames[i], i);
            }
            float unitScale = Vector3.Distance(heads[lookup["Chest"]], heads[lookup["Head"]]) /
                Vector3.Distance(Point(data.anchors[lookup["Chest"]].head), Point(data.anchors[lookup["Head"]].head));
            if (unitScale < .01f || unitScale > 100f) return false;
            Quaternion[] rotations = new Quaternion[map.Length];
            float[] scales = new float[map.Length];
            for (int i = 0; i < map.Length; i++)
            {
                string name = data.boneNames[i];
                string tip = Next(name);
                Vector3 reference = Point(data.anchors[i].tail) - Point(data.anchors[i].head);
                Vector3 target = reference * unitScale;
                if (tip != null && lookup.TryGetValue(tip, out int next)) target = heads[next] - heads[i];
                else if ((name.StartsWith("Hand", StringComparison.Ordinal) || name.StartsWith("WeaponGrip", StringComparison.Ordinal)))
                {
                    string forearm = "Forearm" + name[name.Length - 1];
                    target = (heads[i] - heads[lookup[forearm]]).normalized * reference.magnitude * unitScale;
                }
                if (target.sqrMagnitude < .000001f || reference.sqrMagnitude < .000001f) return false;
                rotations[i] = Quaternion.FromToRotation(reference, target);
                scales[i] = target.magnitude / reference.magnitude;
            }
            int count = data.vertices.Length / 3;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var weights = new BoneWeight[count];
            Matrix4x4 toMesh = anatomy.transform.worldToLocalMatrix * model.localToWorldMatrix;
            Matrix4x4 toNormal = toMesh.inverse.transpose;
            for (int i = 0; i < count; i++)
            {
                int b = data.boneIndices[i];
                Vector3 sourcePoint = Point(data.vertices, i * 3);
                Vector3 sourceNormal = Point(data.normals, i * 3);
                Vector3 p = heads[b] + rotations[b] * (sourcePoint - Point(data.anchors[b].head)) * scales[b];
                vertices[i] = toMesh.MultiplyPoint3x4(p);
                normals[i] = toNormal.MultiplyVector(rotations[b] * sourceNormal).normalized;
                weights[i] = new BoneWeight { boneIndex0 = map[b], weight0 = 1f };
            }
            if (data.tailVertices != null && data.tailVertices.Length > 0 &&
                data.tailPivot != null && data.tailPivot.Length == 3)
            {
                int hips = lookup["Hips"];
                Vector3 pivot = heads[hips] + rotations[hips] *
                    (Point(data.tailPivot) - Point(data.anchors[hips].head)) * scales[hips];
                tail = new GameObject("DF_Nerok_Tail").transform;
                tail.SetParent(bones[hips], false);
                tail.position = model.TransformPoint(pivot);
                tailRest = tail.localRotation;
                tailAxis = bones[hips].InverseTransformDirection(model.up).normalized;
                int tailIndex = bones.Length;
                Array.Resize(ref bones, tailIndex + 1);
                bones[tailIndex] = tail;
                foreach (int index in data.tailVertices)
                    if (index >= 0 && index < weights.Length && data.groups[index] == 0)
                        weights[index] = new BoneWeight { boneIndex0 = tailIndex, weight0 = 1f };
            }
            Material[] materials = {
                Surface("Pirate leather", new Color(.035f,.022f,.014f), .12f,.28f),
                Surface("Natural light grey shark skin", new Color(.58f,.61f,.62f), .08f,.40f),
                Surface("Naval armor", new Color(.035f,.065f,.078f), .55f,.40f),
                Surface("Tidal channels", new Color(.04f,.78f,1f), .30f,.62f, true),
                Surface("Brass fittings", new Color(.72f,.39f,.105f), .20f,.55f, true),
                Surface("White ventral skin and teeth", new Color(.93f,.94f,.91f), .10f,.35f)
            };
            RendererGroup(anatomy, 0, anatomy.transform, vertices, normals, weights, bones, materials);
            // The inherited gauntlet renderer has its own FBX transform/root bone.
            // Keep the new anchor independent of the inherited gauntlet scale:
            // both new skins use the anatomy's coordinate frame and skeleton root.
            right.sharedMesh = null; right.enabled = false;
            right.name = "DF_Nerok_LegacyGauntletR";
            var anchorObject = new GameObject("DF_GauntletR");
            anchorObject.layer = anatomy.gameObject.layer;
            anchorObject.transform.SetParent(anatomy.transform, false);
            var anchorRenderer = anchorObject.AddComponent<SkinnedMeshRenderer>();
            anchorRenderer.rootBone = anatomy.rootBone;
            anchorRenderer.quality = anatomy.quality;
            anchorRenderer.updateWhenOffscreen = anatomy.updateWhenOffscreen;
            RendererGroup(anchorRenderer, 2, anatomy.transform, vertices, normals, weights, bones, materials);
            if (left != null) { left.sharedMesh = null; left.enabled = false; }
            return true;
        }

        private Material Surface(string name, Color color, float metal, float smooth, bool energy = false)
        {
            Material material = RuntimeShaderLibrary.Create(color, false, metal, smooth, energy ? color * .75f : Color.black);
            material.name = "DF_Nerok_" + name;
            if (energy) material.EnableKeyword("_EMISSION");
            ownedMaterials.Add(material);
            return material;
        }

        private void RendererGroup(SkinnedMeshRenderer renderer, int group, Transform source,
            Vector3[] vertices, Vector3[] normals, BoneWeight[] weights, Transform[] bones, Material[] materials)
        {
            var remap = new int[vertices.Length];
            var v = new List<Vector3>(); var n = new List<Vector3>(); var w = new List<BoneWeight>();
            Matrix4x4 convert = renderer.transform.worldToLocalMatrix * source.localToWorldMatrix;
            Matrix4x4 normalMatrix = convert.inverse.transpose;
            for (int i = 0; i < vertices.Length; i++)
            {
                remap[i] = -1;
                if (geometry.groups[i] != group) continue;
                remap[i] = v.Count;
                v.Add(convert.MultiplyPoint3x4(vertices[i]));
                n.Add(normalMatrix.MultiplyVector(normals[i]).normalized); w.Add(weights[i]);
            }
            var mesh = new Mesh { name = "DF_Nerok_Siege_" + group };
            ownedMeshes.Add(mesh);
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(2, v); mesh.boneWeights = w.ToArray();
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            mesh.bindposes = bindposes; mesh.subMeshCount = materials.Length;
            for (int m = 0; m < materials.Length; m++)
            {
                var indices = new List<int>(); int[] sourceIndices = geometry.submeshes[m].triangles;
                for (int i = 0; i < sourceIndices.Length; i += 3)
                {
                    int a = remap[sourceIndices[i]], b = remap[sourceIndices[i+1]], c = remap[sourceIndices[i+2]];
                    if (a >= 0 && b >= 0 && c >= 0) { indices.Add(a); indices.Add(b); indices.Add(c); }
                }
                mesh.SetTriangles(indices, m, false);
            }
            mesh.RecalculateBounds();
            if (group > 0)
            {
                Material[] independent = new Material[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    independent[i] = new Material(materials[i]); ownedMaterials.Add(independent[i]);
                }
                materials = independent;
            }
            renderer.sharedMesh = mesh; renderer.bones = bones; renderer.sharedMaterials = materials;
            Bounds bounds = mesh.bounds; bounds.Expand(bounds.size * .45f); renderer.localBounds = bounds;
        }

        private static Vector3 Point(float[] values, int offset = 0) => new Vector3(values[offset], values[offset + 1], values[offset + 2]);
        private static string Next(string name)
        {
            if (name == "Hips") return "Spine";
            if (name == "Spine") return "Chest";
            if (name == "Chest") return "Head";
            if (name.StartsWith("UpperArm", StringComparison.Ordinal)) return "Forearm" + name[name.Length - 1];
            if (name.StartsWith("Forearm", StringComparison.Ordinal)) return "Hand" + name[name.Length - 1];
            if (name.StartsWith("Thigh", StringComparison.Ordinal)) return "Shin" + name[name.Length - 1];
            if (name.StartsWith("Shin", StringComparison.Ordinal)) return "Foot" + name[name.Length - 1];
            return null;
        }
        private void LateUpdate()
        {
            if (tail == null || Time.deltaTime <= 0f) return;
            // Start at the centred neutral pose. A full sweep takes eight seconds;
            // only the tail bone moves, independent of the hero's combat animation.
            tailTime += Time.deltaTime;
            tail.localRotation = Quaternion.AngleAxis(45f * Mathf.Sin(tailTime * Mathf.PI / 4f), tailAxis) * tailRest;
        }
        private void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Release(mesh);
            foreach (Material material in ownedMaterials) if (material != null) Release(material);
        }
        private static void Release(UnityEngine.Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
