using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>One imported rig owns the visual pose. Movement and combat remain on the actor.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(940)]
    public sealed class AuthoredHeroPresentation : MonoBehaviour
    {
        private Animator animator;
        private Health health;
        private Transform muzzle;
        private Vector3 previousPosition;
        private string currentState;
        private float actionUntil;
        private bool dead;
        private readonly Dictionary<string, float> durations = new Dictionary<string, float>();
        public float PlanarSpeed { get; private set; }
        public Vector3 ReleasePoint => muzzle != null ? muzzle.position : transform.position + Vector3.up * .35f;

        public static bool TryCreate(GameObject actor, HeroId hero, out Transform root)
        {
            root = null;
            GameObject model = Resources.Load<GameObject>("AuthoredHeroes/" + hero);
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("AuthoredHeroes/Controllers/" + hero);
            // A missing or unfinished import must never replace a functioning hero with an invisible one.
            if (model == null || controller == null) return false;
            // Keep the established cosmetic coordinate frame; only the imported model uses feet-origin coordinates.
            root = new GameObject("DF_VisualRoot").transform;
            root.SetParent(actor.transform, false);
            root.localPosition = new Vector3(0, .32f, 0);
            root.localScale = new Vector3(1.22f, 1.32f, 1.22f);
            GameObject instance = Instantiate(model, root, false);
            instance.name = "DF_AuthoredModel";
            instance.transform.localPosition = new Vector3(0, -.96f, 0);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = new Vector3(1.10f, 1f, 1.10f);
            // Unity can return a native-null wrapper for a missing component in the Editor.
            // TryGetComponent checks the native object; C# ?? only checks the managed reference.
            if (!instance.TryGetComponent<Animator>(out Animator animator))
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (!actor.TryGetComponent<AuthoredHeroPresentation>(out var presentation))
                presentation = actor.AddComponent<AuthoredHeroPresentation>();
            presentation.Initialize(animator);
            return true;
        }

        private void Initialize(Animator rig)
        {
            animator = rig;
            foreach (AnimationClip clip in rig.runtimeAnimatorController.animationClips)
            {
                string name = ClipName(clip.name);
                durations[name] = clip.length;
            }
            foreach (Transform bone in rig.GetComponentsInChildren<Transform>(true))
                if (bone.name == "DF_Muzzle") { muzzle = bone; break; }
            previousPosition = transform.position;
            health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
            Play("Idle", 0, .05f);
        }

        private static string ClipName(string name)
        {
            int separator = name.LastIndexOf('|');
            if (separator >= 0) name = name.Substring(separator + 1);
            return name;
        }

        private void OnEnable() { previousPosition = transform.position; }
        private void OnDestroy() { if (health != null) health.Damaged -= OnDamaged; }
        private void OnDamaged(float applied, float absorbed, GameObject source)
        {
            if (health == null || health.IsDead || applied + absorbed <= 0 || Time.time < actionUntil) return;
            Play("Hit", .24f, .04f);
        }
        public void PulseAttack(bool ultimate) => Play(ultimate ? "Ultimate" : "Attack", ultimate ? .95f : .64f, .055f);
        public void PulseBasic(float windup) => Play("Attack", Mathf.Max(.32f, windup + .32f), .045f);
        public void PulseAbility(int slot, float windup) => Play(slot == 3 ? "Ultimate" : "Skill" + Mathf.Clamp(slot, 0, 2), Mathf.Max(.50f, windup + .45f), .06f);
        public void PulseDash() => Play("Run", .20f, .025f);
        public void Empower(float seconds) { GetComponent<VoidWingPresentation>()?.Pulse(); }

        private void Play(string state, float duration, float blend)
        {
            if (animator == null || !durations.ContainsKey(state) || (health != null && health.IsDead && state != "Death")) return;
            animator.speed = duration > 0 ? durations[state] / duration : 1f;
            animator.CrossFadeInFixedTime(state, blend, 0, 0);
            currentState = state;
            actionUntil = duration > 0 ? Time.time + duration : 0;
        }

        private void LateUpdate()
        {
            if (animator == null) return;
            Vector3 displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            displacement.y = 0;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            PlanarSpeed = displacement.magnitude > Mathf.Max(3, dt * 40) ? 0 : displacement.magnitude / dt;
            bool nowDead = health != null && health.IsDead;
            if (nowDead)
            {
                if (!dead) Play("Death", 1.2f, .1f);
                dead = true;
                return;
            }
            if (dead) { dead = false; actionUntil = 0; currentState = null; }
            if (Time.time < actionUntil) return;
            string desired = PlanarSpeed > .16f ? "Run" : "Idle";
            if (currentState != desired) Play(desired, 0, .12f);
            animator.speed = desired == "Run" ? Mathf.Clamp(PlanarSpeed / 4f, .65f, 1.6f) : 1f;
        }
    }
}
