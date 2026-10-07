using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Ione Vaska's pearl mech armor, violet joint reactors and deployed arcane focus, fitted to the existing skeleton.</summary>
    [DisallowMultipleComponent]
    public sealed class UmbralMageAppearance : MonoBehaviour
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
            public Marker[] markers;
            public Submesh[] submeshes;
        }
        private static Geometry geometry;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly Dictionary<string, Transform> markers = new Dictionary<string, Transform>();
        private readonly List<Material> ownedMaterials = new List<Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => geometry = null;

        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<UmbralMageAppearance>() != null) return;
            if (geometry == null)
            {
                TextAsset asset = Resources.Load<TextAsset>("UmbralMage/Model");
                if (asset == null) { Debug.LogError("DF: Umbral Mage model resource is missing."); return; }
                try
                {
                    using (var stream = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                    using (var reader = new StreamReader(stream)) geometry = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
                }
                catch (Exception error) { Debug.LogError("DF: Umbral Mage resource: " + error.Message); return; }
            }
            if (!Valid(geometry)) { Debug.LogError("DF: Umbral Mage geometry is invalid."); return; }
            SkinnedMeshRenderer anatomy = null;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "DF_Anatomy") { anatomy = renderer; break; }
            if (anatomy == null || RuntimeShaderLibrary.Lit == null) return;
            var appearance = model.gameObject.AddComponent<UmbralMageAppearance>();
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
                if (renderer.name == "DF_WeaponL") left = renderer;
                if (renderer.name == "DF_WeaponR") right = renderer;
            }
            if (right == null) return false;
            int[] map = new int[data.boneNames.Length];
            Vector3[] heads = new Vector3[map.Length];
            var lookup = new Dictionary<string, int>();
            for (int i = 0; i < map.Length; i++)
            {
                int index = Array.FindIndex(bones, b => b != null && b.name == data.boneNames[i]);
                if (index < 0) { Debug.LogError("DF: Umbral Mage bone missing: " + data.boneNames[i], this); return false; }
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
                if (data.groups[i] == 0 &&
                    (data.boneNames[b].StartsWith("UpperArm", StringComparison.Ordinal) ||
                     data.boneNames[b].StartsWith("Forearm", StringComparison.Ordinal)))
                {
                    // Thicken the arm around its bone; preserve joint positions and weapon reach.
                    Vector3 origin = Point(data.anchors[b].head);
                    Vector3 axis = (Point(data.anchors[b].tail) - origin).normalized;
                    Vector3 offset = sourcePoint - origin;
                    Vector3 axial = axis * Vector3.Dot(offset, axis);
                    sourcePoint = origin + axial + (offset - axial) * 1.12f;
                    Vector3 normalAxial = axis * Vector3.Dot(sourceNormal, axis);
                    sourceNormal = (normalAxial + (sourceNormal - normalAxial) / 1.12f).normalized;
                }
                if (data.groups[i] == 2)
                {
                    b = lookup["WeaponGripL"];
                    sourcePoint = LeftFocusPoint(sourcePoint);
                    sourceNormal = LeftFocusDirection(sourceNormal);
                }
                Vector3 p = heads[b] + rotations[b] * (sourcePoint - Point(data.anchors[b].head)) * scales[b];
                vertices[i] = toMesh.MultiplyPoint3x4(p);
                normals[i] = toNormal.MultiplyVector(rotations[b] * sourceNormal).normalized;
                weights[i] = new BoneWeight { boneIndex0 = map[b], weight0 = 1f };
            }
            Material[] materials = {
                Surface("Sealed black core", new Color(.018f,.019f,.026f), .12f,.28f),
                Surface("Violet mechanical frame", new Color(.055f,.050f,.075f), .55f,.36f),
                PearlArmor(),
                Surface("Amethyst focus", new Color(.57f,.065f,.95f), .30f,.62f, true),
                Surface("Cyan capacitors", new Color(.015f,.72f,.78f), .20f,.55f, true),
                Surface("White upper arm light strip", Color.white, .10f,.50f, true)
            };
            RendererGroup(anatomy, 0, anatomy.transform, vertices, normals, weights, bones, materials);
            RendererGroup(right, 2, anatomy.transform, vertices, normals, weights, bones, materials);
            if (data.markers != null)
                foreach (Marker marker in data.markers)
                {
                    if (!lookup.TryGetValue(marker.bone, out int b) || marker.position == null || marker.position.Length != 3) continue;
                    b = lookup["WeaponGripL"];
                    Vector3 p = heads[b] + rotations[b] * (LeftFocusPoint(Point(marker.position)) - Point(data.anchors[b].head)) * scales[b];
                    var anchor = new GameObject("DF_Mage" + marker.name).transform;
                    anchor.SetParent(bones[map[b]], false);
                    anchor.position = model.TransformPoint(p);
                    markers.Add(marker.name, anchor);
                    if (marker.name == "StaffTop")
                    {
                        Transform muzzle = Array.Find(all, t => t.name == "DF_Muzzle");
                        if (muzzle != null)
                        {
                            muzzle.SetParent(anchor, false);
                            muzzle.localPosition = Vector3.zero;
                        }
                    }
                }
            return true;
        }

        private Material PearlArmor()
        {
            Material material = Surface("Pearl ceramic armor", new Color(.84f,.86f,.89f), .28f,.56f);
            Shader shader = Resources.Load<Shader>("HeroArt/IoneArmorOrnaments");
            if (shader != null) material.shader = shader;
            else Debug.LogError("DF: Ione armor ornament shader is missing.", this);
            return material;
        }

        private Material Surface(string name, Color color, float metal, float smooth, bool energy = false)
        {
            Material material = RuntimeShaderLibrary.Create(color, false, metal, smooth, energy ? color * .65f : Color.black);
            material.name = "DF_Elyra_" + name;
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
            var mesh = new Mesh { name = "DF_Elyra_Siege_" + group };
            ownedMeshes.Add(mesh);
            mesh.SetVertices(v); mesh.SetNormals(n);
            // Bind-pose projection weights keep ceramic seams stable during arm/leg animation.
            mesh.SetUVs(2, v); mesh.SetUVs(3, n); mesh.boneWeights = w.ToArray();
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            mesh.bindposes = bindposes; mesh.subMeshCount = materials.Length;
            for (int m = 0; m < materials.Length; m++)
            {
                var indices = new List<int>(); int[] sourceIndices = geometry.submeshes[m].triangles;
                for (int i = 0; i < sourceIndices.Length; i += 3)
                {
                    int a = remap[sourceIndices[i]], b = remap[sourceIndices[i+1]], c = remap[sourceIndices[i+2]];
                    if (a >= 0 && b >= 0 && c >= 0) { indices.Add(a); indices.Add(group == 2 ? c : b); indices.Add(group == 2 ? b : c); }
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

        public void AlignGuard(Transform model, Transform leftHand, Transform rightHand, float weight)
        {
            if (weight <= .001f) return;
            AlignHand(leftHand, "StaffGrip", "StaffTop", "StaffFront", true, model, weight);
        }
        private void AlignHand(Transform hand, string center, string top, string front, bool hammer, Transform model, float weight)
        {
            if (hand == null || !markers.TryGetValue(center, out var c) ||
                !markers.TryGetValue(top, out var t) || !markers.TryGetValue(front, out var f)) return;
            // The aperture axis is forward; the former staff front marker now points down.
            Vector3 forward = t.position - c.position;
            Vector3 up = t.position - f.position;
            if (up.sqrMagnitude < .000001f || forward.sqrMagnitude < .000001f) return;
            Quaternion current = Quaternion.LookRotation(forward, up);
            Quaternion desired = Quaternion.LookRotation(model.forward, model.up);
            hand.rotation = Quaternion.Slerp(hand.rotation, desired * Quaternion.Inverse(current) * hand.rotation, weight);
        }

        // Mirror the authored right-hand focus, then rotate its +Y barrel to +Z.
        // Renderer identity stays DF_WeaponR for shop compatibility; skinning follows WeaponGripL.
        private static Vector3 LeftFocusDirection(Vector3 direction) =>
            new Vector3(-direction.x, -direction.z, direction.y);
        private static Vector3 LeftFocusPoint(Vector3 point)
        {
            Vector3 authoredGrip = new Vector3(.34977f, .97991f, .08252f);
            Vector3 leftGrip = new Vector3(-authoredGrip.x, authoredGrip.y, authoredGrip.z);
            return leftGrip + LeftFocusDirection(point - authoredGrip);
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
        private void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}

