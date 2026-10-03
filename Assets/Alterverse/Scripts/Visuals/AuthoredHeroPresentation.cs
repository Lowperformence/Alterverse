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
        private Transform model, muzzle, visualRoot, rifleSupport, leftPalm, rifleHandle;
        private Transform rifleDetailsRoot;
        private readonly List<Material> rifleDetailMaterials = new List<Material>();
        private sealed class ArmChain
        {
            public Transform Upper, Elbow, Hand;
            public Quaternion RestHandLocal;
        }
        private sealed class WeaponGrip
        {
            public Transform Bone, Control;
            public Quaternion RestLocal;
            public Vector3 RestPosition;
        }
        private readonly List<WeaponGrip> grips = new List<WeaponGrip>();
        private ArmChain leftArm, rightArm;
        private HeroId hero;
        private HeroVisualMotion motion;
        private SiegeTankAppearance siegeTank;
        private UmbralMageAppearance umbralMage;
        private float guardWeight;
        public IReadOnlyList<Renderer> WeaponRenderers => weaponRenderers;
        public Vector3 ReleasePoint => muzzle != null ? muzzle.position : transform.position + Vector3.up * .35f;
        public Transform Control(string name) => controls.TryGetValue(name, out Transform part) ? part : null;

        public static bool TryCreate(GameObject actor, HeroId hero, out Transform root)
        {
            root = null;
            // Repeated Build calls reuse the existing rig and its pose controls.
            if (actor.TryGetComponent<AuthoredHeroPresentation>(out var existing) &&
                existing.visualRoot != null && existing.model != null)
            {
                root = existing.visualRoot;
                return true;
            }
            GameObject asset = Resources.Load<GameObject>("AuthoredHeroes/" + (hero == HeroId.Nerok ? HeroId.Rook : hero));
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
            if (hero == HeroId.Veyr)
            {
                AssassinVioletHeadStripe.Apply(instance.transform);
                AssassinShoulderStripes.Apply(instance.transform);
            }
            if (hero == HeroId.Nyra)
            {
                CyberRangerAppearance.Apply(instance.transform);
                if (instance.GetComponent<CyberRangerAppearance>() == null)
                    Debug.LogError("DF: Sivra's Cyber Ranger mesh was not applied; inspect Model.bytes and its bone mapping.", actor);
            }
            if (hero == HeroId.Kaelon) SiegeTankAppearance.Apply(instance.transform);
            if (hero == HeroId.Elyra) UmbralMageAppearance.Apply(instance.transform);
            if (hero == HeroId.Mira)
            {
                TahlaHandSymmetry.Apply(instance.transform);
                HealerLegArmorAppearance.Apply(instance.transform);
                TahlaHalo.Apply(instance.transform);
            }
            if (hero == HeroId.Rook) BrakkWrestlingMask.Apply(instance.transform);
            if (hero == HeroId.Nerok) CyberSharkAppearance.Apply(instance.transform);
            if (hero == HeroId.Traveler)
            {
                ChronoTravelerAppearance.Apply(instance.transform);
                TravelerOrbAppearance.Apply(instance.transform);
            }
            HeroLimbTechDetails.Apply(instance.transform, hero);
            SkinOrnamentPresentation.Apply(instance.transform);
            HeroUndersuitFabric.Apply(instance.transform);
            ApplyHandSize(instance.transform, hero);
            presentation.Initialize(root, instance.transform);
            return true;
        }

        // Scale the existing hand skin and Brakk's gauntlets through their shared rig.
        // Counter-scale weapon grip children so held weapons retain their original size.
        // Their positions (and palm sockets) still follow the resized hands. Apply before
        // Initialize captures the grip rest positions and the rifle support solver.
        private static void ApplyHandSize(Transform imported, HeroId hero)
        {
            // Relative to the previous 2x hands: -20% normally, -60% for Brakk.
            float scale = hero == HeroId.Mira ? 1f : hero == HeroId.Nerok ? 1f : hero == HeroId.Rook ? .8f : 1.6f;
            Transform[] bones = imported.GetComponentsInChildren<Transform>(true);
            foreach (Transform grip in bones)
            {
                if (grip.name != "WeaponGripL" && grip.name != "WeaponGripR") continue;
                Transform hand = grip.parent;
                if (hand == null || hand.name != "Hand" + grip.name[grip.name.Length - 1]) continue;
                foreach (Transform palm in bones)
                    if (palm.name == "DF_PalmGrip" + grip.name[grip.name.Length - 1])
                    {
                        // The contact offset remains rigid inside the unscaled weapon.
                        // Move the rigid weapon contact onto the resized palm for any scale.
                        Vector3 contact = hand.InverseTransformPoint(palm.position);
                        grip.localPosition = (grip.localPosition + contact * (scale - 1f)) / scale;
                        break;
                    }
            }
            foreach (Transform bone in bones)
                if (bone.name == "HandL" || bone.name == "HandR")
                    bone.localScale *= scale;
            foreach (Transform bone in bones)
                if (bone.name == "WeaponGripL" || bone.name == "WeaponGripR")
                    bone.localScale /= scale;
            foreach (SkinnedMeshRenderer renderer in imported.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                var baked = new Mesh();
                renderer.BakeMesh(baked);
                Bounds bounds = renderer.localBounds;
                bounds.Encapsulate(baked.bounds.min);
                bounds.Encapsulate(baked.bounds.max);
                renderer.localBounds = bounds;
                Destroy(baked);
            }
        }

        private void Initialize(Transform root, Transform imported)
        {
            visualRoot = root;
            model = imported;
            siegeTank = imported.GetComponent<SiegeTankAppearance>();
            umbralMage = imported.GetComponent<UmbralMageAppearance>();
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
                {
                if (bone.name == "DF_Muzzle") muzzle = bone;
                if (bone.name == "DF_SupportGrip") rifleSupport = bone;
                if (bone.name == "DF_PalmGripL") leftPalm = bone;
                if (bone.name == "DF_HandleContactR") rifleHandle = bone;
            }
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
                Bind("Hand" + side, "WristJoint" + side);
                BindWeaponGrip(side);
                Bind("Thigh" + side, "Leg" + side);
                Bind("Shin" + side, "KneeJoint" + side);
                Bind("Foot" + side, "AnkleJoint" + side);
            }
            if (hero == HeroId.Nyra && (rightArm == null || leftArm == null ||
                muzzle == null || rifleSupport == null || rifleHandle == null || leftPalm == null))
                Debug.LogError("DF: Sivra rifle rig is incomplete. Check HandR/HandL and the muzzle, support, handle and palm sockets.", this);
            BuildNyraWeaponDetails();
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
                    RestHandLocal = hand.localRotation }
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
            Quaternion upperBefore = arm.Upper.rotation, elbowBefore = arm.Elbow.rotation, handBefore = arm.Hand.localRotation;
            arm.Upper.rotation = Quaternion.FromToRotation(arm.Elbow.position - shoulder, elbowTarget - shoulder) * arm.Upper.rotation;
            arm.Elbow.rotation = Quaternion.FromToRotation(arm.Hand.position - arm.Elbow.position, target - arm.Elbow.position) * arm.Elbow.rotation;
            Quaternion solvedUpper = arm.Upper.rotation, solvedElbow = arm.Elbow.rotation;
            arm.Upper.rotation = Quaternion.Slerp(upperBefore, solvedUpper, weight);
            arm.Elbow.rotation = Quaternion.Slerp(elbowBefore, solvedElbow, weight);
            // A neutral local wrist follows the forearm instead of bending back to a world-space pose.
            arm.Hand.localRotation = Quaternion.Slerp(handBefore, arm.RestHandLocal, weight);
        }

        private void BindWeaponGrip(string side)
        {
            Transform target = null;
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
                if (bone.name == "WeaponGrip" + side) { target = bone; break; }
            Transform control = Control("Weapon" + side);
            if (target == null || control == null)
            {
                Debug.LogError("DF: weapon grip bone missing: WeaponGrip" + side, this);
                return;
            }
            grips.Add(new WeaponGrip { Bone = target, Control = control,
                RestLocal = target.localRotation, RestPosition = target.localPosition });
        }

        private void BuildNyraWeaponDetails()
        {
            if (hero != HeroId.Nyra) return;
            WeaponGrip grip = grips.Find(g => g.Bone != null && g.Bone.name == "WeaponGripR");
            Renderer rifle = weaponRenderers.Find(r => r != null && r.name == "DF_WeaponR");
            if (grip == null || rifle == null)
            {
                Debug.LogError("DF: Sivra rifle details cannot attach: WeaponGripR bone or DF_WeaponR renderer is missing.", this);
                return;
            }
            // The imported rifle is skinned to WeaponGripR, which cancels the enlarged
            // hand scale. Details must use that same bone or they drift into the receiver.
            Vector3 forward = muzzle != null && rifleSupport != null ?
                muzzle.position - rifleSupport.position : model.forward;
            Vector3 up = rifleSupport != null && rifleHandle != null ?
                Vector3.ProjectOnPlane(rifleSupport.position - rifleHandle.position, forward) : model.up;
            if (forward.sqrMagnitude < .000001f) forward = model.forward;
            if (up.sqrMagnitude < .000001f) up = model.up;
            Vector3 origin = rifleSupport != null ? rifleSupport.position : rifle.bounds.center;

            rifleDetailsRoot = new GameObject("DF_SivraRifleDetails").transform;
            rifleDetailsRoot.SetParent(grip.Bone, false);
            rifleDetailsRoot.SetPositionAndRotation(origin, Quaternion.LookRotation(forward, up));
            GameObject authoredDetails = Resources.Load<GameObject>("CyberRanger/SivraRifleDetails");
            if (authoredDetails != null)
            {
                Material titanium = RuntimeShaderLibrary.Create(new Color(.33f, .45f, .48f), false, .82f, .72f, Color.black);
                Material graphite = RuntimeShaderLibrary.Create(new Color(.026f, .043f, .052f), false, .52f, .52f, Color.black);
                Color lineColor = new Color(.025f, .72f, .94f);
                Material emissive = RuntimeShaderLibrary.Create(lineColor, false, .24f, .82f, lineColor * 1.8f);
                if (titanium == null || graphite == null || emissive == null)
                {
                    Debug.LogError("DF: Sivra rifle shaders are unavailable.", this);
                    if (titanium != null) Destroy(titanium);
                    if (graphite != null) Destroy(graphite);
                    if (emissive != null) Destroy(emissive);
                    return;
                }
                emissive.EnableKeyword("_EMISSION");
                titanium.name = "Sivra rifle titanium";
                graphite.name = "Sivra rifle graphite";
                emissive.name = "Sivra rifle cyan circuits";
                rifleDetailMaterials.Add(titanium);
                rifleDetailMaterials.Add(graphite);
                rifleDetailMaterials.Add(emissive);
                GameObject attachment = Instantiate(authoredDetails, rifleDetailsRoot, false);
                attachment.name = "DF_SivraRifleAttachment";
                attachment.transform.localPosition = Vector3.zero;
                attachment.transform.localRotation = Quaternion.identity;
                attachment.transform.localScale = Vector3.one;
                int meshCount = 0;
                foreach (MeshRenderer part in attachment.GetComponentsInChildren<MeshRenderer>(true))
                {
                    part.sharedMaterial = part.name.Contains("CyanEmission") ? emissive :
                        part.name.Contains("Graphite") ? graphite : titanium;
                    meshCount++;
                }
                if (meshCount != 3)
                    Debug.LogError("DF: Sivra rifle FBX imported " + meshCount + " mesh layers instead of 3.", this);
                else
                    Debug.Log("DF: Sivra Blender rifle attached to WeaponGripR (3 mesh layers).", this);
                return;
            }
            Debug.LogError("DF: CyberRanger/SivraRifleDetails FBX is missing; showing emergency rifle details.", this);
            Material source = rifle.sharedMaterial;
            Shader shader = source != null ? source.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("DF: Sivra rifle detail material has no usable shader.", this);
                return;
            }
            Material metal = source != null ? new Material(source) : new Material(shader);
            SetDetailColor(metal, new Color(.30f, .38f, .42f), false);
            Material cyan = source != null ? new Material(source) : new Material(shader);
            SetDetailColor(cyan, new Color(.04f, .78f, .92f), true);
            Material dark = source != null ? new Material(source) : new Material(shader);
            SetDetailColor(dark, new Color(.035f, .065f, .075f), false);
            rifleDetailMaterials.Add(metal);
            rifleDetailMaterials.Add(cyan);
            rifleDetailMaterials.Add(dark);

            // Local +Z follows the barrel, +Y is the rifle's top, and X spans its sides.
            DetailBox("Receiver cheek L", new Vector3(-.063f, .025f, .055f), new Vector3(.016f, .072f, .150f), metal);
            DetailBox("Receiver cheek R", new Vector3(.063f, .025f, .055f), new Vector3(.016f, .072f, .150f), metal);
            DetailBox("Optic base", new Vector3(0f, .093f, .072f), new Vector3(.060f, .020f, .105f), metal);
            DetailBox("Optic housing", new Vector3(0f, .118f, .082f), new Vector3(.060f, .038f, .105f), dark);
            DetailBox("Optic lens", new Vector3(0f, .120f, .139f), new Vector3(.048f, .030f, .008f), cyan);
            DetailBox("Power line L", new Vector3(-.067f, .004f, .218f), new Vector3(.009f, .014f, .295f), cyan);
            DetailBox("Power line R", new Vector3(.067f, .004f, .218f), new Vector3(.009f, .014f, .295f), cyan);
            DetailBox("Barrel collar", new Vector3(0f, 0f, .390f), new Vector3(.058f, .054f, .032f), metal);
            for (int i = 0; i < 3; i++)
            {
                float z = .020f + i * .033f;
                DetailBox("Cooling slot L " + i, new Vector3(-.073f, .032f, z), new Vector3(.004f, .009f, .023f), dark);
                DetailBox("Cooling slot R " + i, new Vector3(.073f, .032f, z), new Vector3(.004f, .009f, .023f), dark);
            }
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 2; i++)
                    DetailBox("Receiver fastener " + side + " " + i,
                        new Vector3(side * .073f, .058f, .005f + i * .110f),
                        new Vector3(.008f, .010f, .010f), dark);
            Debug.Log("DF: Sivra rifle details attached to WeaponGripR (" +
                rifleDetailsRoot.childCount + " pieces).", this);
        }

        private void DetailBox(string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "DF_SivraRifle_" + name;
            part.transform.SetParent(rifleDetailsRoot, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = size;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void SetDetailColor(Material material, Color color, bool emissive)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 1.8f);
            }
        }

        private void ApplyWeaponGrips()
{
            foreach (WeaponGrip grip in grips)
            {
                if (grip.Bone == null || grip.Control == null) continue;
                // The fitted handle and the palm share one rigid frame. Independent world
                // rotation previously pulled the handle out of the fingers during movement.
                grip.Bone.localRotation = grip.RestLocal;
                grip.Bone.localPosition = grip.RestPosition;
                if(hero == HeroId.Traveler && grip.Bone.name == "WeaponGripR")
                    grip.Bone.position += model.TransformVector(new Vector3(0,.10f + Mathf.Sin(Time.time*2.2f)*.015f,0));
            }
        }

        private Vector3 GuardHint(ArmChain arm)
        {
            if (arm == null) return Vector3.zero;
            return model.InverseTransformPoint(arm.Upper.position)
                + new Vector3(arm == leftArm ? -1f : 1f, -.40f, .10f);
        }

        private void ApplyModelGuard()
        {
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            // Rifle fire keeps both grips; melee/cast actions retain their established delivery.
            float targetWeight = hero == HeroId.Nyra || motion == null || !motion.ArmActionActive ? 1f : 0f;
            guardWeight = Mathf.MoveTowards(guardWeight, targetWeight, Time.deltaTime * 14f);
            // Coordinates include each imported model's proportions. Elbows stay down and
            // outside the torso; actions blend back into the established arm animation.
            switch (hero)
            {
                case HeroId.Nyra:
                    SolveGuard(rightArm, new Vector3(.11f, 1.30f, .205f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Veyr:
                    SolveGuard(leftArm, new Vector3(-0.239700f, 1.122000f, 0.206800f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(0.239700f, 1.122000f, 0.206800f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Kaelon:
                    SolveGuard(leftArm, new Vector3(-0.339662f, 1.134000f, 0.260000f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(0.357009f, 1.166400f, 0.234000f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Elyra:
                    SolveGuard(leftArm, new Vector3(-0.222500f, 1.199000f, 0.193200f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(0.347100f, 1.199000f, 0.147200f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Mira:
                    SolveGuard(leftArm, new Vector3(-0.222927f, 1.101600f, 0.153600f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(0.258100f, 1.101600f, 0.172800f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Rook:
                    SolveGuard(leftArm, new Vector3(-0.368000f, 1.237600f, 0.364000f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(0.368000f, 1.237600f, 0.364000f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Nerok:
                    SolveGuard(leftArm, new Vector3(-.33f, 1.07f, .20f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(.36f, 1.04f, .23f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Traveler:
                    // Lower, relaxed hands keep the upper arms away from the hood.
                    SolveGuard(leftArm, new Vector3(-.32f, .89f, .18f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(.33f, .93f, .22f), GuardHint(rightArm), guardWeight);
                    break;
            }
        }

        private void ApplyBrawlerPunch()
        {
            if (hero != HeroId.Rook || motion == null || leftArm == null || rightArm == null ||
                !motion.TryGetBrawlerPunch(out float extension, out float draw, out bool right)) return;
            foreach (ArmChain arm in new[] { leftArm, rightArm })
            {
                float side = arm == leftArm ? -1f : 1f;
                bool striking = (arm == rightArm) == right;
                Vector3 shoulder = model.InverseTransformPoint(arm.Upper.position);
                float reach = (model.InverseTransformPoint(arm.Elbow.position) - shoulder).magnitude +
                    (model.InverseTransformPoint(arm.Hand.position) - model.InverseTransformPoint(arm.Elbow.position)).magnitude;
                Vector3 guard = shoulder + new Vector3(-side * .025f, -.23f, .28f);
                Vector3 target = striking ? Vector3.Lerp(guard - model.InverseTransformVector(model.forward * (.08f * draw)),
                    shoulder + new Vector3(-side * .035f, -.08f, reach * .96f), extension) : guard;
                SolveGuard(arm, target, shoulder + new Vector3(side * .38f, -.45f, .08f), 1f);
            }
        }

        private void ApplyRifleAim()
        {
            if (hero != HeroId.Nyra || rightArm == null || muzzle == null ||
                rifleSupport == null || rifleHandle == null) return;
            // Three authored contact points define the barrel and its roll without
            // assuming FBX bone axes. Rotate the hand and rifle together, never the
            // weapon independently of its trigger grip.
            Vector3 forward = muzzle.position - rifleSupport.position;
            Vector3 up = Vector3.ProjectOnPlane(rifleSupport.position - rifleHandle.position, forward);
            if (forward.sqrMagnitude < .000001f || up.sqrMagnitude < .000001f) return;
            Quaternion current = Quaternion.LookRotation(forward, up);
            Vector3 aim = model.forward - model.up * (motion != null && motion.ArmActionActive ? 0f : .06f);
            Quaternion desired = Quaternion.LookRotation(aim, model.up);
            rightArm.Hand.rotation = desired * Quaternion.Inverse(current) * rightArm.Hand.rotation;
        }

        private void ApplyRifleSupport()
        {
            if (hero != HeroId.Nyra || rifleSupport == null || leftArm == null || leftPalm == null) return;
            // The support socket is on the rifle, so the left palm tracks its actual foregrip.
            // Four short corrections account for the palm's offset from the wrist joint.
            for (int i = 0; i < 4; i++)
            {
                Vector3 palmOffset = leftPalm.position - leftArm.Hand.position;
                Vector3 wrist = model.InverseTransformPoint(rifleSupport.position - palmOffset);
                SolveGuard(leftArm, wrist, GuardHint(leftArm), guardWeight);
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in rifleDetailMaterials)
                if (material != null) Destroy(material);
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
            ApplyBrawlerPunch();
            ApplyWeaponGrips();
            if (siegeTank != null) siegeTank.AlignGuard(transform, leftArm?.Hand, rightArm?.Hand, guardWeight);
            if (umbralMage != null) umbralMage.AlignGuard(model, leftArm?.Hand, rightArm?.Hand, guardWeight);
            ApplyRifleAim();
            ApplyRifleSupport();
        }
    }
}

