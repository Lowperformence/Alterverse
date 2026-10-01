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
            HeroIdentity identity = GetComponent<HeroIdentity>();
            if (identity != null && identity.Hero == HeroId.Nyra)
            {
                // The old ranger motion assumes a pre-authored shoulder carry. Recreate that
                // bind-pose difference while retaining its existing recoil and small elbow movement.
                if (boneName == "UpperArmL") offset = Quaternion.Euler(-58, 28, 0);
                if (boneName == "UpperArmR") offset = Quaternion.Euler(-62, -8, 0);
                if (boneName == "HandL") offset = Quaternion.Euler(82, -28, 0);
                if (boneName == "HandR") offset = Quaternion.Euler(80, 8, 0);
            }
            bindings.Add(new Binding { Bone = target, Control = control, Extra = extra,
                PoseOffset = offset, Rest = target.localRotation, Basis = Quaternion.Inverse(visualRoot.rotation) * target.rotation,
                ControlRest = control.localRotation, ExtraRest = extra != null ? extra.localRotation : Quaternion.identity });
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
        }
    }
}
