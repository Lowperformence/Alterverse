using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Traveler's reference-led graphite/titanium cyber suit, fitted to her existing skeleton.</summary>
    [DisallowMultipleComponent]
    public sealed class ChronoTravelerAppearance : MonoBehaviour
    {
        [Serializable] private sealed class Anchor { public float[] head, tail; }
        [Serializable] private sealed class Submesh { public int[] triangles; }
        [Serializable] private sealed class Geometry
        {
            public int version;
            public string[] boneNames;
            public Anchor[] anchors;
            public float[] vertices, normals;
            public int[] boneIndices;
            public Submesh[] submeshes;
            public bool legEquipmentAuthored;
        }
        private static Geometry geometry;
        private Mesh ownedMesh;
        private readonly List<Material> ownedMaterials = new List<Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => geometry = null;

        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<ChronoTravelerAppearance>() != null) return;
            if (geometry == null)
            {
                TextAsset asset = Resources.Load<TextAsset>("ChronoTraveler/Model");
                if (asset == null) { Debug.LogError("DF: Cyber Ranger model resource is missing."); return; }
                try
                {
                    using (var stream = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                    using (var reader = new StreamReader(stream)) geometry = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
                }
                catch (Exception error) { Debug.LogError("DF: Cyber Ranger resource: " + error.Message); return; }
            }
            if (!Valid(geometry)) { Debug.LogError("DF: Cyber Ranger geometry is invalid."); return; }
            SkinnedMeshRenderer anatomy = null;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "DF_Anatomy") { anatomy = renderer; break; }
            if (anatomy == null || RuntimeShaderLibrary.Lit == null) return;
            var appearance = model.gameObject.AddComponent<ChronoTravelerAppearance>();
            if (!appearance.Build(model, anatomy)) Destroy(appearance);
        }

        private static bool Valid(Geometry data)
        {
            if (data == null || data.version != 1 || data.vertices == null || data.normals == null ||
                data.vertices.Length % 3 != 0 || data.vertices.Length != data.normals.Length ||
                data.boneIndices == null || data.boneIndices.Length != data.vertices.Length / 3 ||
                data.boneNames == null || data.anchors == null || data.anchors.Length != data.boneNames.Length ||
                data.submeshes == null || data.submeshes.Length != 6) return false;
            foreach (Anchor a in data.anchors)
                if (a == null || a.head == null || a.tail == null || a.head.Length != 3 || a.tail.Length != 3) return false;
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
            Transform[] bones = anatomy.bones;
            int[] map = new int[data.boneNames.Length];
            Vector3[] heads = new Vector3[map.Length];
            var lookup = new Dictionary<string, int>();
            for (int i = 0; i < map.Length; i++)
            {
                int index = Array.FindIndex(bones, b => b != null && b.name == data.boneNames[i]);
                if (index < 0) { Debug.LogError("DF: Cyber Ranger bone missing: " + data.boneNames[i], this); return false; }
                map[i] = index;
                heads[i] = model.InverseTransformPoint(bones[index].position);
                lookup.Add(data.boneNames[i], i);
            }
            float unitScale = Vector3.Distance(heads[lookup["Chest"]], heads[lookup["Head"]]) /
                Vector3.Distance(Point(data.anchors[lookup["Chest"]].head), Point(data.anchors[lookup["Head"]].head));
            if (unitScale < .01f || unitScale > 100f) return false;
            // Lower the actual shoulder roots before fitting armor and capturing bind poses.
            // Moving their child chains together preserves arm lengths and grip sockets.
            Vector3 shoulderDrop = model.TransformVector(Vector3.down * (.075f * unitScale));
            foreach (string side in new[] { "L", "R" })
                if (lookup.TryGetValue("UpperArm" + side, out int shoulder))
                    bones[map[shoulder]].position += shoulderDrop;
            for (int i = 0; i < map.Length; i++)
                heads[i] = model.InverseTransformPoint(bones[map[i]].position);
            Quaternion[] rotations = new Quaternion[map.Length];
            float[] scales = new float[map.Length];
            for (int i = 0; i < map.Length; i++)
            {
                string name = data.boneNames[i];
                string tip = Next(name);
                Vector3 reference = Point(data.anchors[i].tail) - Point(data.anchors[i].head);
                Vector3 target = reference * unitScale;
                if (tip != null && lookup.TryGetValue(tip, out int next)) target = heads[next] - heads[i];
                else if (name.StartsWith("Hand", StringComparison.Ordinal))
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
                string boneName = data.boneNames[b];
                if (boneName.StartsWith("Thigh", StringComparison.Ordinal) ||
                    boneName.StartsWith("Shin", StringComparison.Ordinal))
                {
                    Vector3 origin = Point(data.anchors[b].head);
                    Vector3 axis = (Point(data.anchors[b].tail) - origin).normalized;
                    Vector3 offset = sourcePoint - origin;
                    Vector3 axial = axis * Vector3.Dot(offset, axis);
                    sourcePoint = origin + axial + (offset - axial) * 1.12f;
                    Vector3 axialNormal = axis * Vector3.Dot(sourceNormal, axis);
                    sourceNormal = (axialNormal + (sourceNormal - axialNormal) / 1.12f).normalized;
                }
                Vector3 p = heads[b] + rotations[b] * (sourcePoint - Point(data.anchors[b].head)) * scales[b];
                vertices[i] = toMesh.MultiplyPoint3x4(p);
                normals[i] = toNormal.MultiplyVector(rotations[b] * sourceNormal).normalized;
                weights[i] = new BoneWeight { boneIndex0 = map[b], weight0 = 1f };
            }
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindposes[i] = bones[i] != null ? bones[i].worldToLocalMatrix * anatomy.transform.localToWorldMatrix : Matrix4x4.identity;
            ownedMesh = new Mesh { name = "DF_Traveler_ChronoTraveler", indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            ownedMesh.vertices = vertices;
            ownedMesh.normals = normals;
            ownedMesh.boneWeights = weights;
            ownedMesh.bindposes = bindposes;
            ownedMesh.subMeshCount = data.submeshes.Length;
            for (int i = 0; i < data.submeshes.Length; i++) ownedMesh.SetTriangles(data.submeshes[i].triangles, i, false);
            ownedMesh.RecalculateBounds();
            Material[] materials = {
                Surface("Cyber undersuit", new Color(.028f,.041f,.044f), .12f,.28f),
                Surface("Graphite enamel", new Color(.062f,.082f,.10f), .66f,.57f),
                Surface("Brushed titanium", new Color(.30f,.38f,.42f), .82f,.52f),
                Surface("Cyan circuits", new Color(.02f,.65f,.94f), .35f,.48f, true),
                Surface("Optical recess", new Color(.007f,.018f,.016f), .40f,.74f),
                Surface("Blue shoulder stripes", new Color(.025f,.18f,1f), .25f,.5f, true)
            };
            // Replace the existing instance renderer so death/respawn, portrait lighting,
            // bounds consumers and hit-flash code continue to address the same object.
            anatomy.sharedMesh = ownedMesh;
            anatomy.sharedMaterials = materials;
            Bounds bounds = ownedMesh.bounds;
            Vector3 padding = new Vector3(.55f,.30f,.75f) * unitScale;
            Vector3 x = toMesh.MultiplyVector(Vector3.right * padding.x);
            Vector3 y = toMesh.MultiplyVector(Vector3.up * padding.y);
            Vector3 z = toMesh.MultiplyVector(Vector3.forward * padding.z);
            bounds.Expand(new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
            anatomy.localBounds = bounds;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "DF_WeaponR" || renderer.name == "DF_WeaponL") RefinishWeapon(renderer);
            if (!data.legEquipmentAuthored)
                AddLegEquipment(model, anatomy, data, bones, map, heads, lookup, vertices, unitScale);
            return true;
        }

        private void AddLegEquipment(Transform model, SkinnedMeshRenderer anatomy, Geometry data,
            Transform[] bones, int[] map, Vector3[] heads, Dictionary<string, int> lookup,
            Vector3[] vertices, float unitScale)
        {
            Material shell = Surface("Leg phone casing", new Color(.07f,.10f,.13f), .65f,.40f);
            Material cable = Surface("Leg cable insulation", new Color(.018f,.024f,.032f), .15f,.30f);
            Material screen = Surface("Leg phone display", new Color(.025f,.55f,.95f), .15f,.5f, true);
            Matrix4x4 toModel = model.worldToLocalMatrix * anatomy.transform.localToWorldMatrix;
            foreach (string side in new[] { "L", "R" })
            {
                if (!lookup.TryGetValue("Thigh" + side, out int thigh) ||
                    !lookup.TryGetValue("Shin" + side, out int shin)) continue;
                float sign = side == "L" ? -1f : 1f;
                Vector3 middle = Vector3.Lerp(heads[thigh], heads[shin], .48f);
                float outer = sign * middle.x;
                for (int i = 0; i < vertices.Length; i++)
                    if (data.boneIndices[i] == thigh)
                        outer = Mathf.Max(outer, sign * toModel.MultiplyPoint3x4(vertices[i]).x);
                middle.x = sign * (outer + .018f * unitScale);
                var root = new GameObject("DF_TravelerLegEquipment" + side).transform;
                root.SetParent(model, false);
                root.localPosition = middle;
                root.SetParent(bones[map[thigh]], true);
                // Mount phones on the outside of both thighs, with each screen facing out.
                Part(root, "Phone cradle", PrimitiveType.Cube, Vector3.zero,
                    new Vector3(.028f,.15f,.086f) * unitScale, shell);
                Part(root, "Phone screen", PrimitiveType.Cube,
                    new Vector3(sign * .016f,.008f,0) * unitScale,
                    new Vector3(.003f,.112f,.065f) * unitScale, screen);
                Part(root, "Phone button", PrimitiveType.Cube,
                    new Vector3(sign * .018f,-.058f,0) * unitScale,
                    new Vector3(.004f,.008f,.018f) * unitScale, shell);
                for (int lane = 0; lane < 2; lane++)
                {
                    Vector3 previous = Vector3.zero;
                    for (int k = 0; k <= 8; k++)
                    {
                        float t = k / 8f;
                        Vector3 point = new Vector3(sign * (.014f + .024f * Mathf.Sin(t * Mathf.PI)),
                            .15f - .30f * t, -.055f - lane * .019f) * unitScale;
                        if (k > 0)
                        {
                            Vector3 delta = point - previous;
                            Transform segment = Part(root, "Cable " + lane + "-" + k, PrimitiveType.Cylinder,
                                (previous + point) * .5f,
                                new Vector3(.008f * unitScale, delta.magnitude * .5f, .008f * unitScale), cable);
                            segment.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
                        }
                        if (k == 0 || k == 8)
                            Part(root, "Cable connector " + lane + "-" + k, PrimitiveType.Cube,
                                point, new Vector3(.018f,.019f,.015f) * unitScale, shell);
                        previous = point;
                    }
                }
            }
        }

        private static Transform Part(Transform parent, string name, PrimitiveType shape,
            Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private Material Surface(string name, Color color, float metal, float smooth, bool energy = false)
        {
            Material material = RuntimeShaderLibrary.Create(color, false, metal, smooth, energy ? color * .75f : Color.black);
            material.name = "DF_Traveler_" + name;
            if (energy) material.EnableKeyword("_EMISSION");
            ownedMaterials.Add(material);
            return material;
        }

        private void RefinishWeapon(Renderer renderer)
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                Material original = slots[i];
                if (original == null) continue;
                string name = original.name.ToLowerInvariant();
                bool energy = name.Contains("energy") || name.Contains("lens");
                bool metal = name.Contains("silver") || name.Contains("steel") || name.Contains("metal");
                Color color = energy ? new Color(.02f,.65f,.94f) : metal ? new Color(.37f,.46f,.46f) : new Color(.04f,.065f,.065f);
                // Preserve texture/normal maps and the exact weapon geometry and grip sockets.
                Material material = new Material(original) { name = original.name + "_ChronoTraveler" };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal ? .78f : .45f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .48f);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", energy ? color * .8f : Color.black);
                if (energy) material.EnableKeyword("_EMISSION");
                slots[i] = material;
                ownedMaterials.Add(material);
            }
            renderer.sharedMaterials = slots;
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
            if (ownedMesh != null) Destroy(ownedMesh);
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}

