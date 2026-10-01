using System.Collections.Generic;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Retargets the existing hero movement onto the imported skeleton; owns no alternate animation state machine.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(940)]
    public sealed class AuthoredHeroPresentation : MonoBehaviour
    {
        private sealed class Binding
        {
            public Transform Bone, Control, Extra;
            public Quaternion Rest, Basis, ControlRest, ExtraRest, PoseOffset;
        }
        private readonly Dictionary<string, Transform> controls = new Dictionary<string, Transform>();
        private readonly List<Binding> bindings = new List<Binding>();
        private readonly List<Renderer> weaponRenderers = new List<Renderer>();
        private Transform model, muzzle, visualRoot;
        private sealed class ArmChain
        {
            public Transform Upper, Elbow, Hand;
            public Quaternion RestHandInModel;
        }
        private ArmChain leftArm, rightArm;
        private HeroId hero;
        private HeroVisualMotion motion;
        private float guardWeight;
        public IReadOnlyList<Renderer> WeaponRenderers => weaponRenderers;
        public Vector3 ReleasePoint => muzzle != null ? muzzle.position : transform.position + Vector3.up * .35f;
        public Transform Control(string name) => controls.TryGetValue(name, out Transform part) ? part : null;

        public static bool TryCreate(GameObject actor, HeroId hero, out Transform root)
        {
            root = null;
            GameObject asset = Resources.Load<GameObject>("AuthoredHeroes/" + hero);
            if (asset == null) return false;
            root = new GameObject("DF_VisualRoot").transform;
            root.SetParent(actor.transform, false);
            root.localPosition = new Vector3(0, .32f, 0);
            root.localScale = new Vector3(1.22f, 1.32f, 1.22f);
            GameObject instance = Instantiate(asset, root, false);
            instance.name = "DF_AuthoredModel";
            instance.transform.localPosition = new Vector3(0, -.96f, 0);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = new Vector3(1.10f, 1f, 1.10f);
            // The user chose the established movement. Never let imported clips compete with it.
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            if (!actor.TryGetComponent<AuthoredHeroPresentation>(out var presentation))
                presentation = actor.AddComponent<AuthoredHeroPresentation>();
            presentation.Initialize(root, instance.transform);
            return true;
        }

        private void Initialize(Transform root, Transform imported)
        {
            visualRoot = root;
            model = imported;
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
                if (bone.name == "DF_Muzzle") muzzle = bone;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "DF_WeaponL" || renderer.name == "DF_WeaponR" || renderer.name == "DF_GauntletL" || renderer.name == "DF_GauntletR")
                    weaponRenderers.Add(renderer);
            hero = GetComponent<HeroIdentity>().Hero;
            leftArm = CaptureArm("L"); rightArm = CaptureArm("R");
            BuildControls();
            Bind("Hips", "PelvisJoint");
            Bind("Spine", "SpineLower");
            Bind("Chest", "SpineUpper", "Torso");
            Bind("Head", "NeckJoint", "Head");
            foreach (string side in new[] { "L", "R" })
            {
                Bind("UpperArm" + side, "Arm" + side);
                Bind("Forearm" + side, "ElbowJoint" + side);
                Bind("Hand" + side, "WristJoint" + side, "Weapon" + side);
                Bind("Thigh" + side, "Leg" + side);
                Bind("Shin" + side, "KneeJoint" + side);
                Bind("Foot" + side, "AnkleJoint" + side);
            }
        }

        private Transform Make(string name, Transform parent, Vector3 rootPosition)
        {
            Transform part = new GameObject("DF_" + name).transform;
            part.SetParent(parent, false);
            part.position = visualRoot.TransformPoint(rootPosition);
            part.rotation = visualRoot.rotation;
            controls.Add(name, part);
            return part;
        }

        private void BuildControls()
        {
            Transform host = new GameObject("DF_LegacyPoseControls").transform;
            host.SetParent(visualRoot, false);
            Transform pelvis = Make("PelvisJoint", host, new Vector3(0, -.35f, 0));
            Make("Pelvis", pelvis, new Vector3(0, -.35f, 0));
            Transform lower = Make("SpineLower", pelvis, new Vector3(0, -.13f, 0));
            Transform upper = Make("SpineUpper", lower, new Vector3(0, .16f, 0));
            Make("Torso", upper, new Vector3(0, .16f, 0));
            Transform neck = Make("NeckJoint", upper, new Vector3(0, .61f, 0));
            Make("Head", neck, new Vector3(0, .83f, 0));
            foreach (string side in new[] { "L", "R" })
            {
                float sign = side == "L" ? -1 : 1;
                Transform arm = Make("Arm" + side, upper, new Vector3(sign * .34f, .43f, .03f));
                Transform elbow = Make("ElbowJoint" + side, arm, new Vector3(sign * .40f, .13f, .04f));
                Transform wrist = Make("WristJoint" + side, elbow, new Vector3(sign * .42f, -.15f, .12f));
                Make("Weapon" + side, wrist, new Vector3(sign * .42f, -.15f, .12f));
                Make("Gauntlet" + side, wrist, new Vector3(sign * .42f, -.15f, .12f));
                Transform hip = Make("Leg" + side, pelvis, new Vector3(sign * .18f, -.35f, 0));
                Transform knee = Make("KneeJoint" + side, hip, new Vector3(sign * .18f, -.62f, 0));
                Transform ankle = Make("AnkleJoint" + side, knee, new Vector3(sign * .18f, -.88f, .04f));
                Make("Boot" + side, ankle, new Vector3(sign * .18f, -.91f, .09f));
            }
        }

        private void Bind(string boneName, string controlName, string extraName = null)
        {
            Transform target = null;
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
                if (bone.name == boneName) { target = bone; break; }
            Transform control = Control(controlName);
            if (target == null || control == null)
            {
                Debug.LogError("DF: imported hero retarget binding missing: " + boneName, this);
                return;
            }
            Transform extra = extraName != null ? Control(extraName) : null;
            Quaternion offset = Quaternion.identity;
            bindings.Add(new Binding { Bone = target, Control = control, Extra = extra,
                PoseOffset = offset, Rest = target.localRotation, Basis = Quaternion.Inverse(visualRoot.rotation) * target.rotation,
                ControlRest = control.localRotation, ExtraRest = extra != null ? extra.localRotation : Quaternion.identity });
        }

        private ArmChain CaptureArm(string side)
        {
            Transform upper = null, elbow = null, hand = null;
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "UpperArm" + side) upper = bone;
                if (bone.name == "Forearm" + side) elbow = bone;
                if (bone.name == "Hand" + side) hand = bone;
            }
            return upper != null && elbow != null && hand != null
                ? new ArmChain { Upper = upper, Elbow = elbow, Hand = hand,
                    RestHandInModel = Quaternion.Inverse(model.rotation) * hand.rotation }
                : null;
        }

        // Solve the guard against actual shoulder/elbow/hand positions. Old guard angles
        // assumed different arm lengths and cannot define a grip on the imported anatomy.
        private void SolveGuard(ArmChain arm, Vector3 targetInModel, Vector3 hintInModel, float weight)
        {
            if (arm == null || weight <= .001f) return;
            Vector3 shoulder = arm.Upper.position;
            Vector3 target = model.TransformPoint(targetInModel);
            Vector3 hint = model.TransformPoint(hintInModel);
            float upperLength = Vector3.Distance(shoulder, arm.Elbow.position);
            float lowerLength = Vector3.Distance(arm.Elbow.position, arm.Hand.position);
            if (upperLength < .001f || lowerLength < .001f) return;
            Vector3 toTarget = target - shoulder;
            float distance = Mathf.Clamp(toTarget.magnitude,
                Mathf.Abs(upperLength - lowerLength) + .001f, upperLength + lowerLength - .001f);
            if (toTarget.sqrMagnitude < .000001f) return;
            Vector3 direction = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(hint - shoulder, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(model.right, direction);
            bend.Normalize();
            float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 elbowTarget = shoulder + direction * along + bend * height;
            target = shoulder + direction * distance;
            Quaternion upperBefore = arm.Upper.rotation, elbowBefore = arm.Elbow.rotation, handBefore = arm.Hand.rotation;
            arm.Upper.rotation = Quaternion.FromToRotation(arm.Elbow.position - shoulder, elbowTarget - shoulder) * arm.Upper.rotation;
            arm.Elbow.rotation = Quaternion.FromToRotation(arm.Hand.position - arm.Elbow.position, target - arm.Elbow.position) * arm.Elbow.rotation;
            Quaternion solvedUpper = arm.Upper.rotation, solvedElbow = arm.Elbow.rotation;
            arm.Upper.rotation = Quaternion.Slerp(upperBefore, solvedUpper, weight);
            arm.Elbow.rotation = Quaternion.Slerp(elbowBefore, solvedElbow, weight);
            // Preserve authored grip orientation instead of accumulating the old wrist twists.
            arm.Hand.rotation = Quaternion.Slerp(handBefore, model.rotation * arm.RestHandInModel, weight);
        }

        private void ApplyModelGuard()
        {
            if (hero != HeroId.Veyr && hero != HeroId.Traveler && hero != HeroId.Nyra) return;
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            // Rifle fire keeps both grips; melee/cast actions retain their established delivery.
            float targetWeight = hero == HeroId.Nyra || motion == null || !motion.ArmActionActive ? 1f : 0f;
            guardWeight = Mathf.MoveTowards(guardWeight, targetWeight, Time.deltaTime * 14f);
            if (hero == HeroId.Nyra)
            {
                SolveGuard(rightArm, new Vector3(.12f, 1.30f, .18f), new Vector3(.36f, 1.04f, -.02f), guardWeight);
                SolveGuard(leftArm, new Vector3(.15f, 1.31f, .32f), new Vector3(-.27f, 1.03f, .15f), guardWeight);
            }
            else if (hero == HeroId.Veyr)
            {
                SolveGuard(leftArm, new Vector3(-.24f, 1.12f, .22f), new Vector3(-.38f, 1.06f, .02f), guardWeight);
                SolveGuard(rightArm, new Vector3(.24f, 1.12f, .22f), new Vector3(.38f, 1.06f, .02f), guardWeight);
            }
            else
            {
                SolveGuard(leftArm, new Vector3(-.23f, 1.10f, .12f), new Vector3(-.35f, 1.06f, -.03f), guardWeight);
                SolveGuard(rightArm, new Vector3(.22f, 1.16f, .22f), new Vector3(.35f, 1.08f, .02f), guardWeight);
            }
        }

        private void LateUpdate()
        {
            // HeroVisualMotion (930) -> HeroProceduralRig (935) -> this coordinate conversion (940).
            // There are no old visible meshes and no independent Animator writing the same bones.
            foreach (Binding binding in bindings)
            {
                if (binding.Bone == null || binding.Control == null) continue;
                Quaternion delta = binding.PoseOffset * Quaternion.Inverse(binding.ControlRest) * binding.Control.localRotation;
                if (binding.Extra != null) delta *= Quaternion.Inverse(binding.ExtraRest) * binding.Extra.localRotation;
                binding.Bone.localRotation = binding.Rest * Quaternion.Inverse(binding.Basis) * delta * binding.Basis;
            }
            ApplyModelGuard();
        }
    }
}
