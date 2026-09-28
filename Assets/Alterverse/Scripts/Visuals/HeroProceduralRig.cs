using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Adds presentation-only elbow, wrist, hip, knee and ankle joints to the procedural hero art.
    /// Gameplay roots, colliders, movement speed and combat timing are never modified.
    /// </summary>
    [DefaultExecutionOrder(935)]
    public sealed class HeroProceduralRig : MonoBehaviour
    {
        private static readonly Dictionary<string, Mesh> SplitMeshCache = new();

        private Transform visualRoot;
        private Transform pelvisJoint, spineLowerJoint, spineUpperJoint, neckJoint;
        private Transform hipL, hipR, kneeL, kneeR, ankleL, ankleR;
        private Transform elbowL, elbowR, wristL, wristR;
        private Quaternion hipRestL, hipRestR, kneeRestL, kneeRestR, ankleRestL, ankleRestR;
        private Quaternion elbowRestL, elbowRestR, wristRestL, wristRestR;
        private Quaternion targetHipL = Quaternion.identity, targetHipR = Quaternion.identity;
        private Quaternion targetKneeL = Quaternion.identity, targetKneeR = Quaternion.identity;
        private Quaternion targetAnkleL = Quaternion.identity, targetAnkleR = Quaternion.identity;
        private Quaternion targetElbowL = Quaternion.identity, targetElbowR = Quaternion.identity;
        private Quaternion targetWristL = Quaternion.identity, targetWristR = Quaternion.identity;
        private Quaternion targetPelvis = Quaternion.identity, targetSpineLower = Quaternion.identity;
        private Quaternion targetSpineUpper = Quaternion.identity, targetNeck = Quaternion.identity;
        private float targetWeight, displayedWeight, targetBodyWeight, displayedBodyWeight;
        private int poseFrame = -1, bodyPoseFrame = -1;

        public bool Ready { get; private set; }

        public void Build(Transform root)
        {
            if (Ready || root == null) return;
            visualRoot = root;

            // Do not touch the visible hierarchy unless both sides can be articulated completely.
            // This keeps old/proxy hero builds on their proven fallback instead of half-converting them.
            if (!CanBuild())
            {
                Debug.LogWarning("[Alterverse Movement] Procedural rig preflight failed; no legacy movement fallback will run.", this);
                return;
            }

            bool leftLeg = BuildLeg("L", out hipL, out kneeL, out ankleL);
            bool rightLeg = BuildLeg("R", out hipR, out kneeR, out ankleR);
            bool leftArm = BuildArm("L", out elbowL, out wristL);
            bool rightArm = BuildArm("R", out elbowR, out wristR);
            bool body = BuildBodyHierarchy();

            hipRestL = Rest(hipL); hipRestR = Rest(hipR);
            kneeRestL = Rest(kneeL); kneeRestR = Rest(kneeR);
            ankleRestL = Rest(ankleL); ankleRestR = Rest(ankleR);
            elbowRestL = Rest(elbowL); elbowRestR = Rest(elbowR);
            wristRestL = Rest(wristL); wristRestR = Rest(wristR);

            Ready = leftLeg && rightLeg && leftArm && rightArm;
            if (!Ready)
                Debug.LogWarning("[Alterverse Movement] Procedural joint construction failed; no legacy movement fallback will run.", this);
            if (!body)
                Debug.LogWarning("[Alterverse Movement] Full torso hierarchy could not be built on '" + name + "'; limb rig remains available.", this);
        }

        private bool BuildBodyHierarchy()
        {
            Transform torso = FindDeep("DF_Torso");
            Transform pelvisMesh = FindDeep("DF_Pelvis");
            Transform head = FindDeep("DF_Head");
            Transform neck = FindDeep("DF_Neck");
            Transform armLeft = FindDeep("DF_ArmL");
            Transform armRight = FindDeep("DF_ArmR");
            if (torso == null || pelvisMesh == null || head == null) return false;

            // These pivots are presentation bones under DF_VisualRoot. They never become
            // ancestors of the gameplay root, colliders or hitboxes.
            Vector3 pelvisPosition = CenterOf(pelvisMesh);
            Vector3 lowerPosition = visualRoot.TransformPoint(new Vector3(0f, -.13f, 0f));
            Vector3 upperPosition = CenterOf(torso);
            Vector3 neckPosition = neck != null ? CenterOf(neck) : head.position - head.up * .20f;
            pelvisJoint = CreateJoint("DF_PelvisJoint", visualRoot, pelvisPosition, visualRoot.rotation);
            spineLowerJoint = CreateJoint("DF_SpineLower", pelvisJoint, lowerPosition, visualRoot.rotation);
            spineUpperJoint = CreateJoint("DF_SpineUpper", spineLowerJoint, upperPosition, visualRoot.rotation);
            neckJoint = CreateJoint("DF_NeckJoint", spineUpperJoint, neckPosition, visualRoot.rotation);

            List<Transform> originalRootParts = new();
            for (int i = 0; i < visualRoot.childCount; i++)
            {
                Transform child = visualRoot.GetChild(i);
                if (child != pelvisJoint && child != spineLowerJoint && child != spineUpperJoint && child != neckJoint)
                    originalRootParts.Add(child);
            }

            pelvisMesh.SetParent(pelvisJoint, true);
            torso.SetParent(spineUpperJoint, true);
            head.SetParent(neckJoint, true);
            if (neck != null) neck.SetParent(neckJoint, true);
            if (armLeft != null) armLeft.SetParent(spineUpperJoint, true);
            if (armRight != null) armRight.SetParent(spineUpperJoint, true);

            foreach (Transform part in originalRootParts)
            {
                if (part == torso || part == pelvisMesh || part == head || part == neck ||
                    part == armLeft || part == armRight || part == hipL || part == hipR ||
                    part == kneeL || part == kneeR || part == ankleL || part == ankleR ||
                    part == elbowL || part == elbowR || part == wristL || part == wristR) continue;
                if (part == null || part.parent != visualRoot) continue;

                string partName = part.name;
                Vector3 localPosition = part.localPosition;
                if (IsBodyCloth(partName))
                {
                    part.SetParent(localPosition.y < -.18f ? pelvisJoint : spineUpperJoint, true);
                    continue;
                }
                if (IsHeadPart(partName, localPosition))
                    part.SetParent(neckJoint, true);
                else if (IsPelvisPart(partName, localPosition))
                    part.SetParent(pelvisJoint, true);
                else if (Mathf.Abs(localPosition.x) <= .32f &&
                         localPosition.y >= -.08f && localPosition.y <= .58f &&
                         !IsIndependentAccessory(partName))
                    part.SetParent(spineUpperJoint, true);
            }

            return true;
        }

        private static bool IsBodyCloth(string partName)
        {
            string n = partName.ToLowerInvariant();
            return n.Contains("cape") || n.Contains("cloak") || n.Contains("mantle") ||
                   n.Contains("robe") || n.Contains("tabard") || n.Contains("veil") ||
                   n.Contains("drape") || n.Contains("stole") || n.Contains("coattail");
        }

        private static bool IsHeadPart(string partName, Vector3 localPosition)
        {
            string n = partName.ToLowerInvariant();
            if (n.Contains("orbit") || n.Contains("halo") || n.Contains("wing") ||
                n.Contains("projector") || n.Contains("weapon") || n.Contains("staff") ||
                n.Contains("rifle") || n.Contains("cape") || n.Contains("robe") ||
                n.Contains("coat") || n.Contains("muzzle") || n.Contains("emitter")) return false;
            return n.Contains("head") || n.Contains("neck") || n.Contains("hair") ||
                   n.Contains("face") || n.Contains("eye") || n.Contains("visor") ||
                   n.Contains("brow") || n.Contains("jaw") || n.Contains("mask") ||
                   localPosition.y > .69f && Mathf.Abs(localPosition.x) < .32f;
        }

        private static bool IsPelvisPart(string partName, Vector3 localPosition)
        {
            string n = partName.ToLowerInvariant();
            return n.Contains("pelvis") || n.Contains("belt") || n.Contains("buckle") ||
                   n.Contains("tasset") || n.Contains("hipguard") ||
                   localPosition.y >= -.43f && localPosition.y < -.15f;
        }

        private static bool IsIndependentAccessory(string partName)
        {
            string n = partName.ToLowerInvariant();
            return n.Contains("orbit") || n.Contains("halo") || n.Contains("wing") ||
                   n.Contains("projector") || n.Contains("weapon") || n.Contains("staff") ||
                   n.Contains("rifle") || n.Contains("cape") || n.Contains("muzzle") ||
                   n.Contains("emitter");
        }

        private static Vector3 CenterOf(Transform part)
        {
            if (part == null) return Vector3.zero;
            MeshFilter filter = part.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                return part.TransformPoint(filter.sharedMesh.bounds.center);
            Renderer renderer = part.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds.center : part.position;
        }

        public void SetBodyPose(float weight, Quaternion pelvis, Quaternion lowerSpine,
            Quaternion upperSpine, Quaternion neck)
        {
            if (pelvisJoint == null || spineLowerJoint == null || spineUpperJoint == null || neckJoint == null) return;
            targetPelvis = pelvis;
            targetSpineLower = lowerSpine;
            targetSpineUpper = upperSpine;
            targetNeck = neck;
            targetBodyWeight = Mathf.Clamp01(weight);
            bodyPoseFrame = Time.frameCount;
        }

        private bool CanBuild()
        {
            Transform leftLeg = FindDeep("DF_LegL");
            Transform rightLeg = FindDeep("DF_LegR");
            Transform leftBoot = FindDeep("DF_BootL");
            Transform rightBoot = FindDeep("DF_BootR");
            Transform leftArm = FindDeep("DF_ArmL");
            Transform rightArm = FindDeep("DF_ArmR");
            if (leftLeg == null || rightLeg == null || leftBoot == null || rightBoot == null ||
                leftArm == null || rightArm == null) return false;

            MeshFilter leftFilter = leftLeg.GetComponent<MeshFilter>();
            MeshFilter rightFilter = rightLeg.GetComponent<MeshFilter>();
            return leftFilter != null && leftFilter.sharedMesh != null && leftLeg.GetComponent<MeshRenderer>() != null &&
                   rightFilter != null && rightFilter.sharedMesh != null && rightLeg.GetComponent<MeshRenderer>() != null;
        }

        public void SetProceduralPose(float weight,
            Quaternion leftHip, Quaternion leftKnee, Quaternion leftAnkle,
            Quaternion rightHip, Quaternion rightKnee, Quaternion rightAnkle,
            Quaternion leftElbow, Quaternion leftWrist,
            Quaternion rightElbow, Quaternion rightWrist)
        {
            if (!Ready) return;
            targetHipL = leftHip; targetKneeL = leftKnee; targetAnkleL = leftAnkle;
            targetHipR = rightHip; targetKneeR = rightKnee; targetAnkleR = rightAnkle;
            targetElbowL = leftElbow; targetWristL = leftWrist;
            targetElbowR = rightElbow; targetWristR = rightWrist;
            targetWeight = Mathf.Clamp01(weight);
            poseFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            if (!Ready) return;

            float dt = Mathf.Max(Time.deltaTime, .0001f);
            if (poseFrame != Time.frameCount) targetWeight = 0f;
            if (bodyPoseFrame != Time.frameCount) targetBodyWeight = 0f;
            float response = 1f - Mathf.Exp(-18f * dt);
            displayedWeight = Mathf.Lerp(displayedWeight, targetWeight, response);

            ApplyJoint(hipL, hipRestL, targetHipL, displayedWeight, response);
            ApplyJoint(hipR, hipRestR, targetHipR, displayedWeight, response);
            ApplyJoint(kneeL, kneeRestL, targetKneeL, displayedWeight, response);
            ApplyJoint(kneeR, kneeRestR, targetKneeR, displayedWeight, response);
            ApplyJoint(ankleL, ankleRestL, targetAnkleL, displayedWeight * .72f, response);
            ApplyJoint(ankleR, ankleRestR, targetAnkleR, displayedWeight * .72f, response);
            ApplyJoint(elbowL, elbowRestL, targetElbowL, displayedWeight, response);
            ApplyJoint(elbowR, elbowRestR, targetElbowR, displayedWeight, response);
            ApplyJoint(wristL, wristRestL, targetWristL, displayedWeight * .45f, response);
            ApplyJoint(wristR, wristRestR, targetWristR, displayedWeight * .45f, response);

            displayedBodyWeight = Mathf.Lerp(displayedBodyWeight, targetBodyWeight, response);
            ApplyJoint(pelvisJoint, Quaternion.identity, targetPelvis, displayedBodyWeight, response);
            ApplyJoint(spineLowerJoint, Quaternion.identity, targetSpineLower, displayedBodyWeight, response);
            ApplyJoint(spineUpperJoint, Quaternion.identity, targetSpineUpper, displayedBodyWeight, response);
            ApplyJoint(neckJoint, Quaternion.identity, targetNeck, displayedBodyWeight, response);
        }

        private static void ApplyJoint(Transform joint, Quaternion rest, Quaternion delta, float weight, float response)
        {
            if (joint == null) return;
            Quaternion goal = rest * Quaternion.Slerp(Quaternion.identity, delta, weight);
            joint.localRotation = Quaternion.Slerp(joint.localRotation, goal, response);
        }

        private bool BuildArm(string suffix, out Transform elbow, out Transform wrist)
        {
            elbow = wrist = null;
            Transform arm = FindDeep("DF_Arm" + suffix);
            if (arm == null) return false;

            Transform forearm = FindDeepUnder(arm, "DF_Vambrace" + suffix) ??
                                FindChildContaining(arm, "forearm") ?? FindChildContaining(arm, "vambrace");
            Transform hand = FindDeepUnder(arm, "DF_Glove" + suffix);
            Transform gauntlet = FindDeep("DF_Gauntlet" + suffix);
            if (hand == null && gauntlet != null) hand = gauntlet;

            Vector3 elbowWorld = forearm != null ? TopOf(forearm) : arm.TransformPoint(new Vector3(0f, -.27f, .01f));
            Vector3 wristWorld = hand != null ? TopOf(hand) : arm.TransformPoint(new Vector3(0f, -.55f, .04f));

            elbow = CreateJoint("DF_ElbowJoint" + suffix, arm, elbowWorld, arm.rotation);
            wrist = CreateJoint("DF_WristJoint" + suffix, elbow, wristWorld, arm.rotation);

            List<Transform> direct = new();
            for (int i = 0; i < arm.childCount; i++)
            {
                Transform child = arm.GetChild(i);
                if (child != elbow) direct.Add(child);
            }

            Vector3 elbowLocalRoot = visualRoot.InverseTransformPoint(elbowWorld);
            Vector3 wristLocalRoot = visualRoot.InverseTransformPoint(wristWorld);
            float elbowY = elbowLocalRoot.y;
            float wristY = wristLocalRoot.y;
            for (int i = 0; i < direct.Count; i++)
            {
                Transform child = direct[i];
                float y = visualRoot.InverseTransformPoint(child.position).y;
                if (y <= wristY + .035f) child.SetParent(wrist, true);
                else if (y <= elbowY + .055f) child.SetParent(elbow, true);
            }

            // Brakk's large gauntlets are root-level hand pieces in the production model.
            if (gauntlet != null && gauntlet.parent != wrist)
                gauntlet.SetParent(wrist, true);

            return true;
        }

        private bool BuildLeg(string suffix, out Transform hip, out Transform knee, out Transform ankle)
        {
            hip = knee = ankle = null;
            Transform original = FindDeep("DF_Leg" + suffix);
            Transform boot = FindDeep("DF_Boot" + suffix);
            if (original == null || boot == null) return false;

            MeshFilter filter = original.GetComponent<MeshFilter>();
            MeshRenderer renderer = original.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null) return false;

            Transform kneeArmor = FindDeepUnder(original, "DF_Knee" + suffix) ?? FindDeep("DF_Knee" + suffix);
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 hipWorld = original.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            Vector3 ankleWorld = TopOf(boot);
            Vector3 kneeWorld = kneeArmor != null ? kneeArmor.position : Vector3.Lerp(hipWorld, ankleWorld, .51f);
            float splitY = original.InverseTransformPoint(kneeWorld).y;

            Transform parent = original.parent;
            original.name = "DF_LegLegacy" + suffix;
            hip = CreateJoint("DF_Leg" + suffix, parent, hipWorld, parent.rotation);
            knee = CreateJoint("DF_KneeJoint" + suffix, hip, kneeWorld, parent.rotation);
            ankle = CreateJoint("DF_AnkleJoint" + suffix, knee, ankleWorld, parent.rotation);

            Mesh upperMesh = SplitMesh(filter.sharedMesh, splitY, true);
            Mesh lowerMesh = SplitMesh(filter.sharedMesh, splitY, false);
            Transform thigh = CreateMeshCopy("DF_ThighMotion" + suffix, original, upperMesh, renderer, hip);
            Transform shin = CreateMeshCopy("DF_ShinMotion" + suffix, original, lowerMesh, renderer, knee);
            renderer.enabled = false;

            // Move existing armor pieces to the anatomical half they belong to.
            List<Transform> children = new();
            for (int i = 0; i < original.childCount; i++) children.Add(original.GetChild(i));
            float kneeRootY = visualRoot.InverseTransformPoint(kneeWorld).y;
            for (int i = 0; i < children.Count; i++)
            {
                Transform child = children[i];
                if (child == thigh || child == shin) continue;
                float y = visualRoot.InverseTransformPoint(child.position).y;
                child.SetParent(y >= kneeRootY ? hip : knee, true);
            }

            boot.SetParent(ankle, true);
            return true;
        }

        private Transform CreateMeshCopy(string name, Transform source, Mesh mesh, MeshRenderer sourceRenderer, Transform parent)
        {
            GameObject go = new(name);
            Transform t = go.transform;
            t.SetParent(source.parent, false);
            t.localPosition = source.localPosition;
            t.localRotation = source.localRotation;
            t.localScale = source.localScale;
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = sourceRenderer.sharedMaterials;
            mr.shadowCastingMode = sourceRenderer.shadowCastingMode;
            mr.receiveShadows = sourceRenderer.receiveShadows;
            mr.lightProbeUsage = sourceRenderer.lightProbeUsage;
            mr.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            t.SetParent(parent, true);
            return t;
        }

        private static Mesh SplitMesh(Mesh source, float splitY, bool upper)
        {
            string key = source.GetEntityId().GetHashCode() + ":" + Mathf.RoundToInt(splitY * 1000f) + ":" + (upper ? "U" : "L");
            if (SplitMeshCache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            MeshClipBuilder builder = new(source, splitY, upper);
            Mesh result = builder.Build();
            result.name = source.name + (upper ? "_UpperClosed" : "_LowerClosed");
            SplitMeshCache[key] = result;
            return result;
        }

        /// <summary>
        /// Clips a closed loft mesh against a horizontal plane, interpolating its vertex data
        /// and sealing the new joint surface. Triangle-centroid filtering leaves torn, open legs.
        /// </summary>
        private sealed class MeshClipBuilder
        {
            private readonly Mesh source;
            private readonly float splitY;
            private readonly bool keepUpper;
            private readonly Vector3[] sourceVertices, sourceNormals;
            private readonly Vector4[] sourceTangents;
            private readonly Vector2[] sourceUv, sourceUv2;
            private readonly Color[] sourceColors;
            private readonly List<Vector3> vertices = new();
            private readonly List<Vector3> normals = new();
            private readonly List<Vector4> tangents = new();
            private readonly List<Vector2> uv = new(), uv2 = new();
            private readonly List<Color> colors = new();
            private readonly List<int>[] triangles;
            private readonly Dictionary<long, int> vertexMap = new();
            private readonly HashSet<int> boundary = new();

            private readonly struct ClipVertex
            {
                public readonly int A, B;
                public readonly float T;
                public ClipVertex(int index) { A = B = index; T = 0f; }
                public ClipVertex(int a, int b, float t) { A = a; B = b; T = t; }
            }

            public MeshClipBuilder(Mesh mesh, float plane, bool upper)
            {
                source = mesh;
                splitY = plane;
                keepUpper = upper;
                sourceVertices = mesh.vertices;
                sourceNormals = mesh.normals;
                sourceTangents = mesh.tangents;
                sourceUv = mesh.uv;
                sourceUv2 = mesh.uv2;
                sourceColors = mesh.colors;
                triangles = new List<int>[mesh.subMeshCount];
                for (int i = 0; i < triangles.Length; i++) triangles[i] = new List<int>();
            }

            public Mesh Build()
            {
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    int[] indices = source.GetTriangles(sub);
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                        ClipTriangle(sub, indices[i], indices[i + 1], indices[i + 2]);
                }

                SealCut();
                Mesh mesh = new() { indexFormat = source.indexFormat, subMeshCount = source.subMeshCount };
                mesh.SetVertices(vertices);
                if (sourceNormals != null && sourceNormals.Length == source.vertexCount) mesh.SetNormals(normals);
                if (sourceTangents != null && sourceTangents.Length == source.vertexCount) mesh.SetTangents(tangents);
                if (sourceUv != null && sourceUv.Length == source.vertexCount) mesh.SetUVs(0, uv);
                if (sourceUv2 != null && sourceUv2.Length == source.vertexCount) mesh.SetUVs(1, uv2);
                if (sourceColors != null && sourceColors.Length == source.vertexCount) mesh.SetColors(colors);
                for (int sub = 0; sub < triangles.Length; sub++) mesh.SetTriangles(triangles[sub], sub, false);
                mesh.RecalculateBounds();
                return mesh;
            }

            private void ClipTriangle(int sub, int a, int b, int c)
            {
                List<ClipVertex> polygon = new(4) { new(a), new(b), new(c) };
                List<ClipVertex> clipped = new(4);
                for (int i = 0; i < polygon.Count; i++)
                {
                    ClipVertex current = polygon[i];
                    ClipVertex next = polygon[(i + 1) % polygon.Count];
                    bool insideCurrent = Inside(current);
                    bool insideNext = Inside(next);
                    if (insideCurrent && insideNext) clipped.Add(next);
                    else if (insideCurrent && !insideNext) clipped.Add(Intersection(current, next));
                    else if (!insideCurrent && insideNext)
                    {
                        ClipVertex crossing = Intersection(current, next);
                        clipped.Add(crossing);
                        clipped.Add(next);
                    }
                }

                if (clipped.Count < 3) return;
                int first = AddVertex(clipped[0]);
                for (int i = 1; i + 1 < clipped.Count; i++)
                {
                    int second = AddVertex(clipped[i]);
                    int third = AddVertex(clipped[i + 1]);
                    triangles[sub].Add(first);
                    triangles[sub].Add(second);
                    triangles[sub].Add(third);
                }
            }

            private bool Inside(ClipVertex vertex)
            {
                float y = Position(vertex).y;
                return keepUpper ? y >= splitY - .00001f : y <= splitY + .00001f;
            }

            private ClipVertex Intersection(ClipVertex from, ClipVertex to)
            {
                Vector3 a = Position(from), b = Position(to);
                float t = Mathf.Clamp01((splitY - a.y) / (b.y - a.y));
                ClipVertex result = Interpolate(from, to, t);
                boundary.Add(AddVertex(result));
                return result;
            }

            private static ClipVertex Interpolate(ClipVertex from, ClipVertex to, float t)
            {
                // The clipping polygon only references original vertices and plane-edge points.
                // Normalize the pair key so adjacent triangles share the exact same cut vertex.
                int fromIndex = from.A == from.B ? from.A : -1;
                int toIndex = to.A == to.B ? to.A : -1;
                if (fromIndex >= 0 && toIndex >= 0) return new ClipVertex(fromIndex, toIndex, t);

                // A cut point lies on an original edge. Reconstruct a stable original-edge pair.
                if (from.A == from.B) return new ClipVertex(from.A, to.A, t);
                if (to.A == to.B) return new ClipVertex(from.A, to.A, t);
                return new ClipVertex(from.A, to.B, t);
            }

            private Vector3 Position(ClipVertex vertex)
            {
                return Vector3.Lerp(sourceVertices[vertex.A], sourceVertices[vertex.B], vertex.T);
            }

            private int AddVertex(ClipVertex vertex)
            {
                int lo = Mathf.Min(vertex.A, vertex.B), hi = Mathf.Max(vertex.A, vertex.B);
                long key = ((long)lo << 32) | (uint)hi;
                if (vertexMap.TryGetValue(key, out int existing)) return existing;

                int index = vertices.Count;
                vertexMap.Add(key, index);
                vertices.Add(Position(vertex));
                if (sourceNormals != null && sourceNormals.Length == source.vertexCount)
                    normals.Add(Vector3.Lerp(sourceNormals[vertex.A], sourceNormals[vertex.B], vertex.T).normalized);
                if (sourceTangents != null && sourceTangents.Length == source.vertexCount)
                    tangents.Add(Vector4.Lerp(sourceTangents[vertex.A], sourceTangents[vertex.B], vertex.T));
                if (sourceUv != null && sourceUv.Length == source.vertexCount)
                    uv.Add(Vector2.Lerp(sourceUv[vertex.A], sourceUv[vertex.B], vertex.T));
                if (sourceUv2 != null && sourceUv2.Length == source.vertexCount)
                    uv2.Add(Vector2.Lerp(sourceUv2[vertex.A], sourceUv2[vertex.B], vertex.T));
                if (sourceColors != null && sourceColors.Length == source.vertexCount)
                    colors.Add(Color.Lerp(sourceColors[vertex.A], sourceColors[vertex.B], vertex.T));
                return index;
            }

            private void SealCut()
            {
                if (boundary.Count < 3) return;
                Vector3 center = Vector3.zero;
                foreach (int index in boundary) center += vertices[index];
                center /= boundary.Count;
                List<int> ring = new(boundary);
                ring.Sort((a, b) =>
                {
                    Vector3 pa = vertices[a] - center, pb = vertices[b] - center;
                    return Mathf.Atan2(pa.z, pa.x).CompareTo(Mathf.Atan2(pb.z, pb.x));
                });

                Vector3 capNormal = keepUpper ? Vector3.down : Vector3.up;
                int capCenter = AddCapVertex(center, capNormal);
                int[] capRing = new int[ring.Count];
                for (int i = 0; i < ring.Count; i++) capRing[i] = AddCapVertex(vertices[ring[i]], capNormal);
                List<int> cap = triangles[0];
                for (int i = 0; i < capRing.Length; i++)
                {
                    int next = (i + 1) % capRing.Length;
                    cap.Add(capCenter);
                    if (keepUpper) { cap.Add(capRing[i]); cap.Add(capRing[next]); }
                    else { cap.Add(capRing[next]); cap.Add(capRing[i]); }
                }
            }

            private int AddCapVertex(Vector3 position, Vector3 normal)
            {
                int index = vertices.Count;
                vertices.Add(position);
                if (sourceNormals != null && sourceNormals.Length == source.vertexCount) normals.Add(normal);
                if (sourceTangents != null && sourceTangents.Length == source.vertexCount) tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                if (sourceUv != null && sourceUv.Length == source.vertexCount) uv.Add(Vector2.zero);
                if (sourceUv2 != null && sourceUv2.Length == source.vertexCount) uv2.Add(Vector2.zero);
                if (sourceColors != null && sourceColors.Length == source.vertexCount)
                {
                    Color average = Color.white;
                    if (boundary.Count > 0)
                    {
                        average = Color.clear;
                        foreach (int boundaryIndex in boundary) average += colors[boundaryIndex];
                        average /= boundary.Count;
                    }
                    colors.Add(average);
                }
                return index;
            }

        }

        private Transform FindDeep(string name)
        {
            if (visualRoot == null) return null;
            foreach (Transform t in visualRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Transform FindDeepUnder(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Transform FindChildContaining(Transform root, string token)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return t;
            return null;
        }

        private static Transform CreateJoint(string name, Transform parent, Vector3 worldPosition, Quaternion worldRotation)
        {
            Transform joint = new GameObject(name).transform;
            joint.SetParent(parent, true);
            joint.position = worldPosition;
            joint.rotation = worldRotation;
            joint.localScale = Vector3.one;
            return joint;
        }

        private static Vector3 TopOf(Transform value)
        {
            if (value == null) return Vector3.zero;
            MeshFilter filter = value.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Bounds bounds = filter.sharedMesh.bounds;
                return value.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            }
            return value.position + value.up * .08f;
        }

        private static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;
    }
}
