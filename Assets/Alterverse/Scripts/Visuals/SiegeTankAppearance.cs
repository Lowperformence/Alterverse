using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Kaelon's woodland armor and original weapons, fitted to her rig with half-length forearms.</summary>
    [DisallowMultipleComponent]
    public sealed class SiegeTankAppearance : MonoBehaviour
    {
        private const float ForearmLengthScale = .5f;
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
        private Transform calibratedHammerHand;
        private Quaternion hammerFrameInHand;
        private readonly ShieldGuardFrame shieldGuard = new ShieldGuardFrame();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => geometry = null;

        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<SiegeTankAppearance>() != null) return;
            if (geometry == null)
            {
                TextAsset asset = Resources.Load<TextAsset>("SiegeTank/Model");
                if (asset == null) { Debug.LogError("DF: Siege Tank model resource is missing."); return; }
                try
                {
                    using (var stream = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                    using (var reader = new StreamReader(stream)) geometry = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
                }
                catch (Exception error) { Debug.LogError("DF: Siege Tank resource: " + error.Message); return; }
            }
            if (!Valid(geometry)) { Debug.LogError("DF: Siege Tank geometry is invalid."); return; }
            SkinnedMeshRenderer anatomy = null;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "DF_Anatomy") { anatomy = renderer; break; }
            if (anatomy == null || RuntimeShaderLibrary.Lit == null) return;
            var appearance = model.gameObject.AddComponent<SiegeTankAppearance>();
            if (!appearance.Build(model, anatomy)) Destroy(appearance);
        }

        private static bool Valid(Geometry data)
        {
            if (data == null || data.version != 1 || data.vertices == null || data.normals == null ||
                data.vertices.Length % 3 != 0 || data.vertices.Length != data.normals.Length ||
                data.groups == null || data.groups.Length != data.vertices.Length / 3 || data.boneIndices == null || data.boneIndices.Length != data.vertices.Length / 3 ||
                data.boneNames == null || data.anchors == null || data.anchors.Length != data.boneNames.Length ||
                data.submeshes == null || data.submeshes.Length != 7) return false;
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
            if (left == null || right == null) return false;
            int[] map = new int[data.boneNames.Length];
            Vector3[] heads = new Vector3[map.Length];
            var lookup = new Dictionary<string, int>();
            for (int i = 0; i < map.Length; i++)
            {
                int index = Array.FindIndex(bones, b => b != null && b.name == data.boneNames[i]);
                if (index < 0) { Debug.LogError("DF: Siege Tank bone missing: " + data.boneNames[i], this); return false; }
                map[i] = index;
                heads[i] = model.InverseTransformPoint(bones[index].position);
                lookup.Add(data.boneNames[i], i);
            }
            float unitScale = Vector3.Distance(heads[lookup["Chest"]], heads[lookup["Head"]]) /
                Vector3.Distance(Point(data.anchors[lookup["Chest"]].head), Point(data.anchors[lookup["Head"]].head));
            if (unitScale < .01f || unitScale > 100f) return false;
            // Fit the shorter arm chain before mesh bind poses and animation rest poses
            // are captured. Hands and their grip children move together without scaling.
            ShortenForearms(all, bones, lookup);
            for (int i = 0; i < heads.Length; i++)
                heads[i] = model.InverseTransformPoint(bones[i].position);
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
                Vector3 offset = Point(data.vertices, i * 3) - Point(data.anchors[b].head);
                Vector3 normal = Point(data.normals, i * 3);
                if (data.groups[i] == 0 && (data.boneNames[b] == "ForearmL" || data.boneNames[b] == "ForearmR"))
                {
                    Vector3 axis = (Point(data.anchors[b].tail) - Point(data.anchors[b].head)).normalized;
                    Vector3 along = axis * Vector3.Dot(offset, axis);
                    // scales[b] contains the halved length. Compensate across the axis
                    // so the previous forearm thickness is retained, with correct normals.
                    offset = along + (offset - along) / ForearmLengthScale;
                    Vector3 axialNormal = axis * Vector3.Dot(normal, axis);
                    normal = axialNormal / ForearmLengthScale + normal - axialNormal;
                }
                Vector3 p = heads[b] + rotations[b] * offset * scales[b];
                vertices[i] = toMesh.MultiplyPoint3x4(p);
                normals[i] = toNormal.MultiplyVector(rotations[b] * normal).normalized;
                weights[i] = new BoneWeight { boneIndex0 = map[b], weight0 = 1f };
            }
            Material[] materials = {
                Surface("Joint seals", new Color(.03f,.035f,.045f), .22f,.30f),
                WoodlandArmor(),
                Surface("Bronze edges", new Color(.38f,.32f,.19f), .80f,.44f),
                Surface("Amber reactor", new Color(1f,.32f,.035f), .35f,.48f, true),
                Surface("Exposed steel", new Color(.32f,.34f,.36f), .85f,.47f),
                // Retain legacy material key for weapon-colorway exclusion; geometry now spells DF.
                Surface("Shield DR", new Color(1f,.92f,.025f), .12f,.30f, true, 2.5f),
                Surface("Yellow head stripe", new Color(1f,.85f,.015f), .1f,.45f, true)
            };
            RendererGroup(anatomy, 0, anatomy.transform, vertices, normals, weights, bones, materials);
            RendererGroup(left, 1, anatomy.transform, vertices, normals, weights, bones, materials);
            RendererGroup(right, 2, anatomy.transform, vertices, normals, weights, bones, materials);
            if (data.markers != null)
                foreach (Marker marker in data.markers)
                {
                    if (!lookup.TryGetValue(marker.bone, out int b) || marker.position == null || marker.position.Length != 3) continue;
                    Vector3 p = heads[b] + rotations[b] * (Point(marker.position) - Point(data.anchors[b].head)) * scales[b];
                    var anchor = new GameObject("DF_Tank" + marker.name).transform;
                    anchor.SetParent(bones[map[b]], false);
                    anchor.position = model.TransformPoint(p);
                    markers.Add(marker.name, anchor);
                    if (marker.name == "HammerTop")
                    {
                        Transform muzzle = Array.Find(all, t => t.name == "DF_Muzzle");
                        if (muzzle != null) muzzle.position = anchor.position;
                    }
                }
            return true;
        }

        private static void ShortenForearms(Transform[] parts, Transform[] bones, Dictionary<string, int> lookup)
        {
            foreach (string side in new[] { "L", "R" })
            {
                Transform forearm = bones[lookup["Forearm" + side]];
                Transform hand = bones[lookup["Hand" + side]];
                Transform grip = bones[lookup["WeaponGrip" + side]];
                FollowSocket(parts, "DF_PalmGrip" + side, hand);
                FollowSocket(parts, "DF_HandleContact" + side, grip);
                if (side == "R") FollowSocket(parts, "DF_Muzzle", grip);
                hand.position = Vector3.Lerp(forearm.position, hand.position, ForearmLengthScale);
            }
        }

        private static void FollowSocket(Transform[] parts, string name, Transform parent)
        {
            Transform socket = Array.Find(parts, part => part.name == name);
            if (socket != null && !socket.IsChildOf(parent)) socket.SetParent(parent, true);
        }

        private Material WoodlandArmor()
        {
            // User-supplied woodland reference, anchored in rest space on every armor face.
            Material material=RuntimeShaderLibrary.Create(Color.white,false,.28f,.25f);
            material.name="DF_Kaelon_WoodlandArmor";
            Shader shader=Resources.Load<Shader>("HeroArt/TankWoodlandCamo");
            if(shader!=null)material.shader=shader;
            Texture2D pattern=Resources.Load<Texture2D>("HeroArt/WoodlandCamo");
            if(pattern!=null)material.SetTexture("_BaseMap",pattern);
            material.SetTextureScale("_BaseMap",Vector2.one*.60f);
            ownedMaterials.Add(material);
            return material;
        }

        private Material Surface(string name, Color color, float metal, float smooth, bool energy = false, float emissionStrength = .75f)
        {
            Material material = RuntimeShaderLibrary.Create(color, false, metal, smooth, energy ? color * emissionStrength : Color.black);
            material.name = "DF_Kaelon_" + name;
            if (energy) material.EnableKeyword("_EMISSION");
            ownedMaterials.Add(material);
            return material;
        }

        private void RendererGroup(SkinnedMeshRenderer renderer, int group, Transform source,
            Vector3[] vertices, Vector3[] normals, BoneWeight[] weights, Transform[] bones, Material[] materials)
        {
            var remap = new int[vertices.Length];
            var v = new List<Vector3>(); var n = new List<Vector3>(); var w = new List<BoneWeight>();
            var rest = new List<Vector3>(); var restNormals = new List<Vector3>();
            Matrix4x4 convert = renderer.transform.worldToLocalMatrix * source.localToWorldMatrix;
            Matrix4x4 normalMatrix = convert.inverse.transpose;
            for (int i = 0; i < vertices.Length; i++)
            {
                remap[i] = -1;
                if (geometry.groups[i] != group) continue;
                remap[i] = v.Count;
                v.Add(convert.MultiplyPoint3x4(vertices[i]));
                n.Add(normalMatrix.MultiplyVector(normals[i]).normalized); w.Add(weights[i]);
                rest.Add(Point(geometry.vertices,i*3));
                restNormals.Add(Point(geometry.normals,i*3));
            }
            var mesh = new Mesh { name = "DF_Kaelon_Siege_" + group };
            ownedMeshes.Add(mesh);
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.boneWeights = w.ToArray();
            // Keep a continuous fallback UV map; the woodland shader uses rest-space
            // triplanar coordinates below for the side and top faces as well.
            var camoUV=new List<Vector2>(v.Count);
            for(int i=0;i<v.Count;i++)
            {
                Vector3 point=rest[i];
                camoUV.Add(new Vector2(point.x,point.y));
            }
            mesh.SetUVs(0,camoUV);
            // Continuous 3D rest coordinates avoid per-vertex projection switches and
            // stretched UV triangles. They stay attached through animation and hand scaling.
            mesh.SetUVs(2,rest);
            mesh.SetUVs(3,restNormals);
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            mesh.bindposes = bindposes; mesh.subMeshCount = 7;
            for (int m = 0; m < 7; m++)
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

        // Keep the head above and outside the right shoulder, as in the marked guard.
        // Shared with strike recovery so the weapon returns to this exact diagonal.
        public static Vector3 HammerGuardShaft(Transform actor) =>
            (actor.up * .90f + actor.right * .32f + actor.forward * .30f).normalized;

        /// <summary>Set the right-hand guard from a fixed weapon frame, before strike sampling.</summary>
        public void OrientHammerGuard(Transform actor, Transform hand)
        {
            if (actor == null || hand == null) return;
            if (calibratedHammerHand != hand)
            {
                if (!markers.TryGetValue("HammerGrip", out var grip) ||
                    !markers.TryGetValue("HammerTop", out var top) ||
                    !markers.TryGetValue("HammerFront", out var front)) return;
                Vector3 shaft = top.position - grip.position;
                Vector3 casingFront = front.position - top.position;
                if (shaft.sqrMagnitude < .000001f ||
                    Vector3.Cross(shaft, casingFront).sqrMagnitude < .000001f) return;
                // Calibrate once after hand sizing and grip fitting. Do not feed last frame's
                // corrected wrist back into another correction (the old late helper did that).
                hammerFrameInHand = Quaternion.Inverse(hand.rotation) *
                    Quaternion.LookRotation(casingFront, shaft);
                calibratedHammerHand = hand;
            }
            // The authored HammerFront marker is on the SIDE of the head: its striking
            // faces lie along local X. Tilt the shaft up, outward and slightly forward.
            // Hand and child grip move together, retaining the closed fingers' contact.
            Vector3 guardShaft = HammerGuardShaft(actor);
            Quaternion weaponFrame = Quaternion.LookRotation(Vector3.ProjectOnPlane(actor.right, guardShaft), guardShaft);
            hand.rotation = weaponFrame * Quaternion.Inverse(hammerFrameInHand);
        }

        public void AlignGuard(Transform model, Transform leftHand, Transform rightHand, float weight)
        {
            AlignHand(rightHand, "HammerGrip", "HammerTop", "HammerFront", true, model, weight);
            if (markers.TryGetValue("ShieldCenter", out var center) &&
                markers.TryGetValue("ShieldTop", out var top) && markers.TryGetValue("ShieldFront", out var front))
                shieldGuard.Orient(model, leftHand, center, top, front);
        }
        private void AlignHand(Transform hand, string center, string top, string front, bool hammer, Transform model, float weight)
        {
            if (hand == null || !markers.TryGetValue(center, out var c) ||
                !markers.TryGetValue(top, out var t) || !markers.TryGetValue(front, out var f)) return;
            Vector3 up = t.position - c.position;
            Vector3 forward = f.position - (hammer ? t.position : c.position);
            if (up.sqrMagnitude < .000001f || forward.sqrMagnitude < .000001f) return;
            Quaternion current = Quaternion.LookRotation(forward, up);
            Quaternion desired = Quaternion.LookRotation(model.forward, model.up);
            hand.rotation = Quaternion.Slerp(hand.rotation, desired * Quaternion.Inverse(current) * hand.rotation, weight);
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
