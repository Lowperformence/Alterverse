using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Samples the imported Mixamo run on an invisible source skeleton and transfers its
    /// foot trajectories and arm directions to the existing rigid-part hero visuals.
    /// The actor transform, colliders, combat poses and gameplay speed remain authoritative.
    /// </summary>
    public sealed class MixamoRunPose : MonoBehaviour
    {
        private GameObject source;
        private Transform hips, leftFoot, rightFoot, leftArm, rightArm, leftForeArm, rightForeArm;
        private PlayableGraph graph;
        private AnimationClipPlayable[] clipPlayables;
        private AnimationClip[] clips;
        private AnimationMixerPlayable mixer;
        private int activeClip = -1;
        private const int Run = 0, Stop = 1, TurnRight = 2, TurnLeft = 3,
            TurnBack = 4, Melee = 5, Magic = 6, Jump = 7;
        private static readonly string[] ClipNames = {
            "DF_MixamoRun", "DF_MixamoStop", "DF_MixamoTurnRight", "DF_MixamoTurnLeft",
            "DF_MixamoTurnBack", "DF_MixamoMelee", "DF_MixamoMagic", "DF_MixamoJump"
        };
        private bool ready;

        private void Awake()
        {
            GameObject prefab = Resources.Load<GameObject>("DF_MixamoRun");
            if (prefab == null) return;
            clips = new AnimationClip[ClipNames.Length];
            for (int i = 0; i < clips.Length; i++)
                foreach (AnimationClip candidate in Resources.LoadAll<AnimationClip>(ClipNames[i]))
                    if (candidate != null && candidate.length > .2f && !candidate.name.StartsWith("__preview__"))
                    { clips[i] = candidate; break; }
            if (clips[Run] == null) return;

            source = Instantiate(prefab, transform);
            source.name = "DF_RunSource";
            source.transform.localPosition = Vector3.zero;
            source.transform.localRotation = Quaternion.identity;
            source.transform.localScale = Vector3.one;
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            Animator animator = source.GetComponent<Animator>();
            if (animator == null) animator = source.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (Transform bone in source.GetComponentsInChildren<Transform>(true))
            {
                string name = bone.name;
                if (name.EndsWith("Hips")) hips = bone;
                else if (name.EndsWith("LeftFoot")) leftFoot = bone;
                else if (name.EndsWith("RightFoot")) rightFoot = bone;
                else if (name.EndsWith("LeftArm")) leftArm = bone;
                else if (name.EndsWith("RightArm")) rightArm = bone;
                else if (name.EndsWith("LeftForeArm")) leftForeArm = bone;
                else if (name.EndsWith("RightForeArm")) rightForeArm = bone;
            }
            if (hips == null || leftFoot == null || rightFoot == null ||
                leftArm == null || rightArm == null || leftForeArm == null || rightForeArm == null)
            {
                Destroy(source);
                source = null;
                return;
            }

            graph = PlayableGraph.Create("DF_MixamoRun");
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
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Run", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
            ready = true;
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
            if (selected != activeClip)
            {
                mixer.SetInputWeight(activeClip, 0f);
                mixer.SetInputWeight(selected, 1f);
                activeClip = selected;
            }
            float time = attacking ? Mathf.Clamp01(attackProgress) * clips[selected].length
                : stopping ? Mathf.Repeat(phase / (Mathf.PI * 2f), 1f) * clips[selected].length
                : Mathf.Repeat(phase / (Mathf.PI * 2f), 1f) * clips[selected].length;
            clipPlayables[selected].SetTime(time);
            graph.Evaluate(0f);
            weight = attacking ? Mathf.Max(weight, .7f) : stopping ? Mathf.Max(weight, .55f) : weight;

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
            float armWeight = attacking ? weight * .48f : weight;
            TransferArm(armL, armRestL, leftArm, leftForeArm, armWeight);
            TransferArm(armR, armRestR, rightArm, rightForeArm, armWeight);
        }

        private static void TransferFoot(Transform target, Vector3 rest, Quaternion rotation,
            Vector3 sample, float centerZ, float floor, float scale, float weight)
        {
            if (target == null) return;
            Vector3 goal = rest + new Vector3(0f, Mathf.Max(0f, sample.y - floor) * scale,
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
            target.localRotation = Quaternion.Slerp(target.localRotation,
                rest * Quaternion.Euler(angle, 0f, 0f), weight);
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
