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
    [DefaultExecutionOrder(925)]
    public sealed class HeroArticulatedJointRig : MonoBehaviour
    {
        private static readonly Dictionary<string, Mesh> SplitMeshCache = new();

        private Transform visualRoot;
        private Transform hipL, hipR, kneeL, kneeR, ankleL, ankleR;
        private Transform elbowL, elbowR, wristL, wristR;
        private Quaternion hipRestL, hipRestR, kneeRestL, kneeRestR, ankleRestL, ankleRestR;
        private Quaternion elbowRestL, elbowRestR, wristRestL, wristRestR;
        private Quaternion targetHipL = Quaternion.identity, targetHipR = Quaternion.identity;
        private Quaternion targetKneeL = Quaternion.identity, targetKneeR = Quaternion.identity;
        private Quaternion targetAnkleL = Quaternion.identity, targetAnkleR = Quaternion.identity;
        private Quaternion targetElbowL = Quaternion.identity, targetElbowR = Quaternion.identity;
        private Quaternion targetWristL = Quaternion.identity, targetWristR = Quaternion.identity;
        private float targetWeight, displayedWeight;
        private int poseFrame = -1;

        public bool Ready { get; private set; }

        public void Build(Transform root)
        {
            if (Ready || root == null) return;
            visualRoot = root;

            // Do not touch the visible hierarchy unless both sides can be articulated completely.
            // This keeps old/proxy hero builds on their proven fallback instead of half-converting them.
            if (!CanBuild())
            {
                Debug.LogWarning("[Alterverse] Hero joint rig preflight failed; keeping the classic movement hierarchy intact.", this);
                return;
            }

            bool leftLeg = BuildLeg("L", out hipL, out kneeL, out ankleL);
            bool rightLeg = BuildLeg("R", out hipR, out kneeR, out ankleR);
            bool leftArm = BuildArm("L", out elbowL, out wristL);
            bool rightArm = BuildArm("R", out elbowR, out wristR);

            hipRestL = Rest(hipL); hipRestR = Rest(hipR);
            kneeRestL = Rest(kneeL); kneeRestR = Rest(kneeR);
            ankleRestL = Rest(ankleL); ankleRestR = Rest(ankleR);
            elbowRestL = Rest(elbowL); elbowRestR = Rest(elbowR);
            wristRestL = Rest(wristL); wristRestR = Rest(wristR);

            Ready = leftLeg && rightLeg && leftArm && rightArm;
            if (!Ready)
                Debug.LogWarning("[Alterverse] Hero articulated rig could not resolve every limb; the classic motion fallback stays active.", this);
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

        public void SetTemplatePose(float weight,
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

            Mesh result = new() { name = source.name + (upper ? "_Upper" : "_Lower"), indexFormat = source.indexFormat };
            result.vertices = source.vertices;
            if (source.normals != null && source.normals.Length == source.vertexCount) result.normals = source.normals;
            if (source.tangents != null && source.tangents.Length == source.vertexCount) result.tangents = source.tangents;
            if (source.uv != null && source.uv.Length == source.vertexCount) result.uv = source.uv;
            if (source.uv2 != null && source.uv2.Length == source.vertexCount) result.uv2 = source.uv2;
            if (source.colors != null && source.colors.Length == source.vertexCount) result.colors = source.colors;

            Vector3[] vertices = source.vertices;
            result.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] sourceTriangles = source.GetTriangles(sub);
                List<int> triangles = new(sourceTriangles.Length);
                for (int i = 0; i + 2 < sourceTriangles.Length; i += 3)
                {
                    int a = sourceTriangles[i], b = sourceTriangles[i + 1], c = sourceTriangles[i + 2];
                    float center = (vertices[a].y + vertices[b].y + vertices[c].y) / 3f;
                    if (upper ? center >= splitY : center < splitY)
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    }
                }
                result.SetTriangles(triangles, sub, false);
            }
            result.RecalculateBounds();
            SplitMeshCache[key] = result;
            return result;
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
