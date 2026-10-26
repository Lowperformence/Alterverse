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
            public int[] mouthVertices;
            public int[] clothingVertices;
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
        private float freeArmSide;
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
            if (data.mouthVertices != null)
                foreach (int index in data.mouthVertices)
                    if (index < 0 || index >= data.boneIndices.Length) return false;
            if (data.clothingVertices != null)
            {
                int previous = -1;
                foreach (int index in data.clothingVertices)
                {
                    if (index <= previous || index >= data.boneIndices.Length || data.groups[index] != 0) return false;
                    previous = index;
                }
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
            InsetShoulderJoints(model, bones, lookup, heads);
            freeArmSide = SideFromTorso(heads[lookup["UpperArmL"]], heads[lookup["Chest"]]);
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
                if(data.groups[i]==0)FitSilhouette(data,b,lookup,rotations[b],ref sourcePoint,ref sourceNormal);
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
                Surface("Black shark eyes and nostrils", new Color(.003f,.004f,.006f), 0f,.82f),
                SharkSkin(),
                Surface("Natural shark mouth and gums", new Color(.10f,.028f,.032f), 0f,.32f),
                Surface("Subtle bronze shark iris", new Color(.12f,.068f,.027f), 0f,.70f),
                Surface("Ivory shark teeth", new Color(.87f,.865f,.79f), 0f,.46f)
            };
            if (data.boneWeights2 != null) anatomy.quality = SkinQuality.Bone2;
            RendererGroup(anatomy, 0, anatomy.transform, vertices, normals, weights, bones, materials);
            BuildRoundedShoulders(model,anatomy,bones,lookup,heads,rotations,scales,materials[1]);
            BuildLeatherShoulders(model,anatomy,bones,lookup,heads,rotations,scales);
            BuildLeatherCuffs(model,anatomy,bones,lookup,heads,rotations,scales);
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
            BuildAuthoredArmor(model, anatomy, bones, lookup, heads, rotations, scales);
            return true;
        }

        // Separate additive armor only: keep the original anatomy and both weapons untouched.
        private void BuildAuthoredArmor(Transform model, SkinnedMeshRenderer anatomy, Transform[] bones,
            Dictionary<string, int> lookup, Vector3[] heads, Quaternion[] rotations, float[] scales)
        {
            TextAsset asset = Resources.Load<TextAsset>("CyberShark/Armor20261012");
            if (asset == null) return;
            Geometry armor;
            try
            {
                using (var stream = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress))
                using (var reader = new StreamReader(stream)) armor = JsonUtility.FromJson<Geometry>(reader.ReadToEnd());
            }
            catch (Exception error) { Debug.LogWarning("DF: Nerok armor: " + error.Message, this); return; }
            if (!Valid(armor)) { Debug.LogWarning("DF: Nerok armor data is invalid.", this); return; }
            int count = armor.vertices.Length / 3;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var weights = new BoneWeight[count];
            Matrix4x4 toMesh = anatomy.transform.worldToLocalMatrix * model.localToWorldMatrix;
            Matrix4x4 toNormal = toMesh.inverse.transpose;
            // The exporter uses the established coordinate contract, including matching bone order.
            for (int i = 0; i < armor.boneNames.Length; i++)
                if (i >= geometry.boneNames.Length || armor.boneNames[i] != geometry.boneNames[i])
                { Debug.LogWarning("DF: Nerok armor bone contract mismatch.", this); return; }
            for (int i = 0; i < count; i++)
            {
                int source = armor.boneIndices[i], b = lookup[armor.boneNames[source]];
                Vector3 p = Point(armor.vertices, i * 3), n = Point(armor.normals, i * 3);
                FitSilhouette(armor, source, lookup, rotations[b], ref p, ref n);
                p = heads[b] + rotations[b] * (p - Point(armor.anchors[source].head)) * scales[b];
                vertices[i] = toMesh.MultiplyPoint3x4(p);
                normals[i] = toNormal.MultiplyVector(rotations[b] * n).normalized;
                weights[i] = new BoneWeight { boneIndex0 = b, weight0 = 1f };
            }
            var mesh = new Mesh { name = "DF_Nerok_AuthoredArmor20261012",
                indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            ownedMeshes.Add(mesh); mesh.vertices = vertices; mesh.normals = normals; mesh.boneWeights = weights;
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * anatomy.transform.localToWorldMatrix;
            mesh.bindposes = bindposes; mesh.subMeshCount = armor.submeshes.Length;
            for (int i = 0; i < armor.submeshes.Length; i++) mesh.SetTriangles(armor.submeshes[i].triangles, i, false);
            mesh.RecalculateBounds();
            var go = new GameObject("DF_Nerok_AuthoredArmor20261012"); go.layer = anatomy.gameObject.layer;
            go.transform.SetParent(anatomy.transform, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
            renderer.bones = bones; renderer.rootBone = anatomy.rootBone; renderer.quality = SkinQuality.Bone1;
            renderer.updateWhenOffscreen = anatomy.updateWhenOffscreen;
            renderer.sharedMaterials = new[] {
                Surface("Authored armor forged naval steel", new Color(.06f,.085f,.10f), .8f,.66f),
                Surface("Authored armor saltwater ceramic", new Color(.22f,.38f,.38f), .35f,.60f),
                Surface("Authored armor brushed copper", new Color(.49f,.23f,.105f), .75f,.68f),
                Surface("Authored armor brown leather straps", new Color(.11f,.047f,.025f), 0f,.24f),
                Surface("Authored armor pale rivet metal", new Color(.56f,.60f,.56f), .7f,.66f),
                Surface("Authored armor deep crimson braid", new Color(.27f,.032f,.026f), 0f,.25f)
            };
            Bounds bounds = mesh.bounds; bounds.Expand(bounds.size * .45f); renderer.localBounds = bounds;
        }

        private static void InsetShoulderJoints(Transform model, Transform[] bones,
            Dictionary<string, int> lookup, Vector3[] heads)
        {
            // Brakk's broad rig leaves Nerok's slimmer deltoids outside his jacket.
            // Move each complete arm inward by one fitted shoulder radius, before
            // skin bindposes, weapon contacts and the combat rest pose are captured.
            foreach (string side in new[] { "L", "R" })
            {
                int upper = lookup["UpperArm" + side], elbow = lookup["Forearm" + side];
                float sourceLength = Vector3.Distance(Point(geometry.anchors[upper].head),
                    Point(geometry.anchors[elbow].head));
                float armScale = Vector3.Distance(heads[upper], heads[elbow]) / sourceLength;
                Vector3 position = heads[upper];
                position.x -= SideFromTorso(position, heads[lookup["Chest"]]) * (.109f * .74f * armScale);
                bones[upper].position = model.TransformPoint(position);
            }
            // Children inherit the inset, including elbows, palms and both weapon grips.
            for (int i = 0; i < heads.Length; i++) heads[i] = model.InverseTransformPoint(bones[i].position);
        }

        // FBX root transforms can reverse model-space X relative to the L/R bone names.
        private static float SideFromTorso(Vector3 shoulder, Vector3 chest) => shoulder.x < chest.x ? -1f : 1f;

        // Deform mesh coordinates at build time. Joint positions and animation lengths stay intact.
        private static void FitSilhouette(Geometry data,int bone,Dictionary<string,int> lookup,
            Quaternion targetRotation,ref Vector3 point,ref Vector3 normal)
        {
            string name=data.boneNames[bone];
            if(name=="Head")
            {
                Vector3 pivot=Point(data.anchors[bone].head);
                point=pivot+Vector3.Scale(point-pivot,new Vector3(.70f,.74f,.84f));
                normal=new Vector3(normal.x/.70f,normal.y/.74f,normal.z/.84f).normalized;
                return;
            }
            if(!name.StartsWith("UpperArm",StringComparison.Ordinal)&&!name.StartsWith("Forearm",StringComparison.Ordinal))return;
            string side=name.Substring(name.Length-1);
            Vector3 a=Point(data.anchors[lookup["UpperArm"+side]].head);
            Vector3 e=Point(data.anchors[lookup["Forearm"+side]].head);
            Vector3 w=Point(data.anchors[lookup["Hand"+side]].head);
            Vector3 upper=ClosestOnSegment(point,a,e),lower=ClosestOnSegment(point,e,w);
            bool above=(point-upper).sqrMagnitude<(point-lower).sqrMagnitude;
            Vector3 center=above?upper:lower,axis=(above?e-a:w-e).normalized;
            point=center+(point-center)*.74f;
            Vector3 axial=axis*Vector3.Dot(normal,axis);
            normal=(axial+(normal-axial)/.74f).normalized;
            if(name.StartsWith("UpperArm",StringComparison.Ordinal))
            {
                // Lower the visible shoulder, fading to the unchanged elbow half way down the arm.
                Vector3 down=(e-a).normalized;
                float length=(e-a).magnitude;
                float t=Vector3.Dot(point-a,down)/length;
                float blend=Mathf.Clamp01(t/.5f);
                // Lower in the visible model's down direction, including mirrored FBX rigs.
                Vector3 displacement=Quaternion.Inverse(targetRotation)*Vector3.down*.065f;
                point+=displacement*(1f-Mathf.SmoothStep(0f,1f,blend));
                float slope=t>0f&&t<.5f ? -6f*blend*(1f-blend)/.5f : 0f;
                Vector3 gradient=down*(slope/length);
                normal=(normal-gradient*(Vector3.Dot(displacement,normal)/
                    (1f+Vector3.Dot(gradient,displacement)))).normalized;
            }
        }

        private static Vector3 ClosestOnSegment(Vector3 point,Vector3 a,Vector3 b)
        {
            Vector3 axis=b-a;
            return a+axis*Mathf.Clamp01(Vector3.Dot(point-a,axis)/Mathf.Max(axis.sqrMagnitude,.000001f));
        }

        private static bool LegacyShoulderCap(int a,int b,int c,int material)
        {
            if(material!=1)return false;
            int bone=geometry.boneIndices[a];
            if(!geometry.boneNames[bone].StartsWith("UpperArm",StringComparison.Ordinal)||
                geometry.boneIndices[b]!=bone||geometry.boneIndices[c]!=bone)return false;
            Vector3 center=Point(geometry.anchors[bone].head);
            // Only the source arm's flat closing fan has a vertex at the shoulder joint.
            return (Point(geometry.vertices,a*3)-center).sqrMagnitude<1e-10f||
                (Point(geometry.vertices,b*3)-center).sqrMagnitude<1e-10f||
                (Point(geometry.vertices,c*3)-center).sqrMagnitude<1e-10f;
        }

        private void BuildRoundedShoulders(Transform model,SkinnedMeshRenderer anatomy,Transform[] bones,
            Dictionary<string,int> lookup,Vector3[] heads,Quaternion[] rotations,float[] scales,Material skin)
        {
            var points=new List<Vector3>();var weights=new List<BoneWeight>();var faces=new List<int>();
            Matrix4x4 convert=anatomy.transform.worldToLocalMatrix*model.localToWorldMatrix;
            foreach(string side in new[]{"L","R"})
            {
                int bone=lookup["UpperArm"+side];Vector3 a=Point(geometry.anchors[bone].head);
                Vector3 axis=(Point(geometry.anchors[lookup["Forearm"+side]].head)-a).normalized;
                Vector3 across=Vector3.Cross(axis,Vector3.forward).normalized,front=Vector3.Cross(axis,across).normalized;
                var indices=new HashSet<int>();int[] source=geometry.submeshes[1].triangles;
                for(int i=0;i<source.Length;i+=3)
                {
                    if(geometry.boneIndices[source[i]]!=bone||!LegacyShoulderCap(source[i],source[i+1],source[i+2],1))continue;
                    for(int j=0;j<3;j++)
                        if((Point(geometry.vertices,source[i+j]*3)-a).sqrMagnitude>1e-10f)indices.Add(source[i+j]);
                }
                if(indices.Count<8)continue;
                var rim=new List<Vector3>();foreach(int index in indices)rim.Add(Point(geometry.vertices,index*3));
                rim.Sort((p,q)=>Mathf.Atan2(Vector3.Dot(p-a,front),Vector3.Dot(p-a,across)).CompareTo(
                    Mathf.Atan2(Vector3.Dot(q-a,front),Vector3.Dot(q-a,across))));
                int Add(Vector3 p)
                {
                    Vector3 n=(p-a).normalized;FitSilhouette(geometry,bone,lookup,rotations[bone],ref p,ref n);
                    points.Add(convert.MultiplyPoint3x4(heads[bone]+rotations[bone]*(p-a)*scales[bone]));
                    weights.Add(new BoneWeight{boneIndex0=bone,weight0=1f});return points.Count-1;
                }
                void Tri(int p,int q,int r,Vector3 expected)
                {
                    Vector3 target=convert.MultiplyVector(rotations[bone]*expected);
                    if(Vector3.Dot(Vector3.Cross(points[q]-points[p],points[r]-points[p]),target)<0f){int temp=q;q=r;r=temp;}
                    faces.Add(p);faces.Add(q);faces.Add(r);
                }
                const int rows=6;int pole=Add(a-axis*.074f);var rings=new int[rows,rim.Count];
                for(int row=0;row<rows;row++)
                {
                    float angle=(row+1f)/rows*Mathf.PI*.5f;
                    for(int i=0;i<rim.Count;i++)
                    {
                        // Use the actual source rim so the round dome meets the original arm exactly.
                        Vector3 p=row==rows-1?rim[i]:a+(rim[i]-a)*Mathf.Sin(angle)-axis*(.074f*Mathf.Cos(angle));
                        rings[row,i]=Add(p);
                    }
                }
                for(int i=0;i<rim.Count;i++)
                {
                    int next=(i+1)%rim.Count;
                    Tri(pole,rings[0,i],rings[0,next],-axis);
                    for(int row=0;row<rows-1;row++)
                    {
                        Vector3 expected=(rim[i]+rim[next])*.5f-a;
                        Tri(rings[row,i],rings[row+1,i],rings[row+1,next],expected);
                        Tri(rings[row,i],rings[row+1,next],rings[row,next],expected);
                    }
                }
            }
            if(points.Count==0)return;
            var mesh=new Mesh{name="DF_Nerok_RoundShoulders"};ownedMeshes.Add(mesh);
            mesh.SetVertices(points);mesh.SetTriangles(faces,0);mesh.boneWeights=weights.ToArray();
            var bindposes=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++)bindposes[i]=bones[i].worldToLocalMatrix*anatomy.transform.localToWorldMatrix;
            mesh.bindposes=bindposes;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(mesh.name);go.layer=anatomy.gameObject.layer;go.transform.SetParent(anatomy.transform,false);
            var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=bones;
            renderer.rootBone=anatomy.rootBone;renderer.quality=SkinQuality.Bone1;renderer.sharedMaterial=skin;
            Bounds bounds=mesh.bounds;bounds.Expand(bounds.size*.45f);renderer.localBounds=bounds;
        }

        private void BuildLeatherShoulders(Transform model, SkinnedMeshRenderer anatomy, Transform[] bones,
            Dictionary<string, int> lookup, Vector3[] heads, Quaternion[] rotations, float[] scales)
        {
            var points = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var faces = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            var shared = new Dictionary<(int, int, Vector3Int), int>();
            Matrix4x4 convert = anatomy.transform.worldToLocalMatrix * model.localToWorldMatrix;
            Matrix4x4 toNormal = convert.inverse.transpose;
            foreach (string side in new[] { "L", "R" })
            {
                int bone = lookup["UpperArm" + side];
                Vector3 a = Point(geometry.anchors[bone].head);
                Vector3 axis = Point(geometry.anchors[lookup["Forearm" + side]].head) - a;
                float length = axis.magnitude;
                axis /= length;
                Vector3 targetAxis = (heads[lookup["Forearm" + side]] - heads[bone]).normalized;
                Vector3 targetFront = Vector3.ProjectOnPlane(Vector3.forward, targetAxis).normalized;
                float rigSide = SideFromTorso(heads[bone], heads[lookup["Chest"]]);
                Quaternion inverseFit = Quaternion.Inverse(rotations[bone]);
                // Resolve front and outside on the actual rig, then express them in
                // source space for the shared skin fitting and shoulder dome.
                Vector3 front = inverseFit * targetFront;
                Vector3 outward = inverseFit * (Vector3.Cross(targetFront, targetAxis).normalized * rigSide);
                Vector3 Radial(float angle) => outward * Mathf.Cos(angle) + front * Mathf.Sin(angle);
                Vector3 ShellPoint(Vector2 uv, float radius)
                {
                    float along = uv.y * length;
                    // The upper plate bends over the round deltoid rather than ending
                    // as a narrow patch on its side. Leave the very top of the crown open.
                    float dome = along < 0f ? Mathf.Sqrt(Mathf.Max(.08f, 1f - along * along / (.083f * .083f))) : 1f;
                    return a + axis * along + Radial(uv.x) * ((radius - .018f * Mathf.Clamp01(uv.y)) * dome);
                }
                Vector3 ShellNormal(Vector2 uv, float radius)
                {
                    Vector3 across = ShellPoint(uv + new Vector2(.001f, 0f), radius) -
                        ShellPoint(uv - new Vector2(.001f, 0f), radius);
                    Vector3 down = ShellPoint(uv + new Vector2(0f, .001f), radius) -
                        ShellPoint(uv - new Vector2(0f, .001f), radius);
                    Vector3 normal = Vector3.Cross(across, down).normalized;
                    return Vector3.Dot(normal, Radial(uv.x)) < 0f ? -normal : normal;
                }
                void Tri(Vector3 p, Vector3 q, Vector3 r, int surface, Vector3 expected)
                {
                    if (Vector3.Dot(Vector3.Cross(q - p, r - p), expected) < 0f)
                    { Vector3 temp = q; q = r; r = temp; }
                    int first = faces[surface].Count;
                    Vector3 facing = Vector3.zero;
                    foreach (Vector3 source in new[] { p, q, r })
                    {
                        Vector3 fitted = source, normal = expected;
                        FitSilhouette(geometry, bone, lookup, rotations[bone], ref fitted, ref normal);
                        facing += toNormal.MultiplyVector(rotations[bone] * normal).normalized;
                        var key = (bone, surface, new Vector3Int(Mathf.RoundToInt(fitted.x * 1000000f),
                            Mathf.RoundToInt(fitted.y * 1000000f), Mathf.RoundToInt(fitted.z * 1000000f)));
                        if (!shared.TryGetValue(key, out int index))
                        {
                            index = points.Count;
                            shared.Add(key, index);
                            points.Add(convert.MultiplyPoint3x4(heads[bone] + rotations[bone] * (fitted - a) * scales[bone]));
                            weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
                        }
                        faces[surface].Add(index);
                    }
                    int i = faces[surface][first], j = faces[surface][first + 1], k = faces[surface][first + 2];
                    if (Vector3.Dot(Vector3.Cross(points[j] - points[i], points[k] - points[i]), facing) < 0f)
                    { faces[surface][first + 1] = k; faces[surface][first + 2] = j; }
                }
                void Tube(Vector3 p, Vector3 q, float radius, int surface)
                {
                    Vector3 direction = (q - p).normalized;
                    Vector3 across = Vector3.Cross(direction, Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.right).normalized;
                    Vector3 other = Vector3.Cross(direction, across).normalized;
                    const int sides = 4;
                    Vector3 Ring(Vector3 center, int index)
                    {
                        float angle = index * Mathf.PI * 2f / sides;
                        return center + (across * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * radius;
                    }
                    for (int i = 0; i < sides; i++)
                    {
                        Vector3 x = Ring(p, i), y = Ring(p, i + 1), z = Ring(q, i + 1), w = Ring(q, i);
                        Vector3 normal = (x + y) * .5f - p;
                        Tri(x, y, z, surface, normal); Tri(x, z, w, surface, normal);
                        Tri(p, y, x, surface, -direction); Tri(q, w, z, surface, direction);
                    }
                }
                void Panel(Vector2[] outline, float radius, bool main)
                {
                    Vector2 center = Vector2.zero;
                    foreach (Vector2 p in outline) center += p;
                    center /= outline.Length;
                    float halfWidth = .01f, halfHeight = .01f;
                    foreach (Vector2 p in outline)
                    {
                        halfWidth = Mathf.Max(halfWidth, Mathf.Abs(p.x - center.x));
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(p.y - center.y));
                    }
                    Vector3 Sample(Vector2 uv, bool underside = false)
                    {
                        float edge = new Vector2((uv.x - center.x) / halfWidth, (uv.y - center.y) / halfHeight).magnitude;
                        float bulge = .007f * (1f - Mathf.Clamp01(edge));
                        return ShellPoint(uv, radius + bulge - (underside ? .012f : 0f));
                    }
                    const int steps = 3;
                    for (int i = 0; i < outline.Length; i++)
                    {
                        Vector2 p = outline[i], q = outline[(i + 1) % outline.Length];
                        Vector2 Param(int row, int column) => center + (p - center) * ((row - column) / (float)steps) +
                            (q - center) * (column / (float)steps);
                        for (int row = 0; row < steps; row++) for (int column = 0; column <= row; column++)
                        {
                            Vector2 u = Param(row, column), v = Param(row + 1, column), w = Param(row + 1, column + 1);
                            Vector3 normal = ShellNormal((u + v + w) / 3f, radius);
                            Tri(Sample(u), Sample(v), Sample(w), 0, normal);
                            Tri(Sample(u, true), Sample(w, true), Sample(v, true), 1, -normal);
                            if (column < row)
                            {
                                Vector2 x = Param(row, column + 1);
                                normal = ShellNormal((u + w + x) / 3f, radius);
                                Tri(Sample(u), Sample(w), Sample(x), 0, normal);
                                Tri(Sample(u, true), Sample(x, true), Sample(w, true), 1, -normal);
                            }
                        }
                        for (int segment = 0; segment < steps; segment++)
                        {
                            Vector2 u = Vector2.Lerp(p, q, segment / (float)steps);
                            Vector2 v = Vector2.Lerp(p, q, (segment + 1f) / steps), mid = (u + v) * .5f;
                            Vector3 tangent = -outward * Mathf.Sin(mid.x) + front * Mathf.Cos(mid.x);
                            Vector3 edge = tangent * (mid.x - center.x) + axis * (mid.y - center.y);
                            Vector3 op = Sample(u), oq = Sample(v), ip = Sample(u, true), iq = Sample(v, true);
                            Tri(op, ip, iq, 1, edge); Tri(op, iq, oq, 1, edge);
                            Vector3 normal = ShellNormal(mid, radius);
                            Tube(op + normal * .0005f, oq + normal * .0005f, .003f, 2);
                        }
                        // Real raised stitches follow the curved plate inset from its tan welt.
                        Vector2 stitchA = Vector2.Lerp(center, p, .88f), stitchB = Vector2.Lerp(center, q, .88f);
                        int stitches = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(Sample(stitchA), Sample(stitchB)) / .014f));
                        for (int stitch = 0; stitch < stitches; stitch++)
                        {
                            Vector2 u = Vector2.Lerp(stitchA, stitchB, (stitch + .20f) / stitches);
                            Vector2 v = Vector2.Lerp(stitchA, stitchB, (stitch + .72f) / stitches);
                            Tube(Sample(u) + ShellNormal(u, radius) * .0012f,
                                Sample(v) + ShellNormal(v, radius) * .0012f, .0013f, 4);
                        }
                    }
                    void Rivet(Vector2 uv)
                    {
                        const int segments = 8;
                        Vector3 normal = ShellNormal(uv, radius), centerPoint = Sample(uv) - normal * .0005f;
                        Vector3 across = Vector3.ProjectOnPlane(axis, normal).normalized;
                        Vector3 other = Vector3.Cross(normal, across).normalized;
                        Vector3 Ring(int index, float depth)
                        {
                            float angle = index * Mathf.PI * 2f / segments;
                            return centerPoint + normal * depth + (across * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * .006f;
                        }
                        for (int i = 0; i < segments; i++)
                        {
                            Vector3 p = Ring(i, 0f), q = Ring(i + 1, 0f), r = Ring(i + 1, .0035f), s = Ring(i, .0035f);
                            Vector3 edge = (p + q) * .5f - centerPoint;
                            Tri(p, q, r, 3, edge); Tri(p, r, s, 3, edge);
                            Tri(centerPoint, q, p, 3, -normal);
                            Tri(centerPoint + normal * .0035f, s, r, 3, normal);
                        }
                    }
                    foreach (float angle in main ? new[] { -1.08f, 1.35f } : new[] { -.96f, 1.12f })
                        Rivet(new Vector2(angle, main ? .14f : center.y + .025f));
                }
                // Broad saddle-shaped cap with rounded corners and a pointed outer hem,
                // followed by three overlapping leather lames, like the reference armor.
                Panel(new[] {
                    new Vector2(-1.50f,-.035f),new Vector2(-1.36f,-.12f),new Vector2(-.91f,-.19f),
                    new Vector2(-.40f,-.22f),new Vector2(.18f,-.225f),new Vector2(.76f,-.19f),
                    new Vector2(1.33f,-.12f),new Vector2(1.73f,-.025f),new Vector2(1.80f,.10f),
                    new Vector2(1.65f,.215f),new Vector2(1.08f,.245f),new Vector2(.53f,.295f),
                    new Vector2(.05f,.32f),new Vector2(-.53f,.295f),new Vector2(-1.15f,.25f),
                    new Vector2(-1.45f,.15f)
                }, .140f, true);
                for (int layer = 0; layer < 3; layer++)
                {
                    float middle = .335f + layer * .12f, width = 1.55f - layer * .08f;
                    Panel(new[] {
                        new Vector2(-width,middle-.035f),new Vector2(-width+.12f,middle-.09f),
                        new Vector2(-width+.4f,middle-.11f),new Vector2(width-.4f,middle-.11f),
                        new Vector2(width-.12f,middle-.09f),new Vector2(width,middle-.035f),
                        new Vector2(width,middle+.035f),new Vector2(width-.12f,middle+.085f),
                        new Vector2(width-.4f,middle+.10f),new Vector2(.35f,middle+.12f),
                        new Vector2(0f,middle+.13f),new Vector2(-.35f,middle+.12f),
                        new Vector2(-width+.4f,middle+.10f),new Vector2(-width+.12f,middle+.085f),
                        new Vector2(-width,middle+.035f)
                    }, .137f - layer * .003f, false);
                }
            }
            var mesh = new Mesh { name = "DF_Nerok_LayeredLeatherPauldrons",
                indexFormat = points.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            ownedMeshes.Add(mesh);
            mesh.SetVertices(points); mesh.subMeshCount = faces.Length;
            for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i);
            mesh.boneWeights = weights.ToArray();
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * anatomy.transform.localToWorldMatrix;
            mesh.bindposes = bindposes; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(mesh.name);
            go.layer = anatomy.gameObject.layer; go.transform.SetParent(anatomy.transform, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.bones = bones; renderer.rootBone = anatomy.rootBone;
            renderer.quality = SkinQuality.Bone1;
            renderer.sharedMaterials = new[] {
                Surface("Layered saddle-brown shoulder leather",new Color(.36f,.18f,.085f),0f,.23f),
                Surface("Thick shoulder leather cut edges",new Color(.15f,.065f,.025f),0f,.12f),
                Surface("Tan shoulder leather welt",new Color(.61f,.365f,.17f),0f,.18f),
                Surface("Shoulder brass rivets",new Color(.52f,.31f,.10f),.72f,.32f),
                Surface("Shoulder flax stitching",new Color(.86f,.68f,.40f),0f,.10f)
            };
            Bounds bounds = mesh.bounds; bounds.Expand(bounds.size * .45f); renderer.localBounds = bounds;
        }

        private static bool LegacyShoulderAccessory(int index,int material)
        {
            if (AuthoredClothingVertex(index)) return false;
            int bone=geometry.boneIndices[index];string name=geometry.boneNames[bone];
            if(!name.StartsWith("UpperArm",StringComparison.Ordinal))return false;
            // The source upper-arm metal slots contain only the old pauldron, fin and brass teeth.
            if(material==2||material==4)return true;
            if(material!=1)return false;
            Vector3 a=Point(geometry.anchors[bone].head),p=Point(geometry.vertices,index*3);
            float sign=name.EndsWith("L",StringComparison.Ordinal)?-1f:1f;
            for(int j=0;j<3;j++)
            {
                Vector3 start=new Vector3(a.x+sign*j*.035f,a.y+.025f,.11f);
                Vector3 end=new Vector3(a.x+sign*(.015f+j*.035f),a.y-.065f,.104f);
                if(Mathf.Abs(Vector3.Distance(p,ClosestOnSegment(p,start,end))-.006f)<.00015f)return true;
            }
            return false;
        }

        private void BuildLeatherCuffs(Transform model,SkinnedMeshRenderer anatomy,Transform[] bones,
            Dictionary<string,int> lookup,Vector3[] heads,Quaternion[] rotations,float[] scales)
        {
            var points=new List<Vector3>();var triangles=new List<int>();var weights=new List<BoneWeight>();
            var uv=new List<Vector2>();
            Matrix4x4 convert=anatomy.transform.worldToLocalMatrix*model.localToWorldMatrix;
            const int sides=32;
            // Four beveled outer rings and two inner rings form a closed, hollow leather wrap.
            float[] along={.34f,.38f,.69f,.73f,.73f,.34f};
            float[] radius={.096f,.097f,.092f,.089f,.087f,.094f};
            foreach(string side in new[]{"L","R"})
            {
                int bone=lookup["Forearm"+side];
                Vector3 e=Point(geometry.anchors[bone].head),w=Point(geometry.anchors[lookup["Hand"+side]].head);
                Vector3 axis=(w-e).normalized,across=Vector3.Cross(axis,Vector3.forward).normalized;
                Vector3 outward=Vector3.Cross(axis,across).normalized;
                int start=points.Count;
                for(int row=0;row<along.Length;row++)for(int s=0;s<=sides;s++)
                {
                    float angle=s*Mathf.PI*2f/sides;
                    float gap=row<4?.005f:0f;
                    Vector3 radial=across*Mathf.Cos(angle)+outward*(Mathf.Sin(angle)*.94f);
                    Vector3 source=e+(w-e)*along[row]+radial*(radius[row]*.74f+gap);
                    Vector3 p=heads[bone]+rotations[bone]*(source-e)*scales[bone];
                    points.Add(convert.MultiplyPoint3x4(p));
                    weights.Add(new BoneWeight{boneIndex0=bone,weight0=1f});
                    uv.Add(new Vector2((float)s/sides,along[row]));
                }
                for(int row=0;row<along.Length;row++)for(int s=0;s<sides;s++)
                {
                    int a=start+row*(sides+1)+s,b=a+1;
                    int c=start+((row+1)%along.Length)*(sides+1)+s,d=c+1;
                    triangles.Add(a);triangles.Add(b);triangles.Add(c);
                    triangles.Add(b);triangles.Add(d);triangles.Add(c);
                }
            }
            var mesh=new Mesh{name="DF_Nerok_LeatherForearmWraps"};ownedMeshes.Add(mesh);
            mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();
            var bindposes=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++)bindposes[i]=bones[i].worldToLocalMatrix*anatomy.transform.localToWorldMatrix;
            mesh.bindposes=bindposes;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("DF_Nerok_LeatherForearmWraps");go.layer=anatomy.gameObject.layer;
            go.transform.SetParent(anatomy.transform,false);
            var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=bones;
            renderer.rootBone=anatomy.rootBone;renderer.quality=SkinQuality.Bone1;
            renderer.sharedMaterial=Surface("Worn brown forearm leather",new Color(.19f,.085f,.035f),0f,.16f);
            Bounds bounds=mesh.bounds;bounds.Expand(bounds.size*.45f);renderer.localBounds=bounds;
        }

        private static bool LegacyForearmAccessory(int index,int material)
        {
            if (AuthoredClothingVertex(index)) return false;
            string bone=geometry.boneNames[geometry.boneIndices[index]];
            if(!bone.StartsWith("Forearm",StringComparison.Ordinal))return false;
            if(material!=1)return true;
            int hand=Array.IndexOf(geometry.boneNames,"Hand"+bone[bone.Length-1]);
            Vector3 w=Point(geometry.anchors[hand].head);
            return PlateVertex(Point(geometry.vertices,index*3),new[]{
                new Vector2(w.x-.033f,w.y+.066f),new Vector2(w.x+.033f,w.y+.066f),
                new Vector2(w.x+.033f,w.y+.021f),new Vector2(w.x-.033f,w.y+.021f)},.098f,.018f);
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

        private Material SharkSkin()
        {
            Shader shader = Resources.Load<Shader>("CyberShark/SharkSkin");
            if (shader == null || !shader.isSupported)
                return Surface("Slate grey shark head", new Color(.22f,.255f,.28f), 0f,.46f);
            var material = new Material(shader) { name = "DF_Nerok_DetailedSharkHead" };
            ownedMaterials.Add(material);
            return material;
        }

        // Garments share authored slot 2 with armor. Explicit sorted source indices
        // identify the new outfit; the plate fallback supports older model resources.
        private static bool AuthoredClothingVertex(int index)
        {
            return geometry.clothingVertices != null &&
                Array.BinarySearch(geometry.clothingVertices, index) >= 0;
        }

        private static bool JacketVertex(int index)
        {
            if (AuthoredClothingVertex(index)) return true;
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
            var v = new List<Vector3>(); var rest = new List<Vector3>(); var n = new List<Vector3>(); var w = new List<BoneWeight>();
            Matrix4x4 convert = renderer.transform.worldToLocalMatrix * source.localToWorldMatrix;
            Matrix4x4 normalMatrix = convert.inverse.transpose;
            for (int i = 0; i < vertices.Length; i++)
            {
                remap[i] = -1;
                if (geometry.groups[i] != group) continue;
                remap[i] = v.Count;
                v.Add(convert.MultiplyPoint3x4(vertices[i]));
                rest.Add(geometry.boneNames[geometry.boneIndices[i]] == "Head"
                    ? Point(geometry.vertices, i * 3) : v[v.Count - 1]);
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
            var mesh = new Mesh { name = "DF_Nerok_Siege_" + group,
                indexFormat = v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            ownedMeshes.Add(mesh);
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(2, group == 0 ? rest : v); mesh.boneWeights = w.ToArray();
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            mesh.bindposes = bindposes; mesh.subMeshCount = materials.Length;
            var materialTriangles = new List<int>[materials.Length];
            for (int m = 0; m < materials.Length; m++) materialTriangles[m] = new List<int>();
            var mouth = new HashSet<int>(geometry.mouthVertices ?? Array.Empty<int>());
            for (int m = 0; m < geometry.submeshes.Length; m++)
            {
                int[] sourceIndices = geometry.submeshes[m].triangles;
                for (int i = 0; i < sourceIndices.Length; i += 3)
                {
                    int a = remap[sourceIndices[i]], b = remap[sourceIndices[i+1]], c = remap[sourceIndices[i+2]];
                    if (a < 0 || b < 0 || c < 0) continue;
                    if(group==0&&LegacyForearmAccessory(sourceIndices[i],m)&&
                        LegacyForearmAccessory(sourceIndices[i+1],m)&&LegacyForearmAccessory(sourceIndices[i+2],m))continue;
                    if(group==0&&LegacyShoulderAccessory(sourceIndices[i],m)&&
                        LegacyShoulderAccessory(sourceIndices[i+1],m)&&LegacyShoulderAccessory(sourceIndices[i+2],m))continue;
                    if(group==0&&LegacyShoulderCap(sourceIndices[i],sourceIndices[i+1],sourceIndices[i+2],m))continue;
                    // The original nearly black fingers disappear beside the grey palm.
                    // Give only hand-bound armor-slot triangles the same shark-skin finish.
                    int material = m;
                    if (group == 0 &&
                        geometry.boneNames[geometry.boneIndices[sourceIndices[i]]] == "Head" &&
                        geometry.boneNames[geometry.boneIndices[sourceIndices[i+1]]] == "Head" &&
                        geometry.boneNames[geometry.boneIndices[sourceIndices[i+2]]] == "Head")
                    {
                        if (m == 0) material = mouth.Contains(sourceIndices[i]) &&
                            mouth.Contains(sourceIndices[i+1]) && mouth.Contains(sourceIndices[i+2]) ? 9 : 7;
                        else if (m == 1) material = 8;
                        else if (m == 4) material = 10;
                        else if (m == 5) material = 11;
                    }
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
            // Keep the inset shoulder seated while the elbow and wrist hang beside the torso.
            // A slight outward tilt keeps the free arm from disappearing into the jacket.
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

            // Aim the straight arm down and slightly outward in the actual model frame.
            Vector3 current = freeHand.position - freeUpperArm.position;
            Vector3 desired = transform.TransformVector(new Vector3(
                freeArmSide * Mathf.Tan(12f * Mathf.Deg2Rad), -1f, 0f)).normalized;
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
