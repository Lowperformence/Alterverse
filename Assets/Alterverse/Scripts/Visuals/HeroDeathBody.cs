using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Alterverse.Visuals
{
    /// <summary>A passive ragdoll in an isolated physics scene. Never drives gameplay bones or collisions.</summary>
    public sealed class HeroDeathBody : MonoBehaviour
    {
        public const float CollapseSeconds = 1.1f;
        public const float HoldSeconds = 1f;
        public const float FadeSeconds = 1.7f;
        private const int MaximumBodies = 12;
        private static readonly List<HeroDeathBody> bodies = new();
        private static readonly RaycastHit[] groundHits = new RaycastHit[64];
        private static readonly int FadeId = Shader.PropertyToID("_DFDeathFade");
        private readonly List<Renderer> renderers = new();
        private readonly List<BodyPart> parts = new();
        private readonly List<Collider> ragdollColliders = new();
        private readonly List<int> materialCounts = new();
        private MaterialPropertyBlock block;
        private Vector3 startPosition, groundPoint, groundNormal = Vector3.up;
        private float elapsed;
        private Scene isolatedScene;
        private PhysicsScene physicsWorld;
        private Scene ownerScene;
        private static int nextScene;
        private bool quitting;

        // Native Unity objects must be created on the main thread after AddComponent.
        private void Awake() => block = new MaterialPropertyBlock();

        private sealed class BodyPart
        {
            public Transform Bone;
            public Rigidbody Body;
            public BodyPart Parent;
            public float Swing, Twist, LowTwist;
        }

        public static void Spawn(GameObject actor)
        {
#if UNITY_SERVER
            return;
#else
            if (actor == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Transform visual = actor.transform.Find("DF_VisualRoot");
            if (visual == null) return;
            bool visible = false;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) { visible = true; break; }
            if (!visible) return;

            // Bound transient meshes/renderers even if many heroes die simultaneously.
            bodies.RemoveAll(body => body == null);
            while (bodies.Count >= MaximumBodies)
            {
                HeroDeathBody oldest = bodies[0];
                bodies.RemoveAt(0);
                if (oldest != null) { oldest.gameObject.SetActive(false); Destroy(oldest.gameObject); }
            }

            GameObject host = new GameObject("DF_DeathBody");
            SceneManager.MoveGameObjectToScene(host, actor.scene);
            HeroDeathBody body = host.AddComponent<HeroDeathBody>();
            body.ownerScene = actor.scene;
            host.transform.rotation = actor.transform.rotation;
            var copies = new Dictionary<Transform, Transform>();
            Transform root = CopyHierarchy(visual, host.transform, copies, true);
            foreach (var pair in copies)
                body.CopyRenderer(pair.Key, pair.Value, copies);

            Transform hips = Find(root, "Hips") ?? Find(root, "DF_PelvisJoint");
            Vector3 pivot = hips != null ? hips.position : actor.transform.position;
            root.SetParent(null, true);
            host.transform.position = pivot;
            root.SetParent(host.transform, true);
            body.startPosition = pivot;
            body.FindGround(actor);
            body.isolatedScene = SceneManager.CreateScene("DF_DeathPhysics_" + (++nextScene),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            body.physicsWorld = body.isolatedScene.GetPhysicsScene();
            SceneManager.MoveGameObjectToScene(host, body.isolatedScene);
            SceneManager.sceneUnloaded += body.OnSceneUnloaded;
            if (!body.BuildRagdoll(root, actor))
            {
                Debug.LogError("DF: Hero skeleton is incomplete for a death ragdoll.", actor);
                Destroy(host);
                return;
            }
            bodies.Add(body);
#endif
        }

        private static Transform CopyHierarchy(Transform source, Transform parent,
            Dictionary<Transform, Transform> copies, bool top = false)
        {
            GameObject copy = new GameObject(source.name) { layer = source.gameObject.layer };
            Transform target = copy.transform;
            if (top)
            {
                target.SetPositionAndRotation(source.position, source.rotation);
                target.localScale = source.lossyScale;
                target.SetParent(parent, true);
            }
            else
            {
                target.SetParent(parent, false);
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
            }
            copies.Add(source, target);
            for (int i = 0; i < source.childCount; i++) CopyHierarchy(source.GetChild(i), target, copies);
            copy.SetActive(source.gameObject.activeSelf);
            return target;
        }

        private void CopyRenderer(Transform source, Transform target, Dictionary<Transform, Transform> copies)
        {
            Renderer original = source.GetComponent<Renderer>();
            if (original == null || !original.enabled || !original.gameObject.activeInHierarchy) return;
            Renderer copy;
            if (original is SkinnedMeshRenderer skin)
            {
                if (skin.sharedMesh == null) return;
                var result = target.gameObject.AddComponent<SkinnedMeshRenderer>();
                result.sharedMesh = skin.sharedMesh;
                Transform[] originalBones = skin.bones;
                Transform[] bones = new Transform[originalBones.Length];
                for (int i = 0; i < bones.Length; i++)
                    if (originalBones[i] != null && copies.TryGetValue(originalBones[i], out Transform bone)) bones[i] = bone;
                result.bones = bones;
                if (skin.rootBone != null && copies.TryGetValue(skin.rootBone, out Transform rootBone)) result.rootBone = rootBone;
                result.localBounds = skin.localBounds;
                result.quality = skin.quality;
                result.updateWhenOffscreen = true;
                copy = result;
            }
            else if (original is MeshRenderer)
            {
                MeshFilter mesh = source.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) return;
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                copy = target.gameObject.AddComponent<MeshRenderer>();
            }
            else return; // Particle trails and gameplay effects do not survive the hero.
            copy.sharedMaterials = original.sharedMaterials;
            copy.shadowCastingMode = original.shadowCastingMode;
            copy.receiveShadows = original.receiveShadows;
            copy.renderingLayerMask = original.renderingLayerMask;
            original.GetPropertyBlock(block);
            block.SetFloat(FadeId, 0f);
            copy.SetPropertyBlock(block);
            // Keep per-submesh skin/weapon color overrides too.
            for (int i = 0; i < original.sharedMaterials.Length; i++)
            {
                block.Clear();
                original.GetPropertyBlock(block, i);
                if (!block.isEmpty) { block.SetFloat(FadeId, 0f); copy.SetPropertyBlock(block, i); }
            }
            renderers.Add(copy);
            materialCounts.Add(original.sharedMaterials.Length);
        }

        private void FindGround(GameObject actor)
        {
            float fallback = float.PositiveInfinity;
            foreach (Renderer renderer in renderers) fallback = Mathf.Min(fallback, renderer.bounds.min.y);
            if (float.IsPositiveInfinity(fallback)) fallback = startPosition.y - .9f;
            Vector3 origin = actor.transform.position + Vector3.up * 2f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity, ground = fallback;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.normal.y < .5f ||
                    hit.collider.GetComponentInParent<Health>() != null || hit.distance >= closest) continue;
                closest = hit.distance;
                ground = hit.point.y;
                groundNormal = hit.normal;
            }
            groundPoint = new Vector3(actor.transform.position.x, ground + .015f, actor.transform.position.z);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private bool BuildRagdoll(Transform root, GameObject actor)
        {
            Transform Bone(string name, string legacy) => Find(root, name) ?? Find(root, legacy);
            Transform hips = Bone("Hips", "DF_PelvisJoint");
            Transform spine = Bone("Spine", "DF_SpineLower");
            Transform chest = Bone("Chest", "DF_SpineUpper");
            Transform head = Bone("Head", "DF_NeckJoint");
            Transform foot = Bone("FootL", "DF_AnkleJointL");
            if (hips == null || spine == null || chest == null || head == null || foot == null) return false;
            float scale = Mathf.Clamp((head.position.y - foot.position.y) / 1.65f, .6f, 2f);
            BodyPart pelvis = Segment(hips, spine.position, null, 12f, .16f * scale);
            BodyPart lower = Segment(spine, chest.position, pelvis, 8f, .13f * scale, 40f, 25f);
            BodyPart upper = Segment(chest, head.position, lower, 18f, .17f * scale, 55f, 35f);
            Segment(head, head.position + actor.transform.up * (.20f * scale), upper, 4f, .12f * scale, 65f, 55f);
            foreach (string side in new[] { "L", "R" })
            {
                Transform arm = Bone("UpperArm" + side, "DF_Arm" + side);
                Transform elbow = Bone("Forearm" + side, "DF_ElbowJoint" + side);
                Transform hand = Bone("Hand" + side, "DF_WristJoint" + side);
                Transform thigh = Bone("Thigh" + side, "DF_Leg" + side);
                Transform knee = Bone("Shin" + side, "DF_KneeJoint" + side);
                Transform ankle = Bone("Foot" + side, "DF_AnkleJoint" + side);
                if (arm == null || elbow == null || hand == null || thigh == null || knee == null || ankle == null) return false;
                BodyPart shoulder = Segment(arm, elbow.position, upper, 4f, .075f * scale, 110f, 80f);
                BodyPart forearm = Segment(elbow, hand.position, shoulder, 2f, .06f * scale, 35f, 15f);
                forearm.LowTwist = -135f;
                Segment(hand, hand.position + (hand.position - elbow.position).normalized * (.12f * scale),
                    forearm, .7f, .055f * scale, 65f, 45f);
                BodyPart leg = Segment(thigh, knee.position, pelvis, 8f, .10f * scale, 65f, 85f);
                BodyPart shin = Segment(knee, ankle.position, leg, 5f, .08f * scale, 15f, 135f);
                shin.LowTwist = -10f;
                Segment(ankle, ankle.position + actor.transform.forward * (.18f * scale), shin, 1f, .065f * scale, 50f, 25f);
                // Start a knee buckle instead of balancing an upright statue on its feet.
                leg.Body.AddTorque(-actor.transform.right * .9f, ForceMode.VelocityChange);
                shin.Body.AddTorque(actor.transform.right * .7f, ForceMode.VelocityChange);
            }

            GameObject ground = new GameObject("DF_RagdollGround");
            ground.transform.SetParent(transform, false);
            ground.transform.SetPositionAndRotation(groundPoint - groundNormal * .15f,
                Quaternion.FromToRotation(Vector3.up, groundNormal));
            ground.transform.localScale = Vector3.one;
            BoxCollider floor = ground.AddComponent<BoxCollider>();
            floor.size = new Vector3(10f, .3f, 10f);

            foreach (BodyPart part in parts)
            {
                if (part.Parent == null) continue;
                CharacterJoint joint = part.Body.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = part.Parent.Body;
                joint.anchor = Vector3.zero;
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = part.Parent.Body.transform.InverseTransformPoint(part.Body.position);
                joint.axis = part.Body.transform.InverseTransformDirection(actor.transform.right);
                joint.swingAxis = part.Body.transform.InverseTransformDirection(actor.transform.forward);
                joint.lowTwistLimit = new SoftJointLimit { limit = part.LowTwist };
                joint.highTwistLimit = new SoftJointLimit { limit = part.Twist };
                joint.swing1Limit = new SoftJointLimit { limit = part.Swing };
                joint.swing2Limit = new SoftJointLimit { limit = part.Swing };
                joint.enableProjection = true;
                joint.projectionDistance = .05f;
                joint.projectionAngle = 15f;
                joint.enableCollision = false;
                // No motors, springs or pose targets: every joint is passive under gravity.
            }
            // Avoid self-collision explosions from armor and overlapping limb capsules.
            for (int i = 0; i < ragdollColliders.Count; i++)
                for (int j = 0; j < i; j++) Physics.IgnoreCollision(ragdollColliders[i], ragdollColliders[j]);
            Vector3 inherited = Vector3.zero;
            CharacterController controller = actor.GetComponent<CharacterController>();
            if (controller != null && controller.enabled) inherited = Vector3.ClampMagnitude(controller.velocity, 6f) * .45f;
            foreach (BodyPart part in parts) part.Body.linearVelocity = inherited + Vector3.down * .25f;
            pelvis.Body.AddForce(Vector3.down * .65f, ForceMode.VelocityChange);
            upper.Body.AddTorque(actor.transform.right * 1.5f + actor.transform.forward * .3f, ForceMode.VelocityChange);
            AttachRigidDetails(root);
            return true;
        }

        private BodyPart Segment(Transform bone, Vector3 end, BodyPart parent, float mass, float radius,
            float swing = 75f, float twist = 45f)
        {
            // Unscaled independent proxies avoid nested/nonuniform skeleton scales in PhysX.
            GameObject proxy = new GameObject("DF_Ragdoll_" + bone.name);
            proxy.transform.SetParent(transform, false);
            proxy.transform.SetPositionAndRotation(bone.position, bone.rotation);
            Rigidbody body = proxy.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = .15f;
            body.angularDamping = .25f;
            body.solverIterations = 8;
            body.solverVelocityIterations = 2;
            body.maxAngularVelocity = 12f;
            body.maxDepenetrationVelocity = 2f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Vector3 delta = end - bone.position;
            GameObject shape = new GameObject("Capsule");
            shape.transform.SetParent(proxy.transform, false);
            shape.transform.position = bone.position + delta * .5f;
            shape.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.sqrMagnitude > .00001f ? delta.normalized : Vector3.up);
            CapsuleCollider capsule = shape.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = Mathf.Max(radius * 2f, delta.magnitude * .9f);
            ragdollColliders.Add(capsule);
            BodyPart result = new BodyPart { Bone = bone, Body = body, Parent = parent, Swing = swing, Twist = twist, LowTwist = -twist };
            parts.Add(result);
            return result;
        }

        private void AttachRigidDetails(Transform root)
        {
            Transform rifleDetails = Find(root, "DF_SivraRifleDetails");
            Transform rightHand = Find(root, "HandR");
            if (rifleDetails != null && rightHand != null) rifleDetails.SetParent(rightHand, true);
            foreach (Renderer renderer in renderers)
            {
                if (!(renderer is MeshRenderer)) continue;
                bool attached = false;
                foreach (BodyPart part in parts)
                    if (renderer.transform == part.Bone || renderer.transform.IsChildOf(part.Bone)) { attached = true; break; }
                if (attached) continue;
                BodyPart nearest = parts[0];
                float distance = float.PositiveInfinity;
                foreach (BodyPart part in parts)
                {
                    float candidate = (renderer.bounds.center - part.Bone.position).sqrMagnitude;
                    if (candidate < distance) { nearest = part; distance = candidate; }
                }
                if (!nearest.Bone.IsChildOf(renderer.transform)) renderer.transform.SetParent(nearest.Bone, true);
            }
        }

        private void FixedUpdate()
        {
            if (physicsWorld.IsValid() && Time.timeScale > 0f) physicsWorld.Simulate(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= CollapseSeconds + HoldSeconds + FadeSeconds) { Destroy(gameObject); return; }
            // Proxies run in a separate physics world. Copy their final world poses in
            // parent-first order; nothing writes a fixed collapse pose onto these bones.
            foreach (BodyPart part in parts)
                if (part.Bone != null && part.Body != null)
                    part.Bone.SetPositionAndRotation(part.Body.position, part.Body.rotation);

            float fade = Mathf.Clamp01((elapsed - CollapseSeconds - HoldSeconds) / FadeSeconds);
            if (fade <= 0f) return;
            for (int r = 0; r < renderers.Count; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetFloat(FadeId, fade);
                renderer.SetPropertyBlock(block);
                for (int i = 0; i < materialCounts[r]; i++)
                {
                    block.Clear();
                    renderer.GetPropertyBlock(block, i);
                    if (!block.isEmpty) { block.SetFloat(FadeId, fade); renderer.SetPropertyBlock(block, i); }
                }
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene == ownerScene) Destroy(gameObject);
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnDestroy()
        {
            bodies.Remove(this);
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (!quitting && Application.isPlaying && isolatedScene.IsValid() && isolatedScene.isLoaded)
                SceneManager.UnloadSceneAsync(isolatedScene);
        }
    }
}
