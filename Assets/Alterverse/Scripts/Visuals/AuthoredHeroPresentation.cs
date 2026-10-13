using System;
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
        private readonly List<Mesh> rigidWeaponMeshes = new List<Mesh>();
        private Transform model, muzzle, visualRoot, rifleSupport, leftPalm, rifleHandle;
        private Transform timmyTail;
        private Quaternion timmyTailRest;
        private Vector3 timmyTailAxis;
        private float timmyTailTime;
        private Transform rifleDetailsRoot;
        private Transform rifleDetailsGrip;
        private Vector3 rifleDetailsGripPosition;
        private Quaternion rifleDetailsGripRotation;
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
            public Vector3 WeaponCenterLocal;
            public bool HasWeaponCenter;
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
            if (hero == HeroId.Timmy) instance.transform.localScale *= TimmyBalance.ModelScale;
            // A single final pose layer samples Blender clips; an imported Animator must not also write the same bones.
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            RestoreImportedBindPose(instance.transform, hero);
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
            if (hero == HeroId.Kaelon)
            {
                SiegeTankAppearance.Apply(instance.transform);
                AurenForearmSlimmer.Apply(actor);
            }
            if (hero == HeroId.Elyra) UmbralMageAppearance.Apply(instance.transform);
            if (hero == HeroId.Mira)
            {
                TahlaHandSymmetry.Apply(instance.transform);
                HealerLegArmorAppearance.Apply(instance.transform);
                TahlaHelmet.Apply(instance.transform);
                TahlaHalo.Apply(instance.transform);
            }
            if (hero == HeroId.Rook) BrakkWrestlingMask.Apply(instance.transform);
            if (hero == HeroId.Nerok) CyberSharkAppearance.Apply(instance.transform);
            if (hero == HeroId.Traveler)
            {
                ChronoTravelerAppearance.Apply(instance.transform);
                TravelerOrbAppearance.Apply(instance.transform);
            }
            // These two component models already contain their armor and boot details.
            // Keep the legacy passes as a fallback when a replacement did not load.
            bool componentModelApplied =
                (hero == HeroId.Nyra && instance.GetComponent<CyberRangerAppearance>() != null) ||
                (hero == HeroId.Elyra && instance.GetComponent<UmbralMageAppearance>() != null);
            if (hero != HeroId.Timmy && !componentModelApplied)
                HeroLimbTechDetails.Apply(instance.transform, hero);
            SkinOrnamentPresentation.Apply(instance.transform,
                hero == HeroId.Mira ? (Color?)new Color(.52f, .80f, .67f, 1f) : null);
            HeroUndersuitFabric.Apply(instance.transform);
            ApplyHandSize(instance.transform, hero);
            if (hero == HeroId.Nyra) FitSivraRifleGrip(instance.transform);
            // Recolor after every generated detail and material pass so no gold forearm layer remains.
            BrakkForearmTrim.Apply(instance.transform, hero);
            BrakkFistAppearance.Apply(instance.transform, hero);
            if (!componentModelApplied) HeroBootPresentation.Apply(instance.transform, hero);
            BrakkForearmTrim.ApplyLegs(instance.transform, hero);
            BrakkUtilityBeltAppearance.Apply(instance.transform, hero);
            presentation.Initialize(root, instance.transform);
            return true;
        }

        private static void FitSivraRifleGrip(Transform model)
        {
            Transform[] parts = model.GetComponentsInChildren<Transform>(true);
            Transform Find(string name) => Array.Find(parts, p => p.name == name);
            Transform grip = Find("WeaponGripR");
            Transform rightHand = Find("HandR");
            Transform leftHand = Find("HandL");
            Transform handle = Find("DF_HandleContactR");
            Transform support = Find("DF_SupportGrip");
            Transform muzzle = Find("DF_Muzzle");
            Transform leftPalm = Find("DF_PalmGripL");
            if (grip == null || rightHand == null || leftHand == null ||
                handle == null || support == null || muzzle == null || leftPalm == null)
            {
                Debug.LogError("DF: Sivra rifle grip or contact sockets are missing.", model);
                return;
            }
            // The imported contact objects are siblings of the bones. Without this
            // binding they remain at their rest positions when the rifle or hands move.
            handle.SetParent(grip, true);
            support.SetParent(grip, true);
            muzzle.SetParent(grip, true);
            leftPalm.SetParent(leftHand, true);
            leftPalm.position = leftHand.position;

            // The FBX rifle was authored at a separate aiming pose: its trigger
            // contact is away from the rest-pose hand. Fit the weapon itself to the
            // hand before Initialize records the grip's rest position.
            grip.position += rightHand.position - handle.position;
            HeroVisualDiagnostics.Trace("DF: Sivra rifle trigger fitted to the right hand; muzzle and support sockets now follow the weapon.", model);
        }

        // FBX default transforms can contain an exported animation pose. They are NOT
        // necessarily the rest transforms against which the mesh was skinned.
        // Restore from skin bind matrices before cosmetics, grips or retarget offsets are captured.
        internal static void RestoreImportedBindPose(Transform imported, HeroId hero)
        {
            if (hero == HeroId.Nyra) return;
            var targets = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer renderer in imported.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                Matrix4x4[] bind = renderer.sharedMesh.bindposes;
                Transform[] skinBones = renderer.bones;
                for (int i = 0; i < skinBones.Length && i < bind.Length; i++)
                {
                    Transform bone = skinBones[i];
                    if (bone == null || targets.ContainsKey(bone)) continue;
                    bool shield = false;
                    if (hero == HeroId.Kaelon)
                        for (Transform parent = bone; parent != null && parent != imported; parent = parent.parent)
                            if (parent.name == "UpperArmL") { shield = true; break; }
                    if (!shield) targets.Add(bone, renderer.localToWorldMatrix * bind[i].inverse);
                }
            }
            // Capture ALL matrices first; changing a parent must not alter a later skin reference.
            var ordered = new List<Transform>(targets.Keys);
            ordered.Sort((a, b) => BoneDepth(a).CompareTo(BoneDepth(b)));
            foreach (Transform bone in ordered)
            {
                Matrix4x4 local = bone.parent != null
                    ? bone.parent.worldToLocalMatrix * targets[bone] : targets[bone];
                bone.localPosition = local.GetColumn(3);
                bone.localRotation = local.rotation;
                bone.localScale = local.lossyScale;
            }
        }
        private static int BoneDepth(Transform bone)
        {
            int depth = 0;
            while (bone.parent != null) { depth++; bone = bone.parent; }
            return depth;
        }

        // Scale the existing hand skin and Brakk's gauntlets through their shared rig.
        // Counter-scale weapon grip children so held weapons retain their original size.
        // Their positions (and palm sockets) still follow the resized hands. Apply before
        // Initialize captures the grip rest positions and the rifle support solver.
        private static void ApplyHandSize(Transform imported, HeroId hero)
        {
            // Relative to the previous 2x hands: -20% normally, -60% for Brakk.
            // Zhaev/Veyr now also uses 1.6x hands: 60% larger than his previous 1x hands.
            // The palm offset and inverse grip scale below keep the original weapons fitted.
            float scale = hero == HeroId.Mira || hero == HeroId.Nerok || hero == HeroId.Timmy ? 1f : hero == HeroId.Rook ? .8f : 1.6f;
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
            if(hero==HeroId.Timmy)
                foreach(Transform bone in model.GetComponentsInChildren<Transform>(true))
                    if(bone.name=="Tail"&&bone.parent!=null)
                    {
                        timmyTail=bone;timmyTailRest=bone.localRotation;
                        timmyTailAxis=bone.parent.InverseTransformDirection(model.up).normalized;
                        break;
                    }
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
            if (hero == HeroId.Veyr) BuildRigidBlades();
            MeleeBasicAttackAnimation.For(gameObject)?.InitializeRig(model);
            // Capture the same corrected imported rest pose before any frame writes it.
            HeroLocomotionAnimation.For(gameObject, model);
            // The menu preview must start in the same guard before its camera fits.
            if (hero == HeroId.Kaelon) MeleeBasicAttackAnimation.For(gameObject)?.EvaluatePose();
        }

        // Each authored blade is weighted entirely to one WeaponGrip bone. A rigid
        // renderer on that bone preserves the exact fitted geometry and materials,
        // without relying on skinned bounds or an imported bind-pose root for visibility.
        private void BuildRigidBlades()
        {
            for (int i = 0; i < weaponRenderers.Count; i++)
            {
                if (!(weaponRenderers[i] is SkinnedMeshRenderer skin) || skin.sharedMesh == null) continue;
                WeaponGrip grip = grips.Find(g => g.Bone != null && g.Bone.name == "WeaponGrip" + skin.name[skin.name.Length - 1]);
                if (grip == null) continue;
                if (!TryBladeBindMatrix(skin, grip.Bone, out Matrix4x4 toGrip)) continue;
                var mesh = Instantiate(skin.sharedMesh);
                mesh.name = skin.name + "_RigidGrip";
                Matrix4x4 normalMatrix = toGrip.inverse.transpose;
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                for (int v = 0; v < vertices.Length; v++) vertices[v] = toGrip.MultiplyPoint3x4(vertices[v]);
                for (int v = 0; v < normals.Length; v++) normals[v] = normalMatrix.MultiplyVector(normals[v]).normalized;
                mesh.vertices = vertices;
                mesh.normals = normals;
                if (toGrip.determinant < 0f)
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        int[] triangles = mesh.GetTriangles(sub);
                        for (int t = 0; t + 2 < triangles.Length; t += 3)
                        { int swap = triangles[t + 1]; triangles[t + 1] = triangles[t + 2]; triangles[t + 2] = swap; }
                        mesh.SetTriangles(triangles, sub);
                    }
                mesh.RecalculateBounds();
                if (mesh.uv.Length == mesh.vertexCount) mesh.RecalculateTangents();
                var blade = new GameObject(skin.name);
                blade.layer = skin.gameObject.layer;
                blade.transform.SetParent(grip.Bone, false);
                blade.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = blade.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = skin.sharedMaterials;
                renderer.shadowCastingMode = skin.shadowCastingMode;
                renderer.receiveShadows = skin.receiveShadows;
                skin.enabled = false;
                skin.name += "_SkinSource";
                weaponRenderers[i] = renderer;
                rigidWeaponMeshes.Add(mesh);
                HeroVisualDiagnostics.Trace("DF: Zhaev blade bind-space v2: " + blade.name + " / " + mesh.vertexCount +
                    " vertices / local size " + mesh.bounds.size, this);
            }
        }

        private static bool TryBladeBindMatrix(SkinnedMeshRenderer skin, Transform grip, out Matrix4x4 toGrip)
        {
            toGrip = Matrix4x4.identity;
            Mesh source = skin.sharedMesh;
            if (source == null || !source.isReadable) return false;
            int index = System.Array.IndexOf(skin.bones, grip);
            Matrix4x4[] bind = source.bindposes;
            if (index < 0 || index >= bind.Length) return false;
            // Both crescent blades are authored as rigid, 100%-weighted grip meshes.
            // Validate that contract before converting the source to a MeshRenderer.
            foreach (BoneWeight weight in source.boneWeights)
                if (weight.boneIndex0 != index || weight.weight0 < .999f ||
                    weight.weight1 > .001f || weight.weight2 > .001f || weight.weight3 > .001f)
                    return false;
            toGrip = bind[index];
            return true;
        }

        private static Matrix4x4 RelativeMatrix(Transform part, Transform ancestor)
        {
            Matrix4x4 result = Matrix4x4.identity;
            for (Transform current = part; current != ancestor; current = current.parent)
            {
                if (current == null) throw new System.InvalidOperationException("Blade palm is outside its hand hierarchy.");
                result = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * result;
            }
            return result;
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
            if (hero == HeroId.Veyr)
            {
                Transform palm = null;
                foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                    if (part.name == "DF_PalmGrip" + side) { palm = part; break; }
                foreach (Renderer candidate in weaponRenderers)
                    if (candidate != null && candidate.name == "DF_Weapon" + side && candidate is SkinnedMeshRenderer skin)
                        FitVeyrGoldEnd(skin, target, palm);
            }
            var grip = new WeaponGrip { Bone = target, Control = control,
                RestLocal = target.localRotation, RestPosition = target.localPosition };
            foreach (Renderer candidate in weaponRenderers)
            {
                if (candidate == null || candidate.name != "DF_Weapon" + side) continue;
                // Cache the skinned mesh centre in grip space once. Renderer.bounds can
                // lag a bone pose, which would make a live clearance correction oscillate.
                if (candidate is SkinnedMeshRenderer skin)
                {
                    if (hero == HeroId.Veyr && TryBladeBindMatrix(skin, target, out Matrix4x4 toGrip))
                    {
                        Vector3[] vertices = skin.sharedMesh.vertices;
                        if (vertices.Length > 0)
                        {
                            Bounds bounds = new Bounds(toGrip.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                            foreach (Vector3 vertex in vertices) bounds.Encapsulate(toGrip.MultiplyPoint3x4(vertex));
                            grip.WeaponCenterLocal = bounds.center;
                            grip.HasWeaponCenter = true;
                        }
                        break;
                    }
                    var baked = new Mesh();
                    skin.BakeMesh(baked);
                    if (baked.vertexCount > 0)
                    {
                        grip.WeaponCenterLocal = target.InverseTransformPoint(skin.transform.TransformPoint(baked.bounds.center));
                        grip.HasWeaponCenter = true;
                    }
                    Destroy(baked);
                }
                break;
            }
            grips.Add(grip);
        }

        // Use the actual gold end piece selected in the reference, not the old mid-wrap socket.
        // Bind-space coordinates avoid BakeMesh and world-position precision entirely.
        private static void FitVeyrGoldEnd(SkinnedMeshRenderer skin, Transform grip, Transform palm)
        {
            if (palm == null || grip.parent == null || !TryBladeBindMatrix(skin, grip, out Matrix4x4 toGrip)) return;
            Mesh source = skin.sharedMesh;
            Vector3[] vertices = source.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = toGrip.MultiplyPoint3x4(vertices[i]);
            int[] parents = new int[vertices.Length];
            bool[] gold = new bool[vertices.Length];
            for (int i = 0; i < parents.Length; i++) parents[i] = i;
            int Root(int index)
            {
                while (parents[index] != index) { parents[index] = parents[parents[index]]; index = parents[index]; }
                return index;
            }
            Material[] materials = skin.sharedMaterials;
            for (int sub = 0; sub < source.subMeshCount && sub < materials.Length; sub++)
            {
                if (materials[sub] == null || !materials[sub].name.Contains("Champagne metal")) continue;
                int[] triangles = source.GetTriangles(sub);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    gold[a] = gold[b] = gold[c] = true;
                    parents[Root(b)] = Root(a); parents[Root(c)] = Root(a);
                }
            }
            // FBX import splits vertices at UV/material and hard-normal seams.
            // Join coincident gold vertices before finding connected pieces; otherwise
            // a single triangle can be mistaken for the marked terminal handle.
            var welded = new Dictionary<Vector3Int, int>();
            Vector3Int Key(Vector3 point) => new Vector3Int(
                Mathf.RoundToInt(point.x * 100000f), Mathf.RoundToInt(point.y * 100000f),
                Mathf.RoundToInt(point.z * 100000f));
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!gold[i]) continue;
                Vector3Int key = Key(vertices[i]);
                if (welded.TryGetValue(key, out int same)) parents[Root(i)] = Root(same);
                else welded.Add(key, i);
            }
            var bounds = new Dictionary<int, Bounds>();
            var sums = new Dictionary<int, Vector3>();
            var counts = new Dictionary<int, int>();
            foreach (int i in welded.Values)
            {
                int root = Root(i);
                if (!bounds.TryGetValue(root, out Bounds part))
                { part = new Bounds(vertices[i], Vector3.zero); sums[root] = Vector3.zero; counts[root] = 0; }
                part.Encapsulate(vertices[i]); bounds[root] = part;
                sums[root] += vertices[i]; counts[root]++;
            }
            // The wide terminal gold piece is larger than each narrow grip-wrap strip.
            int selected = -1; float largest = 0f;
            foreach (var part in bounds)
                if (part.Value.size.sqrMagnitude > largest)
                { largest = part.Value.size.sqrMagnitude; selected = part.Key; }
            if (selected >= 0)
            {
                Matrix4x4 gripToHand = Matrix4x4.TRS(grip.localPosition, grip.localRotation, grip.localScale);
                Vector3 end = gripToHand.MultiplyPoint3x4(sums[selected] / counts[selected]);
                Vector3 palmInHand = RelativeMatrix(palm, grip.parent).MultiplyPoint3x4(Vector3.zero);
                grip.localPosition += palmInHand - end;
                foreach (Transform socket in grip.GetComponentsInChildren<Transform>(true))
                    if (socket.name == "DF_HandleContact" + grip.name[grip.name.Length - 1])
                        socket.localPosition = RelativeMatrix(socket.parent, grip.parent).inverse.MultiplyPoint3x4(palmInHand);
            }
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
            // Skinned weapons cancel FBX bone scale through their bind poses. Static
            // mesh attachments do not: parenting them under a centimetre-scaled bone
            // can enlarge the details 100x and cover the entire gameplay camera.
            Vector3 forward = muzzle != null && rifleSupport != null ?
                muzzle.position - rifleSupport.position : model.forward;
            Vector3 up = rifleSupport != null && rifleHandle != null ?
                Vector3.ProjectOnPlane(rifleSupport.position - rifleHandle.position, forward) : model.up;
            if (forward.sqrMagnitude < .000001f) forward = model.forward;
            if (up.sqrMagnitude < .000001f) up = model.up;
            Vector3 origin = rifleSupport != null ? rifleSupport.position : rifle.bounds.center;

            rifleDetailsRoot = new GameObject("DF_SivraRifleDetails").transform;
            rifleDetailsRoot.SetParent(model, false);
            rifleDetailsRoot.SetPositionAndRotation(origin, Quaternion.LookRotation(forward, up));
            rifleDetailsGrip = grip.Bone;
            rifleDetailsGripPosition = grip.Bone.InverseTransformPoint(origin);
            rifleDetailsGripRotation = Quaternion.Inverse(grip.Bone.rotation) * rifleDetailsRoot.rotation;
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
                // Retain the FBX import's root scale and normalize its complete mesh
                // envelope in the attachment frame. Never assume FBX units are metres.
                if (!FitRifleDetails(attachment.transform, rifleDetailsRoot))
                {
                    attachment.SetActive(false);
                    Destroy(attachment);
                    Debug.LogError("DF: Sivra rifle details have invalid imported bounds.", this);
                    return;
                }
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
                    HeroVisualDiagnostics.Trace("DF: Sivra Blender rifle attached to WeaponGripR (3 mesh layers).", this);
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
            HeroVisualDiagnostics.Trace("DF: Sivra rifle details attached to WeaponGripR (" +
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

        private static bool FitRifleDetails(Transform attachment, Transform frame)
        {
            if (attachment == null || frame == null) return false;
            Bounds envelope = default;
            bool found = false;
            foreach (MeshFilter filter in attachment.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                Matrix4x4 toFrame = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 p = toFrame.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                                float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)) return false;
                            if (!found) { envelope = new Bounds(p, Vector3.zero); found = true; }
                            else envelope.Encapsulate(p);
                        }
            }
            if (!found || envelope.size.z < .00001f) return false;
            // Blender design spans -0.147 to +0.7475 metres along the barrel.
            float scale = .8945f / envelope.size.z;
            attachment.localScale *= scale;
            return true;
        }

        private void FollowRifleDetails()
        {
            if (rifleDetailsRoot == null || rifleDetailsGrip == null) return;
            // Follow the final two-hand pose, copying position/rotation only.
            rifleDetailsRoot.SetPositionAndRotation(rifleDetailsGrip.TransformPoint(rifleDetailsGripPosition),
                rifleDetailsGrip.rotation * rifleDetailsGripRotation);
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
                // The grip is a child of the hand. Keep its fitted Blender handle contact
                // fixed even while the hand is repositioned by the combat pose solver.
                grip.Bone.localRotation = grip.RestLocal;
                grip.Bone.localPosition = grip.RestPosition;
            }
        }

        private void ClearIdleWeaponOverlap(bool buildingGuard = false)
        {
            if (model == null || !buildingGuard && motion != null && motion.ArmActionActive) return;
            // Only the idle pose is adjusted. A strike owns its wrist and weapon arc.
            var attack = GetComponent<MeleeBasicAttackAnimation>();
            if (!buildingGuard && attack != null && attack.StrikeActive) return;
            if (hero == HeroId.Nyra || hero == HeroId.Rook || hero == HeroId.Kaelon) return;
            for (int i = 0; i < 2; i++)
            {
                ArmChain arm = i == 0 ? leftArm : rightArm;
                if (arm == null) continue;
                bool left = i == 0;
                if (hero == HeroId.Kaelon && left) continue; // The shield keeps its own left-hand orientation.
                WeaponGrip grip = null;
                string gripName = left ? "WeaponGripL" : "WeaponGripR";
                foreach (WeaponGrip candidate in grips)
                    if (candidate.Bone != null && candidate.Bone.name == gripName) { grip = candidate; break; }
                if (grip == null || !grip.HasWeaponCenter) continue;
                Vector3 weaponCenter = grip.Bone.TransformPoint(grip.WeaponCenterLocal);
                Vector3 shaft = weaponCenter - arm.Hand.position;
                if (shaft.sqrMagnitude < .0025f) continue; // No reliable shaft direction on a compact prop.
                float side = left ? -1f : 1f;
                Vector3 hand = model.InverseTransformPoint(arm.Hand.position);
                Vector3 center = model.InverseTransformPoint(weaponCenter);
                float torsoEdge = Mathf.Abs(model.InverseTransformPoint(arm.Upper.position).x) * .84f;
                // The mesh centre must clear the torso; correct the wrist, never the grip.
                if (side * center.x > torsoEdge + .08f && center.y < hand.y + .10f) continue;
                Vector3 desired = model.TransformDirection(new Vector3(side * .48f, -.86f, .12f));
                arm.Hand.rotation = Quaternion.FromToRotation(shaft, desired) * arm.Hand.rotation;
            }
        }

        private Vector3 GuardHint(ArmChain arm)
        {
            if (arm == null) return Vector3.zero;
            return model.InverseTransformPoint(arm.Elbow.position) + new Vector3(0f, -.03f, .015f);
        }

        private void ApplyModelGuard()
        {
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            // The final combat layer owns all non-rifle guards. Rifle grips are authored sockets.
            if (hero != HeroId.Nyra && GetComponent<MeleeBasicAttackAnimation>()?.OwnsPose == true &&
                (motion == null || !motion.AbilityActionActive)) return;
            // Rifle fire keeps both grips; cast actions retain their established delivery.
            float targetWeight = hero == HeroId.Nyra || motion == null || !motion.ArmActionActive ? 1f : 0f;
            guardWeight = Mathf.MoveTowards(guardWeight, targetWeight, Time.deltaTime * 14f);
            // Coordinates include each imported model's proportions. Elbows stay down and
            // outside the torso; actions blend back into the established arm animation.
            switch (hero)
            {
                case HeroId.Nyra:
                {
                    // Keep the trigger hand close to the chest so both elbows can
                    // bend around the rifle instead of extending above a low gun.
                    bool aiming = motion != null && motion.ArmActionActive;
                    Vector3 shoulder = model.InverseTransformPoint(rightArm.Upper.position);
                    SolveGuard(rightArm, aiming ? new Vector3(.14f, 1.28f, .27f) :
                        new Vector3(.20f, 1.12f, .18f),
                        shoulder + new Vector3(.28f, -.28f, .12f), guardWeight);
                    break;
                }
                case HeroId.Veyr:
                    SolveGuard(leftArm, new Vector3(-.35f, 1.01f, .04f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(.35f, 1.01f, .04f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Kaelon:
                    ApplyAurenGuard(leftArm, true);
                    ApplyAurenGuard(rightArm, false);
                    break;
                case HeroId.Elyra:
                    SolveGuard(leftArm, new Vector3(-.34f, 1.04f, .04f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(.34f, 1.04f, .04f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Mira:
                    SolveGuard(leftArm, new Vector3(-.34f, .99f, .04f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(.34f, .99f, .04f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Rook:
                    SolveGuard(leftArm, new Vector3(-.36f, 1.00f, .04f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(.36f, 1.00f, .04f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Nerok:
                    SolveGuard(leftArm, new Vector3(-.36f, 1.00f, .04f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(.37f, 1.00f, .04f), GuardHint(rightArm), guardWeight);
                    break;
                case HeroId.Traveler:
                    SolveGuard(leftArm, new Vector3(-.35f, .89f, .04f), GuardHint(leftArm), guardWeight);
                    SolveGuard(rightArm, new Vector3(.35f, .89f, .04f), GuardHint(rightArm), guardWeight);
                    break;
            }
        }

        private void ApplyAurenGuard(ArmChain arm, bool shield)
        {
            if (arm == null || model == null) return;
            Vector3 point = SiegeTankAppearance.GuardHandTarget(model, arm.Upper, arm.Elbow, arm.Hand, shield);
            SolveGuard(arm, model.InverseTransformPoint(point), GuardHint(arm), guardWeight);
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
            Vector3 aim = model.forward - model.up * (motion != null && motion.ArmActionActive ? 0f : .12f);
            Quaternion desired = Quaternion.LookRotation(aim, model.up);
            rightArm.Hand.rotation = desired * Quaternion.Inverse(current) * rightArm.Hand.rotation;
        }

        private void ApplyRifleSupport()
        {
            if (hero != HeroId.Nyra || rifleSupport == null || leftArm == null || leftPalm == null) return;
            // Hold the foregrip in idle as well as during fire. Previously this
            // solver returned in idle, leaving her left arm extended above the gun.
            Vector3 palmOffset = leftPalm.position - leftArm.Hand.position;
            Vector3 wrist = model.InverseTransformPoint(rifleSupport.position - palmOffset);
            Vector3 shoulder = model.InverseTransformPoint(leftArm.Upper.position);
            Vector3 elbowHint = shoulder + new Vector3(-.27f, -.30f, .17f);
            SolveGuard(leftArm, wrist, elbowHint, guardWeight);
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in rigidWeaponMeshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in rifleDetailMaterials)
                if (material != null) Destroy(material);
        }

        public void FinalizeMeleeGrip() => FinalizeCombatGrip();
        public void FinalizeGuardGrip()
        {
            ApplyWeaponGrips();
            ClearIdleWeaponOverlap(true);
        }
        public void FinalizeCombatGrip(bool adjustIdleWrists = true)
        {
            ApplyWeaponGrips();
            // Shield remains left-facing; the hammer wrist belongs to its strike trajectory.
            if (siegeTank != null) siegeTank.AlignGuard(transform, leftArm?.Hand, rightArm?.Hand, 0f);
            ApplyRifleAim();
            ApplyRifleSupport();
            if (adjustIdleWrists) ClearIdleWeaponOverlap();
            FollowRifleDetails();
        }
        private void LateUpdate()
        {
            // HeroVisualMotion (930) -> HeroProceduralRig (935) -> this coordinate conversion (940).
            // There are no old visible meshes and no independent Animator writing the same bones.
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            var melee=GetComponent<MeleeBasicAttackAnimation>();
            bool ownsBasicPose=melee!=null&&melee.OwnsPose && (motion==null||!motion.AbilityActionActive);
            foreach (Binding binding in bindings)
            {
                if (binding.Bone == null || binding.Control == null) continue;
                if(ownsBasicPose &&
                    (binding.Bone.name.Contains("Arm")||binding.Bone.name.StartsWith("Forearm")||binding.Bone.name.Contains("Hand")))
                {binding.Bone.localRotation=binding.Rest;continue;}
                Quaternion delta = binding.PoseOffset * Quaternion.Inverse(binding.ControlRest) * binding.Control.localRotation;
                if (binding.Extra != null) delta *= Quaternion.Inverse(binding.ExtraRest) * binding.Extra.localRotation;
                binding.Bone.localRotation = binding.Rest * Quaternion.Inverse(binding.Basis) * delta * binding.Basis;
            }
            // Pelvis motion belongs to the same source pose as the legs. Apply it
            // before hand contacts and model-sibling weapon details are positioned.
            GetComponent<HeroGroundedLocomotion>()?.PrepareSourcePelvis();
            ApplyModelGuard();
            if (GetComponent<MeleeBasicAttackAnimation>()?.OwnsPose != true) ApplyBrawlerPunch();
            ApplyWeaponGrips();
            if (siegeTank != null) siegeTank.AlignGuard(transform, leftArm?.Hand, rightArm?.Hand, guardWeight);
            if (umbralMage != null) umbralMage.AlignGuard(model, leftArm?.Hand, rightArm?.Hand, guardWeight);
            ApplyRifleAim();
            ApplyRifleSupport();
            if (!ownsBasicPose) ClearIdleWeaponOverlap();
            FollowRifleDetails();
            // Final pose must run after these writes, independent of .meta/editor execution-order overrides.
            if (melee != null && melee.isActiveAndEnabled) melee.EvaluatePose();
            // Authored legs run after the combat layer's guard resets and foot-preserving solve.
            // Grounding then corrects source contacts instead of replacing the source gait.
            GetComponent<HeroLocomotionAnimation>()?.ApplyLegs();
            GetComponent<HeroGroundedLocomotion>()?.FinalizeFeet();
            FollowRifleDetails();
            if(timmyTail!=null&&Time.deltaTime>0f)
            {
                // Like Nerok, begin centred and animate only the tail, independent of combat.
                timmyTailTime+=Time.deltaTime;
                timmyTail.localRotation=Quaternion.AngleAxis(TimmyBalance.TailSwayDegrees*
                    Mathf.Sin(timmyTailTime*Mathf.PI*2f/TimmyBalance.TailSwaySeconds),timmyTailAxis)*timmyTailRest;
            }
        }
    }
}

