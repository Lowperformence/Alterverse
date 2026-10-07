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
    [DefaultExecutionOrder(960)]
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
            public int[] boneIndices2;
            public float[] boneWeights2;
            public int[] tailVertices;
            public float[] tailPivot;
            public float[] hookGrip;
            public Marker[] markers;
            public Submesh[] submeshes;
        }
        private static Geometry geometry;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly Dictionary<string, Transform> markers = new Dictionary<string, Transform>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Transform tail, freeUpperArm, freeForearm, freeHand, anchorGrip, hookSwing;
        private Quaternion hookRest;
        private Vector3 hookHangDirection;
        private Vector2 hookAngles, hookAngularVelocity;
        private Quaternion tailRest;
        private Quaternion anchorGripRest;
        private Vector3 tailAxis;
        private float tailTime;
        private HeroVisualMotion motion;
        private MeleeBasicAttackAnimation combatPose;

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
            bool hasSecondary = data.boneIndices2 != null || data.boneWeights2 != null;
            if (hasSecondary)
            {
                int count = data.boneIndices.Length;
                if (data.boneIndices2 == null || data.boneWeights2 == null ||
                    data.boneIndices2.Length != count || data.boneWeights2.Length != count) return false;
                for (int i = 0; i < count; i++)
                    if (data.boneIndices2[i] < 0 || data.boneIndices2[i] >= data.boneNames.Length ||
                        float.IsNaN(data.boneWeights2[i]) || float.IsInfinity(data.boneWeights2[i]) ||
                        data.boneWeights2[i] < 0f || data.boneWeights2[i] > 1f) return false;
            }
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
            freeUpperArm = Array.Find(all, b => b.name == "UpperArmL");
            freeForearm = Array.Find(all, b => b.name == "ForearmL");
            freeHand = Array.Find(all, b => b.name == "HandL");
            motion = GetComponentInParent<HeroVisualMotion>();
            combatPose = GetComponentInParent<MeleeBasicAttackAnimation>();
            Transform[] bones = new Transform[data.boneNames.Length];
            for (int i = 0; i < bones.Length; i++) bones[i] = Array.Find(all, b => b.name == data.boneNames[i]);
            anchorGrip = Array.Find(all, b => b.name == "WeaponGripR");
            if (anchorGrip != null) anchorGripRest = anchorGrip.localRotation;
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
            // The shared brawler forearms are much broader than the source shark palms.
            // Fit the existing complete hands (palm, four fingers, thumb and knuckles)
            // together about each wrist; never scale the independent anchor.
            for (int i = 0; i < map.Length; i++)
                if (data.boneNames[i] == "HandL" || data.boneNames[i] == "HandR") scales[i] *= 1.5f;
            int count = data.vertices.Length / 3;
            var digits = new bool[count];
            foreach (int index in data.submeshes[2].triangles)
                if (HandVertex(index)) digits[index] = true;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var weights = new BoneWeight[count];
            Matrix4x4 toMesh = anatomy.transform.worldToLocalMatrix * model.localToWorldMatrix;
            Matrix4x4 toNormal = toMesh.inverse.transpose;
            for (int i = 0; i < count; i++)
            {
                int b = data.boneIndices[i];
                Vector3 sourcePoint = Point(data.vertices, i * 3);
                // Finger tubes were buried behind the palm surface. Expose the curled
                // fingers and thumb while keeping their roots intersecting the palm.
                if (digits[i]) sourcePoint.z += .025f;
                Vector3 sourceNormal = Point(data.normals, i * 3);
                Vector3 p = heads[b] + rotations[b] * (sourcePoint - Point(data.anchors[b].head)) * scales[b];
                Vector3 n = rotations[b] * sourceNormal;
                float blend = data.boneWeights2 != null ? data.boneWeights2[i] : 0f;
                int secondary = data.boneIndices2 != null ? data.boneIndices2[i] : b;
                if (blend > 0f && secondary != b)
                {
                    Vector3 p2 = heads[secondary] + rotations[secondary] *
                        (sourcePoint - Point(data.anchors[secondary].head)) * scales[secondary];
                    p = Vector3.Lerp(p, p2, blend);
                    n = Vector3.Lerp(n, rotations[secondary] * sourceNormal, blend);
                    // Unity expects the strongest influence first.
                    weights[i] = blend <= .5f
                        ? new BoneWeight
                        {
                            boneIndex0 = map[b], weight0 = 1f - blend,
                            boneIndex1 = map[secondary], weight1 = blend
                        }
                        : new BoneWeight
                        {
                            boneIndex0 = map[secondary], weight0 = blend,
                            boneIndex1 = map[b], weight1 = 1f - blend
                        };
                }
                else weights[i] = new BoneWeight { boneIndex0 = map[b], weight0 = 1f };
                vertices[i] = toMesh.MultiplyPoint3x4(p);
                normals[i] = toNormal.MultiplyVector(n).normalized;
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
            if (Array.Exists(data.groups, group => group == 1))
            {
                int grip = lookup["WeaponGripL"];
                Vector3 sourcePivot = data.hookGrip != null && data.hookGrip.Length == 3
                    ? Point(data.hookGrip) : Point(data.anchors[grip].head) + new Vector3(-.012f, -.052f, .070f);
                Vector3 pivot = heads[grip] + rotations[grip] *
                    (sourcePivot - Point(data.anchors[grip].head)) * scales[grip];
                hookSwing = new GameObject("DF_Nerok_HookSwing").transform;
                hookSwing.SetParent(bones[grip], false);
                hookSwing.position = model.TransformPoint(pivot);
                hookRest = hookSwing.localRotation;
                hookHangDirection = hookSwing.InverseTransformDirection(
                    model.TransformDirection(rotations[grip] * Vector3.down)).normalized;
                int hookIndex = bones.Length;
                Array.Resize(ref bones, hookIndex + 1);
                bones[hookIndex] = hookSwing;
                for (int i = 0; i < count; i++)
                    if (data.groups[i] == 1) weights[i] = new BoneWeight { boneIndex0 = hookIndex, weight0 = 1f };
            }
            Material[] materials = {
                Surface("Pirate leather", new Color(.035f,.022f,.014f), .12f,.28f),
                Surface("Natural light grey shark skin", new Color(.58f,.61f,.62f), .08f,.40f),
                Surface("Naval armor", new Color(.035f,.065f,.078f), .55f,.40f),
                Surface("Tidal channels", new Color(.04f,.78f,1f), .30f,.62f, true),
                Surface("Brass fittings", new Color(.48f,.29f,.12f), .72f,.32f),
                Surface("White ventral skin and teeth", new Color(.93f,.94f,.91f), .10f,.35f),
                JacketFabric(),
                Surface("Black shark eyes and nostrils", new Color(.003f,.004f,.006f), 0f,.32f)
            };
            if (data.boneWeights2 != null) anatomy.quality = SkinQuality.Bone2;
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
            var hookObject = new GameObject("DF_Nerok_ChainHook");
            hookObject.layer = anatomy.gameObject.layer;
            hookObject.transform.SetParent(anatomy.transform, false);
            var hookRenderer = hookObject.AddComponent<SkinnedMeshRenderer>();
            hookRenderer.rootBone = anatomy.rootBone;
            hookRenderer.quality = anatomy.quality;
            hookRenderer.updateWhenOffscreen = anatomy.updateWhenOffscreen;
            RendererGroup(hookRenderer, 1, anatomy.transform, vertices, normals, weights, bones, materials);
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

        private Material JacketFabric()
        {
            Shader shader = Resources.Load<Shader>("CyberShark/JacketFabric");
            if (shader == null || !shader.isSupported)
                return Surface("Brown canvas jacket", new Color(.22f,.12f,.065f), 0f,.08f);
            var material = new Material(shader) { name = "DF_Nerok_WovenBrownJacket" };
            ownedMaterials.Add(material);
            return material;
        }

        // The resource has six authored material slots. Its vest and tails share slot 2
        // with metal armor. Match their exact source plate vertices rather than a broad
        // bounding box, which would also recolor nearby armor or shark skin.
        private static bool JacketVertex(int index)
        {
            string bone = geometry.boneNames[geometry.boneIndices[index]];
            Vector3 p = Point(geometry.vertices, index * 3);
            p.x = Mathf.Abs(p.x);
            if (bone == "Chest") return PlateVertex(p, VestOutline, .142f,.035f);
            if (bone == "ThighL" || bone == "ThighR") return PlateVertex(p, TailOutline, .077f,.024f);
            return false;
        }

        private static readonly Vector2[] VestOutline = {
            new Vector2(.135f,1.55f), new Vector2(.245f,1.49f), new Vector2(.218f,1.24f),
            new Vector2(.174f,1.075f), new Vector2(.085f,1.095f), new Vector2(.115f,1.30f) };
        private static readonly Vector2[] TailOutline = {
            new Vector2(.082f,1.07f), new Vector2(.22f,1.07f), new Vector2(.27f,.73f),
            new Vector2(.235f,.56f), new Vector2(.115f,.65f) };

        private static bool PlateVertex(Vector3 p, Vector2[] outline, float z, float depth)
        {
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in outline) center += point;
            center /= outline.Length;
            for (int row = 0; row < 3; row++)
            {
                float scale = row == 0 ? 1f : row == 1 ? .93f : .82f;
                float offset = depth * (row == 0 ? -.45f : row == 1 ? .2f : .65f);
                foreach (Vector2 point in outline)
                {
                    Vector2 xy = center + (point - center) * scale;
                    Vector3 vertex = new Vector3(xy.x, xy.y, z + offset - .08f * xy.x * xy.x);
                    if ((p - vertex).sqrMagnitude < .000000001f) return true;
                }
            }
            return (p - new Vector3(center.x,center.y,z+depth*.88f)).sqrMagnitude < .000000001f;
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
            // Group 2 is Nerok's independent anchor. Shrink only the weapon geometry by 30%
            // around the right-hand grip so its hand contact and animation path do not move.
            if (group == 2 && v.Count > 0)
            {
                Transform grip = Array.Find(bones, b => b != null && b.name == "WeaponGripR");
                Vector3 pivot = Vector3.zero;
                if (grip != null) pivot = renderer.transform.InverseTransformPoint(grip.position);
                else
                {
                    foreach (Vector3 point in v) pivot += point;
                    pivot /= v.Count;
                }
                for (int i = 0; i < v.Count; i++) v[i] = pivot + (v[i] - pivot) * .70f;
            }
            var mesh = new Mesh { name = "DF_Nerok_Siege_" + group };
            ownedMeshes.Add(mesh);
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(2, v); mesh.boneWeights = w.ToArray();
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            mesh.bindposes = bindposes; mesh.subMeshCount = materials.Length;
            var materialTriangles = new List<int>[materials.Length];
            for (int m = 0; m < materials.Length; m++) materialTriangles[m] = new List<int>();
            for (int m = 0; m < geometry.submeshes.Length; m++)
            {
                int[] sourceIndices = geometry.submeshes[m].triangles;
                for (int i = 0; i < sourceIndices.Length; i += 3)
                {
                    int a = remap[sourceIndices[i]], b = remap[sourceIndices[i+1]], c = remap[sourceIndices[i+2]];
                    if (a < 0 || b < 0 || c < 0) continue;
                    // The original nearly black fingers disappear beside the grey palm.
                    // Give only hand-bound armor-slot triangles the same shark-skin finish.
                    int material = m;
                    if (group == 0 && m == 0 &&
                        geometry.boneNames[geometry.boneIndices[sourceIndices[i]]] == "Head" &&
                        geometry.boneNames[geometry.boneIndices[sourceIndices[i+1]]] == "Head" &&
                        geometry.boneNames[geometry.boneIndices[sourceIndices[i+2]]] == "Head") material = 7;
                    if (group == 0 && m == 2 && HandVertex(sourceIndices[i]) &&
                        HandVertex(sourceIndices[i+1]) && HandVertex(sourceIndices[i+2])) material = 1;
                    if (group == 0 && m == 2 && JacketVertex(sourceIndices[i]) &&
                        JacketVertex(sourceIndices[i+1]) && JacketVertex(sourceIndices[i+2])) material = 6;
                    materialTriangles[material].Add(a);
                    materialTriangles[material].Add(b);
                    materialTriangles[material].Add(c);
                }
            }
            for (int m = 0; m < materials.Length; m++) mesh.SetTriangles(materialTriangles[m], m, false);
            mesh.RecalculateBounds();
            if (group > 0)
            {
                Material[] independent = new Material[materials.Length];
                Material anchorMetal = group == 1 || group == 2 ? NerokAnchorMaterial.Create(true) : null;
                if (group == 1 && anchorMetal != null)
                {
                    var rustBrown = new Color(.35f, .14f, .045f);
                    anchorMetal.name = "DF_Nerok_RustBrownChainHook";
                    if (anchorMetal.HasProperty("_SilverColor")) anchorMetal.SetColor("_SilverColor", rustBrown);
                    if (anchorMetal.HasProperty("_RustColor")) anchorMetal.SetColor("_RustColor", new Color(.22f, .065f, .018f));
                    if (anchorMetal.HasProperty("_BaseColor")) anchorMetal.SetColor("_BaseColor", rustBrown);
                    if (anchorMetal.HasProperty("_Color")) anchorMetal.SetColor("_Color", rustBrown);
                }
                if (anchorMetal != null) ownedMaterials.Add(anchorMetal);
                for (int i = 0; i < materials.Length; i++)
                {
                    independent[i] = anchorMetal != null ? anchorMetal : new Material(materials[i]);
                    if (anchorMetal == null) ownedMaterials.Add(independent[i]);
                    if (group == 1 && i == 4 && anchorMetal != null)
                    {
                        Material darkHook = new Material(anchorMetal) { name = "DF_Nerok_DarkerRustBrownHook" };
                        var darkBrown = new Color(.24f, .085f, .025f);
                        if (darkHook.HasProperty("_SilverColor")) darkHook.SetColor("_SilverColor", darkBrown);
                        if (darkHook.HasProperty("_RustColor")) darkHook.SetColor("_RustColor", new Color(.12f, .030f, .008f));
                        if (darkHook.HasProperty("_BaseColor")) darkHook.SetColor("_BaseColor", darkBrown);
                        if (darkHook.HasProperty("_Color")) darkHook.SetColor("_Color", darkBrown);
                        ownedMaterials.Add(darkHook); independent[i] = darkHook;
                    }
                }
                materials = independent;
            }
            renderer.sharedMesh = mesh; renderer.bones = bones; renderer.sharedMaterials = materials;
            Bounds bounds = mesh.bounds; bounds.Expand(bounds.size * .45f); renderer.localBounds = bounds;
        }

        private static bool HandVertex(int index)
        {
            string bone = geometry.boneNames[geometry.boneIndices[index]];
            return bone == "HandL" || bone == "HandR";
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
            if (tail != null && Time.deltaTime > 0f)
            {
                // Start at the centred neutral pose. A full sweep takes eight seconds;
                // only the tail bone moves, independent of the hero's combat animation.
                tailTime += Time.deltaTime;
                tail.localRotation = Quaternion.AngleAxis(45f * Mathf.Sin(tailTime * Mathf.PI / 4f), tailAxis) * tailRest;
            }
            PoseFreeArmAlongGuide();
            UpdateHookSwing();
            // Run after the final authored/combat pose (execution order 960).
            // Pitch at the grip moves the upper eye clear of the forearm on every frame.
            if (anchorGrip != null) anchorGrip.localRotation = anchorGripRest * Quaternion.Euler(14f, 0f, 0f);
        }
        private void UpdateHookSwing()
        {
            if (hookSwing == null) return;
            if (motion == null) motion = GetComponentInParent<HeroVisualMotion>();
            float movement = motion != null ? Mathf.InverseLerp(.10f, 3.5f, motion.PlanarSpeed) : 0f;
            float phase = motion != null ? motion.GaitPhase : 0f;
            var target = new Vector2(Mathf.Sin(phase) * 10f, Mathf.Sin(phase + .65f) * 4f) * movement;
            // Swing only below the held link; ease back to rest when movement stops.
            if (Time.deltaTime > 0f)
                hookAngles = Vector2.SmoothDamp(hookAngles, target, ref hookAngularVelocity, .10f,
                    90f, Mathf.Min(Time.deltaTime, .05f));
            // Cancel wrist/forearm tilt at the held link so the resting chain hangs straight down.
            Quaternion inherited = hookSwing.parent.rotation * hookRest;
            Quaternion hanging = Quaternion.FromToRotation(inherited * hookHangDirection, Vector3.down) * inherited;
            hookSwing.rotation = Quaternion.AngleAxis(hookAngles.x, transform.right) *
                Quaternion.AngleAxis(hookAngles.y, transform.forward) * hanging;
        }
        private void PoseFreeArmAlongGuide()
        {
            if (freeUpperArm == null || freeForearm == null || freeHand == null) return;
            if (motion == null) motion = GetComponentInParent<HeroVisualMotion>();
            if (combatPose == null) combatPose = GetComponentInParent<MeleeBasicAttackAnimation>();
            // The anchor uses the right-hand grip, so the left chain is Nerok's free arm.
            // Match the marked guide: almost straight down, about ten degrees inward toward the torso.
            // Only apply in the normal idle/guard state; attacks and abilities keep authored motion.
            if (combatPose == null || !combatPose.OwnsPose || combatPose.StrikeActive ||
                motion != null && motion.AbilityActionActive) return;

            Vector3 upper = freeForearm.position - freeUpperArm.position;
            Vector3 forearm = freeHand.position - freeForearm.position;
            if (upper.sqrMagnitude < .000001f || forearm.sqrMagnitude < .000001f) return;

            // First remove the visible elbow kink so shoulder, elbow and wrist form one clean line.
            Transform elbowParent = freeForearm.parent;
            Vector3 upperSpace = elbowParent != null ? elbowParent.InverseTransformVector(upper) : upper;
            Vector3 forearmSpace = elbowParent != null ? elbowParent.InverseTransformVector(forearm) : forearm;
            freeForearm.localRotation = Quaternion.FromToRotation(forearmSpace, upperSpace) * freeForearm.localRotation;

            // Then aim the complete straight arm along the user's red line.
            Vector3 current = freeHand.position - freeUpperArm.position;
            Vector3 desired = (-transform.up + transform.right * Mathf.Tan(10f * Mathf.Deg2Rad)).normalized;
            if (current.sqrMagnitude < .000001f) return;
            freeUpperArm.rotation = Quaternion.FromToRotation(current.normalized, desired) * freeUpperArm.rotation;
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

