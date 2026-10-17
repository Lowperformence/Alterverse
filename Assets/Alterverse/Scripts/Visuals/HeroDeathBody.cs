using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Alterverse.Visuals
{
    /// <summary>A render-only limp collapse. No gameplay scripts, colliders or network objects are copied.</summary>
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
        private readonly List<BonePose> poses = new();
        private readonly List<Transform> contacts = new();
        private MaterialPropertyBlock block;
        private Vector3 startPosition, fallDirection;
        private Quaternion startRotation;
        private float groundY, elapsed, side;

        // Native Unity objects must be created on the main thread after AddComponent.
        private void Awake() => block = new MaterialPropertyBlock();

        private struct BonePose
        {
            public Transform Bone;
            public Quaternion Start, End;
            public float Delay;
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
            body.startRotation = actor.transform.rotation;
            host.transform.rotation = body.startRotation;
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
            body.groundY = body.FindGround(actor);
            body.fallDirection = actor.transform.forward;
            // Stable visual variation without deprecated object IDs or gameplay RNG.
            body.side = actor.transform.position.x < 0f ? -1f : 1f;
            body.BuildPose(root, actor.transform.right, actor.transform.forward);
            foreach (BonePose pose in body.poses) body.contacts.Add(pose.Bone);
            if (hips != null) body.contacts.Add(hips);
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
        }

        private float FindGround(GameObject actor)
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
            }
            return ground + .015f;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private void BuildPose(Transform root, Vector3 right, Vector3 forward)
        {
            Fold(root, "Spine", "DF_SpineLower", right, 18f, .04f);
            Fold(root, "Chest", "DF_SpineUpper", right, 14f, .08f);
            Fold(root, "Head", "DF_NeckJoint", right, 28f, .19f);
            foreach (string suffix in new[] { "L", "R" })
            {
                float s = suffix == "L" ? -1f : 1f;
                Fold(root, "Thigh" + suffix, "DF_Leg" + suffix, right, -38f + s * 9f, 0f);
                Fold(root, "Shin" + suffix, "DF_KneeJoint" + suffix, right, 88f + s * 12f, .03f);
                Fold(root, "Foot" + suffix, "DF_AnkleJoint" + suffix, right, -20f, .12f);
                Fold(root, "UpperArm" + suffix, "DF_Arm" + suffix, forward, s * 24f, .10f);
                Fold(root, "Forearm" + suffix, "DF_ElbowJoint" + suffix, right, -30f + s * 12f, .16f);
                Fold(root, "Hand" + suffix, "DF_WristJoint" + suffix, right, 22f, .23f);
            }
        }

        private void Fold(Transform root, string name, string fallback, Vector3 axis, float degrees, float delay)
        {
            Transform bone = Find(root, name) ?? Find(root, fallback);
            if (bone == null) return;
            Quaternion start = bone.localRotation;
            poses.Add(new BonePose { Bone = bone, Start = start,
                End = start * Quaternion.AngleAxis(degrees, bone.InverseTransformDirection(axis)), Delay = delay });
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= CollapseSeconds + HoldSeconds + FadeSeconds) { Destroy(gameObject); return; }
            float t = Mathf.Clamp01(elapsed / CollapseSeconds);
            float fall = t * t * (3f - 2f * t);
            float settle = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * t * 4f;
            transform.rotation = startRotation * Quaternion.Euler(fall * 83f + settle * 3f, 0f, side * fall * 13f);
            transform.position = Vector3.Lerp(startPosition,
                new Vector3(startPosition.x, groundY + .24f, startPosition.z) + fallDirection * .30f, fall);
            foreach (BonePose pose in poses)
            {
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - pose.Delay) / (1f - pose.Delay)));
                pose.Bone.localRotation = Quaternion.Slerp(pose.Start, pose.End, p);
            }
            // Ground the fallen silhouette without letting it sink through the arena floor.
            float lowest = float.PositiveInfinity;
            // Skinned localBounds often include animation safety margins, so use bone contacts
            // rather than that oversized box when grounding the articulated model.
            foreach (Transform contact in contacts) if (contact != null) lowest = Mathf.Min(lowest, contact.position.y - .09f);
            foreach (Renderer renderer in renderers)
                if (renderer is MeshRenderer) lowest = Mathf.Min(lowest, renderer.bounds.min.y);
            if (lowest < groundY) transform.position += Vector3.up * (groundY - lowest);

            float fade = Mathf.Clamp01((elapsed - CollapseSeconds - HoldSeconds) / FadeSeconds);
            if (fade <= 0f) return;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetFloat(FadeId, fade);
                renderer.SetPropertyBlock(block);
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    block.Clear();
                    renderer.GetPropertyBlock(block, i);
                    if (!block.isEmpty) { block.SetFloat(FadeId, fade); renderer.SetPropertyBlock(block, i); }
                }
            }
        }

        private void OnDestroy() => bodies.Remove(this);
    }
}
