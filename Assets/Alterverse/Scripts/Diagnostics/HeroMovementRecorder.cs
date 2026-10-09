#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    /// <summary>Read-only final-frame diagnostics. Exists only during an editor recording.</summary>
    public sealed class HeroMovementRecorder : MonoBehaviour
    {
        private sealed class Sample
        {
            public HeroIdentity hero;
            public readonly Transform[] bones = new Transform[6];
            public readonly Quaternion[] referenceRotation = new Quaternion[6];
            public readonly Vector3[] referencePosition = new Vector3[6];
            public readonly Quaternion[] lastRotation = new Quaternion[6];
            public readonly Vector3[] lastRelativePosition = new Vector3[6];
            public readonly HashSet<string> issues = new HashSet<string>();
            public Vector3 lastActor, lastPresented;
            public float actorTravel, presentedTravel, maxBoneAngle, maxRelativeFootShift, maxContactError;
            public float maxReportedSpeed, maxSwingRise, walkingSeconds;
            public readonly float[] boneRotationTravel = new float[2];
            public readonly float[] relativeFootTravel = new float[2];
            public readonly float[] maxLegAngle = new float[2];
            public readonly float[] maxFootExcursion = new float[2];
            public int frames, walkingFrames, suspendedFrames, deadFrames, inputBlockedFrames, relocations;
            public bool initialized;
            public string state = "no sample";
        }
        private readonly Dictionary<int, Sample> samples = new Dictionary<int, Sample>();
        private readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        private double deadline;
        private bool finished;
        private int recordedFrames;

        private void Start()
        {
            deadline = EditorApplication.timeSinceStartup + 15d;
            EditorApplication.update += CheckDeadline;
            Debug.Log("DF Movement: recording all active heroes for 15 real-time seconds. Keep the Game view visible; move, turn, stop and attack. No gameplay values are changed.", this);
            StartCoroutine(Record());
        }
        private IEnumerator Record()
        {
            while (!finished)
            {
                // Rendering follows every LateUpdate pose writer, including imported combat and foot IK.
                yield return endOfFrame;
                if (finished) yield break;
                CaptureFrame();
                if (EditorApplication.timeSinceStartup >= deadline) Finish();
            }
        }
        private void CheckDeadline()
        {
            // WaitForEndOfFrame can pause when Unity switches from Game view to Scene view.
            // Always end the recording even if rendering is paused or no heroes were drawn.
            if (!finished && EditorApplication.timeSinceStartup >= deadline) Finish();
        }
        private void CaptureFrame()
        {
            recordedFrames++;
            foreach (var hero in UnityEngine.Object.FindObjectsByType<HeroIdentity>(FindObjectsSortMode.None))
            {
                if (hero == null || !hero.gameObject.activeInHierarchy) continue;
                var health = hero.GetComponent<Health>();
                var root = hero.transform.Find("DF_VisualRoot");
                // Ignore menu previews; gameplay heroes must have Health and a visible presentation.
                if (health == null) continue;
                int key = hero.GetInstanceID();
                if (!samples.TryGetValue(key, out Sample sample))
                {
                    sample = new Sample { hero = hero };
                    samples.Add(key, sample);
                }
                bool missingBone = false;
                foreach (Transform bone in sample.bones) if (bone == null) missingBone = true;
                if (missingBone) BindBones(sample, root);
                sample.frames++;
                if (health.IsDead) { sample.deadFrames++; sample.initialized = false; continue; }
                // Report the latest structural state; startup delays that recover do not
                // remain permanent failures in the final result. Motion metrics retain history.
                sample.issues.Clear();
                var motion = hero.GetComponent<HeroVisualMotion>();
                var feet = hero.GetComponent<HeroGroundedLocomotion>();
                var procedural = hero.GetComponent<HeroProceduralRig>();
                var body = hero.GetComponent<HeroLocomotion>();
                var motor = hero.GetComponent<HeroMotor>();
                if (root == null) sample.issues.Add("DF_VisualRoot missing: inspect hero visual creation");
                if (motion == null || !motion.isActiveAndEnabled)
                    sample.issues.Add("HeroVisualMotion missing/disabled: inspect movement initialization errors");
                if (procedural == null || !procedural.Ready || !procedural.isActiveAndEnabled)
                    sample.issues.Add("HeroProceduralRig missing, incomplete or disabled: inspect body/limb controls");
                if (feet == null || !feet.Ready || !feet.isActiveAndEnabled)
                    sample.issues.Add("HeroGroundedLocomotion missing, incomplete or disabled: inspect visible leg binding");
                foreach (Transform bone in sample.bones)
                    if (bone == null) sample.issues.Add("Visible Thigh/Shin/Foot chain incomplete");
                sample.state = "motor=" + (motor == null ? "absent (bot/remote possible)" : motor.State + "/enabled=" + motor.isActiveAndEnabled) +
                    ", input=" + (motor != null && motor.HasMoveInput) +
                    ", locomotion=" + (body == null ? "absent" : "enabled=" + body.isActiveAndEnabled + "/canMove=" + body.CanMove) +
                    ", alive=true, motion=" + (motion != null && motion.isActiveAndEnabled) +
                    ", last collision=" + (body != null && body.LastCollisionCollider != null ? body.LastCollisionCollider.name : "none");
                Vector3 actor = hero.transform.position;
                bool motionActive = motion != null && motion.isActiveAndEnabled;
                Vector3 presented = motionActive ? motion.PresentedPosition : actor;
                Quaternion facing = motionActive ? motion.PresentedRotation : hero.transform.rotation;
                if (!sample.initialized)
                {
                    sample.lastActor = actor; sample.lastPresented = presented; sample.initialized = true;
                    for (int i = 0; i < sample.bones.Length; i++)
                        if (sample.bones[i] != null)
                        {
                            sample.referenceRotation[i] = sample.lastRotation[i] = sample.bones[i].localRotation;
                            sample.referencePosition[i] = sample.lastRelativePosition[i] = Quaternion.Inverse(facing) * (sample.bones[i].position - presented);
                        }
                    continue;
                }
                Vector3 actorDelta = actor - sample.lastActor, delta = presented - sample.lastPresented;
                actorDelta.y = delta.y = 0f;
                sample.lastActor = actor; sample.lastPresented = presented;
                if (delta.magnitude > 2f || actorDelta.magnitude > 2f)
                { sample.relocations++; sample.initialized = false; continue; }
                sample.actorTravel += actorDelta.magnitude; sample.presentedTravel += delta.magnitude;
                float speed = delta.magnitude / Mathf.Max(Time.deltaTime, .0001f);
                if (motionActive) sample.maxReportedSpeed = Mathf.Max(sample.maxReportedSpeed, motion.PlanarSpeed);
                if (motor != null && motor.HasMoveInput && actorDelta.magnitude < .0005f) sample.inputBlockedFrames++;
                bool suspended = motionActive && motion.GroundingSuspended;
                if (suspended) sample.suspendedFrames++;
                bool walking = !suspended && Time.timeScale > 0f && speed >= .2f;
                float leftBoneTurn = 0f, rightBoneTurn = 0f, leftFootTravel = 0f, rightFootTravel = 0f;
                for (int i = 0; i < sample.bones.Length; i++)
                {
                    Transform bone = sample.bones[i]; if (bone == null) continue;
                    Vector3 relative = Quaternion.Inverse(facing) * (bone.position - presented);
                    float turn = Quaternion.Angle(sample.lastRotation[i], bone.localRotation);
                    if (i < 3) leftBoneTurn = Mathf.Max(leftBoneTurn, turn);
                    else rightBoneTurn = Mathf.Max(rightBoneTurn, turn);
                    if (i == 2) leftFootTravel = Vector3.Distance(relative, sample.lastRelativePosition[i]);
                    if (i == 5) rightFootTravel = Vector3.Distance(relative, sample.lastRelativePosition[i]);
                    sample.lastRotation[i] = bone.localRotation; sample.lastRelativePosition[i] = relative;
                }
                if (!walking) continue;
                sample.walkingFrames++; sample.walkingSeconds += Time.deltaTime;
                sample.boneRotationTravel[0] += leftBoneTurn; sample.boneRotationTravel[1] += rightBoneTurn;
                sample.relativeFootTravel[0] += leftFootTravel; sample.relativeFootTravel[1] += rightFootTravel;
                for (int i = 0; i < sample.bones.Length; i++)
                {
                    Transform bone = sample.bones[i]; if (bone == null) continue;
                    int side = i < 3 ? 0 : 1;
                    float angle = Quaternion.Angle(sample.referenceRotation[i], bone.localRotation);
                    sample.maxBoneAngle = Mathf.Max(sample.maxBoneAngle, angle);
                    sample.maxLegAngle[side] = Mathf.Max(sample.maxLegAngle[side], angle);
                    if (i == 2 || i == 5)
                    {
                        Vector3 relative = Quaternion.Inverse(facing) * (bone.position - presented);
                        float excursion = Vector3.Distance(relative, sample.referencePosition[i]);
                        sample.maxRelativeFootShift = Mathf.Max(sample.maxRelativeFootShift, excursion);
                        sample.maxFootExcursion[side] = Mathf.Max(sample.maxFootExcursion[side], excursion);
                        sample.maxSwingRise = Mathf.Max(sample.maxSwingRise, relative.y - sample.referencePosition[i].y);
                    }
                }
                if (feet != null && feet.Ready && feet.isActiveAndEnabled)
                    sample.maxContactError = Mathf.Max(sample.maxContactError, Mathf.Max(feet.LeftContactError, feet.RightContactError));
            }
        }
        private static void BindBones(Sample sample, Transform root)
        {
            if (root == null) return;
            int boundBefore = 0;
            foreach (Transform bone in sample.bones) if (bone != null) boundBefore++;
            Transform imported = root.Find("DF_AuthoredModel");
            string[] names = { "ThighL", "ShinL", "FootL", "ThighR", "ShinR", "FootR" };
            var rig = sample.hero.GetComponent<HeroProceduralRig>();
            if (imported != null)
            {
                foreach (Transform bone in imported.GetComponentsInChildren<Transform>(true))
                    for (int i = 0; i < names.Length; i++) if (bone.name == names[i]) sample.bones[i] = bone;
            }
            else if (rig != null && rig.Ready)
            {
                rig.GetLeg(false, out sample.bones[0], out sample.bones[1], out sample.bones[2]);
                rig.GetLeg(true, out sample.bones[3], out sample.bones[4], out sample.bones[5]);
            }
            // Newly available bones need their own baseline, not zero quaternions/positions.
            int boundAfter = 0;
            foreach (Transform bone in sample.bones) if (bone != null) boundAfter++;
            if (boundAfter > boundBefore) sample.initialized = false;
        }
        private void Finish()
        {
            if (finished) return;
            finished = true; EditorApplication.update -= CheckDeadline;
            foreach (Sample sample in samples.Values)
            {
                string label = sample.hero != null ? sample.hero.name + " / " + sample.hero.DisplayName : "despawned hero";
                for (int side = 0; side < 2; side++)
                    if (sample.walkingFrames >= 20 && sample.presentedTravel > .5f &&
                        sample.boneRotationTravel[side] / Mathf.Max(.001f, sample.walkingSeconds) < 5f &&
                        sample.relativeFootTravel[side] / Mathf.Max(.001f, sample.walkingSeconds) < .03f)
                        sample.issues.Add("SLIDING " + (side == 0 ? "LEFT" : "RIGHT") +
                            " leg: visible bones stayed practically static while traveling; inspect final writers, gait activation and imported skin weights");
                if (sample.maxContactError > .035f)
                    sample.issues.Add("Foot contact error exceeds 35 mm: inspect reach, ground targets and imported scale");
                string report = "DF Movement: " + label + ": actor travel=" + sample.actorTravel.ToString("F2") +
                    "m, presented travel=" + sample.presentedTravel.ToString("F2") + "m, walking frames=" + sample.walkingFrames +
                    ", avg leg motion=" + (Mathf.Max(sample.boneRotationTravel[0], sample.boneRotationTravel[1]) / Mathf.Max(.001f, sample.walkingSeconds)).ToString("F1") + "deg/s, max leg rotation=" + sample.maxBoneAngle.ToString("F1") + "deg, relative foot excursion=" + sample.maxRelativeFootShift.ToString("F3") +
                    "m, swing rise vs initial ankle=" + sample.maxSwingRise.ToString("F3") + "m, max stance error=" + sample.maxContactError.ToString("F3") +
                    "m, max reported speed=" + sample.maxReportedSpeed.ToString("F2") + "m/s, input without travel frames=" + sample.inputBlockedFrames +
                    ", suspended/dead frames=" + sample.suspendedFrames + "/" + sample.deadFrames + ", relocations=" + sample.relocations + ". " + sample.state;
                report += "\nPer leg L/R: motion=" + (sample.boneRotationTravel[0] / Mathf.Max(.001f, sample.walkingSeconds)).ToString("F1") + "/" +
                    (sample.boneRotationTravel[1] / Mathf.Max(.001f, sample.walkingSeconds)).ToString("F1") + "deg/s; rotation range=" +
                    sample.maxLegAngle[0].ToString("F1") + "/" + sample.maxLegAngle[1].ToString("F1") + "deg; foot excursion=" +
                    sample.maxFootExcursion[0].ToString("F3") + "/" + sample.maxFootExcursion[1].ToString("F3") + "m.";
                if (sample.issues.Count > 0) Debug.LogError(report + "\nChecks: " + string.Join("; ", sample.issues), sample.hero);
                else Debug.Log(report + (sample.walkingFrames < 20 ? "\nInsufficient walking samples: standing is allowed; record again while this hero moves." :
                    "\nBone/contact diagnostics passed. Inspect the mesh visually as bone motion alone cannot prove correct skin weights."), sample.hero);
            }
            if (recordedFrames < 20 || samples.Count == 0)
                Debug.LogWarning("DF Movement: insufficient rendered samples or no live gameplay heroes. Keep the Game view visible, unpause Play Mode, then run the recorder again.");
            Debug.Log("DF Movement recording finished: " + samples.Count + " heroes, " + recordedFrames + " final rendered frames. Results are diagnostics, not visual acceptance.");
            Destroy(gameObject);
        }
        private void OnDestroy() { EditorApplication.update -= CheckDeadline; }
    }
}
#endif

