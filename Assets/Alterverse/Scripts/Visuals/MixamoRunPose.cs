using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Samples the imported Mixamo motion clips on an invisible template-specific skeleton and
    /// transfers the resulting pose to the existing hero presentation. Gameplay movement,
    /// hitboxes and root motion remain authoritative on the real actor.
    /// </summary>
    public sealed class MixamoRunPose : MonoBehaviour
    {
        private const string TemplateFolder = "AlterverseRigTemplates/";

        private static readonly Dictionary<HeroId, string> HeroTemplates = new()
        {
            { HeroId.Veyr, TemplateFolder + "AV_Rig_Assassin" },
            { HeroId.Kaelon, TemplateFolder + "AV_Rig_Tank" },
            { HeroId.Nyra, TemplateFolder + "AV_Rig_Soldier" },
            { HeroId.Elyra, TemplateFolder + "AV_Rig_Mage" },
            { HeroId.Mira, TemplateFolder + "AV_Rig_Mage" },
            { HeroId.Rook, TemplateFolder + "AV_Rig_Brakk" },
            { HeroId.Traveler, TemplateFolder + "AV_Rig_Traveler" }
        };

        private static readonly HashSet<string> MissingTemplateWarnings = new();

        private GameObject source;
        private HeroArticulatedJointRig articulatedRig;
        private Transform hips, leftFoot, rightFoot, leftArm, rightArm, leftForeArm, rightForeArm;
        private Transform leftUpLeg, rightUpLeg, leftLegBone, rightLegBone;
        private Quaternion bindLeftFoot, bindRightFoot, bindLeftForeArm, bindRightForeArm;
        private Quaternion bindLeftUpLeg, bindRightUpLeg, bindLeftLeg, bindRightLeg;
        private float bindThighL, bindThighR, bindShinL, bindShinR;
        private PlayableGraph graph;
        private AnimationClipPlayable[] clipPlayables;
        private AnimationClip[] clips;
        private AnimationMixerPlayable mixer;
        private int activeClip = -1, fadingClip = -1;
        private float blendStarted, blendDuration;
        private string activeTemplate = "DF_MixamoRun";

        private const int Run = 0, Stop = 1, TurnRight = 2, TurnLeft = 3,
            TurnBack = 4, Melee = 5, Magic = 6, Jump = 7;

        private static readonly string[] ClipNames =
        {
            "DF_MixamoRun", "DF_MixamoStop", "DF_MixamoTurnRight", "DF_MixamoTurnLeft",
            "DF_MixamoTurnBack", "DF_MixamoMelee", "DF_MixamoMagic", "DF_MixamoJump"
        };

        private bool ready;

        public string ActiveTemplate => activeTemplate;
        public bool Ready => ready;
        public bool Articulated => articulatedRig != null && articulatedRig.Ready;

        private void Awake()
        {
            Transform visualRoot = transform.Find("DF_VisualRoot");
            HeroIdentity identity = GetComponent<HeroIdentity>();
            GameObject templatePrefab = ResolveTemplatePrefab(identity);

            clips = new AnimationClip[ClipNames.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                foreach (AnimationClip candidate in Resources.LoadAll<AnimationClip>(ClipNames[i]))
                {
                    if (candidate == null || candidate.length <= .2f ||
                        candidate.name.StartsWith("__preview__")) continue;
                    clips[i] = candidate;
                    break;
                }
            }

            if (clips[Run] == null) return;

            if (templatePrefab != null)
            {
                source = Instantiate(templatePrefab, transform);
            }
            else if (identity != null)
            {
                source = TemplateRigSkeletonData.BuildForHero(identity.Hero, transform, out activeTemplate);
            }
            else
            {
                GameObject fallbackPrefab = Resources.Load<GameObject>("DF_MixamoRun");
                if (fallbackPrefab != null)
                    source = Instantiate(fallbackPrefab, transform);
            }

            if (source == null) return;

            source.name = "DF_RunSource_" + activeTemplate;
            source.transform.localPosition = Vector3.zero;
            source.transform.localRotation = Quaternion.identity;
            source.transform.localScale = Vector3.one;

            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            Animator animator = source.GetComponent<Animator>();
            if (animator == null) animator = source.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            ResolveBones();
            if (hips == null || leftFoot == null || rightFoot == null ||
                leftArm == null || rightArm == null || leftForeArm == null || rightForeArm == null)
            {
                Debug.LogWarning(
                    $"[Alterverse] Template rig '{activeTemplate}' is missing required Mixamo bones. " +
                    "Falling back to procedural hero motion.", this);
                Destroy(source);
                source = null;
                return;
            }

            CaptureBindPose();

            // Only restructure the visible hero after a valid motion source exists. A missing
            // FBX/clip therefore leaves the legacy presentation completely untouched.
            if (visualRoot != null && leftUpLeg != null && rightUpLeg != null && leftLegBone != null && rightLegBone != null)
            {
                articulatedRig = GetComponent<HeroArticulatedJointRig>() ?? gameObject.AddComponent<HeroArticulatedJointRig>();
                articulatedRig.Build(visualRoot);
            }

            graph = PlayableGraph.Create("DF_MixamoPose_" + activeTemplate);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, ClipNames.Length);
            clipPlayables = new AnimationClipPlayable[ClipNames.Length];

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                clipPlayables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                clipPlayables[i].SetApplyFootIK(false);
                graph.Connect(clipPlayables[i], 0, mixer, i);
                mixer.SetInputWeight(i, i == Run ? 1f : 0f);
            }

            activeClip = Run;
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "TemplateRig", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
            graph.Evaluate(0f);
            ready = true;
        }

        private GameObject ResolveTemplatePrefab(HeroIdentity identity)
        {
            if (identity == null || !HeroTemplates.TryGetValue(identity.Hero, out string resourceName))
                return null;

            GameObject template = Resources.Load<GameObject>(resourceName);
            if (template != null)
            {
                activeTemplate = resourceName.Substring(resourceName.LastIndexOf('/') + 1);
                return template;
            }

            if (MissingTemplateWarnings.Add(resourceName))
                Debug.Log($"[Alterverse] Using embedded template skeleton for {identity.DisplayName} ({resourceName}).");

            return null;
        }

        private void ResolveBones()
        {
            foreach (Transform bone in source.GetComponentsInChildren<Transform>(true))
            {
                string name = bone.name;
                if (name.EndsWith("Hips")) hips = bone;
                else if (name.EndsWith("LeftUpLeg")) leftUpLeg = bone;
                else if (name.EndsWith("RightUpLeg")) rightUpLeg = bone;
                else if (name.EndsWith("LeftLeg")) leftLegBone = bone;
                else if (name.EndsWith("RightLeg")) rightLegBone = bone;
                else if (name.EndsWith("LeftFoot")) leftFoot = bone;
                else if (name.EndsWith("RightFoot")) rightFoot = bone;
                else if (name.EndsWith("LeftArm")) leftArm = bone;
                else if (name.EndsWith("RightArm")) rightArm = bone;
                else if (name.EndsWith("LeftForeArm")) leftForeArm = bone;
                else if (name.EndsWith("RightForeArm")) rightForeArm = bone;
            }
        }

        private void CaptureBindPose()
        {
            bindLeftUpLeg = LocalRotation(leftUpLeg);
            bindRightUpLeg = LocalRotation(rightUpLeg);
            bindLeftLeg = LocalRotation(leftLegBone);
            bindRightLeg = LocalRotation(rightLegBone);
            bindLeftFoot = LocalRotation(leftFoot);
            bindRightFoot = LocalRotation(rightFoot);
            bindLeftForeArm = LocalRotation(leftForeArm);
            bindRightForeArm = LocalRotation(rightForeArm);
            bindThighL = SegmentPitch(leftUpLeg, leftLegBone);
            bindThighR = SegmentPitch(rightUpLeg, rightLegBone);
            bindShinL = SegmentPitch(leftLegBone, leftFoot);
            bindShinR = SegmentPitch(rightLegBone, rightFoot);
        }

        public void Apply(float phase, float weight, float turnRate, bool stopping,
            bool attacking, bool magicAttack, bool jumpAttack, float attackProgress,
            Transform visualRoot,
            Transform bootL, Vector3 restL, Quaternion rotationL,
            Transform bootR, Vector3 restR, Quaternion rotationR,
            Transform legL, Transform legR, Transform armL, Quaternion armRestL,
            Transform armR, Quaternion armRestR)
        {
            if (!ready || visualRoot == null || (weight <= .001f && !stopping && !attacking)) return;

            int selected = attacking ? (jumpAttack ? Jump : magicAttack ? Magic : Melee)
                : stopping ? Stop : Mathf.Abs(turnRate) > 155f ? TurnBack
                : turnRate > 55f ? TurnRight : turnRate < -55f ? TurnLeft : Run;

            if (clips[selected] == null) selected = Run;
            BeginBlend(selected, attacking, stopping);

            float time = Mathf.Repeat(phase / (Mathf.PI * 2f), 1f) * clips[activeClip].length;
            if (attacking)
                time = Mathf.Clamp01(attackProgress) * clips[activeClip].length;

            clipPlayables[activeClip].SetTime(time);
            UpdateBlend();
            graph.Evaluate(0f);

            weight = attacking ? Mathf.Max(weight, .7f) : stopping ? Mathf.Max(weight, .55f) : weight;

            if (Articulated)
            {
                // Read segment directions in the source root's coordinates. Mixamo's local
                // bone axes and the procedural presentation joints do not share a bind pose.
                float thighL = SegmentPitch(leftUpLeg, leftLegBone) - bindThighL;
                float thighR = SegmentPitch(rightUpLeg, rightLegBone) - bindThighR;
                float shinL = SegmentPitch(leftLegBone, leftFoot) - bindShinL;
                float shinR = SegmentPitch(rightLegBone, rightFoot) - bindShinR;
                Quaternion elbowL = Quaternion.Euler(Mathf.Clamp(Mathf.DeltaAngle(0f, Delta(leftForeArm, bindLeftForeArm).eulerAngles.x), -65f, 65f), 0f, 0f);
                Quaternion elbowR = Quaternion.Euler(Mathf.Clamp(Mathf.DeltaAngle(0f, Delta(rightForeArm, bindRightForeArm).eulerAngles.x), -65f, 65f), 0f, 0f);
                articulatedRig.SetTemplatePose(weight,
                    Pitch(thighL, 48f), Pitch(shinL - thighL, 85f), Pitch(-shinL, 32f),
                    Pitch(thighR, 48f), Pitch(shinR - thighR, 85f), Pitch(-shinR, 32f),
                    elbowL, Quaternion.Slerp(Quaternion.identity, elbowL, .24f),
                    elbowR, Quaternion.Slerp(Quaternion.identity, elbowR, .24f));
            }
            else
            {
                Vector3 hip = source.transform.InverseTransformPoint(hips.position);
                Vector3 footA = source.transform.InverseTransformPoint(leftFoot.position);
                Vector3 footB = source.transform.InverseTransformPoint(rightFoot.position);
                float sourceLeg = Mathf.Max(.1f, hip.y - (footA.y + footB.y) * .5f);
                float scale = .72f / sourceLeg;
                float centerZ = (footA.z + footB.z) * .5f;
                float floor = Mathf.Min(footA.y, footB.y);

                TransferFoot(bootL, restL, rotationL, footA, centerZ, floor, scale, weight);
                TransferFoot(bootR, restR, rotationR, footB, centerZ, floor, scale, weight);
                PoseLeg(legL, bootL, restL.x);
                PoseLeg(legR, bootR, restR.x);
            }

            float armWeight = attacking ? weight * .48f : weight;
            TransferArm(armL, armRestL, leftArm, leftForeArm, armWeight);
            TransferArm(armR, armRestR, rightArm, rightForeArm, armWeight);
        }

        private void BeginBlend(int selected, bool attacking, bool stopping)
        {
            if (selected == activeClip) return;

            if (fadingClip >= 0)
                mixer.SetInputWeight(fadingClip, 0f);

            fadingClip = activeClip;
            activeClip = selected;
            blendStarted = Time.time;
            blendDuration = attacking ? .065f : stopping ? .09f : .12f;

            if (fadingClip >= 0) mixer.SetInputWeight(fadingClip, 1f);
            mixer.SetInputWeight(activeClip, fadingClip >= 0 ? 0f : 1f);
        }

        private void UpdateBlend()
        {
            if (fadingClip < 0)
            {
                mixer.SetInputWeight(activeClip, 1f);
                return;
            }

            float progress = Mathf.Clamp01((Time.time - blendStarted) / Mathf.Max(.01f, blendDuration));
            float smooth = Mathf.SmoothStep(0f, 1f, progress);
            mixer.SetInputWeight(fadingClip, 1f - smooth);
            mixer.SetInputWeight(activeClip, smooth);
            if (progress < 1f) return;

            mixer.SetInputWeight(fadingClip, 0f);
            mixer.SetInputWeight(activeClip, 1f);
            fadingClip = -1;
        }

        private static Quaternion LocalRotation(Transform bone) => bone != null ? bone.localRotation : Quaternion.identity;

        private float SegmentPitch(Transform upper, Transform lower)
        {
            if (upper == null || lower == null) return 0f;
            Vector3 direction = source.transform.InverseTransformDirection(lower.position - upper.position);
            return Mathf.Atan2(direction.z, -direction.y) * Mathf.Rad2Deg;
        }

        private static Quaternion Pitch(float degrees, float limit) =>
            Quaternion.Euler(Mathf.Clamp(Mathf.DeltaAngle(0f, degrees), -limit, limit), 0f, 0f);

        private static Quaternion Delta(Transform bone, Quaternion bind)
        {
            if (bone == null) return Quaternion.identity;
            return Quaternion.Inverse(bind) * bone.localRotation;
        }

        private static void TransferFoot(Transform target, Vector3 rest, Quaternion rotation,
            Vector3 sample, float centerZ, float floor, float scale, float weight)
        {
            if (target == null) return;
            Vector3 goal = rest + new Vector3(
                0f,
                Mathf.Max(0f, sample.y - floor) * scale,
                (sample.z - centerZ) * scale);
            target.localPosition = Vector3.Lerp(target.localPosition, goal, weight);
            target.localRotation = Quaternion.Slerp(target.localRotation, rotation, weight * .4f);
        }

        private void TransferArm(Transform target, Quaternion rest, Transform shoulder,
            Transform elbow, float weight)
        {
            if (target == null) return;
            Vector3 direction = source.transform.InverseTransformDirection(elbow.position - shoulder.position);
            float angle = Mathf.Clamp(Mathf.Atan2(-direction.z, -direction.y) * Mathf.Rad2Deg, -65f, 65f);
            target.localRotation = Quaternion.Slerp(
                target.localRotation,
                rest * Quaternion.Euler(angle, 0f, 0f),
                weight);
        }

        private static void PoseLeg(Transform leg, Transform foot, float side)
        {
            if (leg == null || foot == null) return;
            Vector3 hip = new Vector3(side, -.26f, 0f);
            Vector3 ankle = foot.localPosition + Vector3.up * .08f;
            Vector3 axis = ankle - hip;
            leg.localPosition = (hip + ankle) * .5f;
            leg.localRotation = Quaternion.FromToRotation(Vector3.down, axis);
            Vector3 scale = leg.localScale;
            scale.y = axis.magnitude * .5f;
            leg.localScale = scale;
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (source != null) Destroy(source);
        }
    }
}
