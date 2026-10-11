using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Combat
{
    // Skill gestures only. Basic attacks use tap/hold directly through BasicAttack.TryAttack.
    public sealed class ManualAiming : MonoBehaviour
    {
        private Health self;
        private PlayerHeroAbilities abilities;
        private Camera view;
        private Vector2 pressOrigin;
        private Vector3 rawDirection;
        private float distance, began;
        private bool dragged, hasDirection;
        public int Slot { get; private set; } = -1;
        public bool Active => Slot >= 0;
        public bool Cancelled { get; private set; }
        public AimSpec Spec { get; private set; }
        public AimPose Pose { get; private set; }
        public static ManualAiming For(GameObject actor) => actor.GetComponent<ManualAiming>() ?? actor.AddComponent<ManualAiming>();
        private void Awake()
        { self = GetComponent<Health>(); abilities = GetComponent<PlayerHeroAbilities>(); }
        private bool Ready(int slot) => isActiveAndEnabled && self != null && !self.IsDead && Time.timeScale > 0f && HeroCombatStatus.CanAct(gameObject) &&
            abilities != null && abilities.isActiveAndEnabled && abilities.RemainingFor(slot) <= 0f;
        public bool Begin(int slot, Vector2 screen)
        {
            Cancel();
            if (slot < 0 || slot > 3 || !Ready(slot)) return false;
            Spec = abilities.AimFor(slot);
            Slot = slot; pressOrigin = screen; began = Time.unscaledTime; dragged = false; Cancelled = false;
            if (!AimingSettings.Current.rememberAimDirection || !hasDirection) rawDirection = AimGeometry.Direction(transform.forward);
            distance = Spec.castRange * .65f;
            Rebuild();
            if (!Spec.castOnRelease) { Release(); return false; }
            return true;
        }
        public void Drag(Vector2 screen, float scale, bool inCancelZone)
        {
            if (!Active) return;
            if (!Ready(Slot)) { Cancel(); return; }
            Cancelled = inCancelZone;
            Vector2 delta = (screen - pressOrigin) / Mathf.Max(.01f, scale);
            float deadZone = Spec.aimType == AimType.Area ? AimingSettings.Current.dragDeadZone : AimingSettings.Current.directionDeadZone;
            if (delta.magnitude >= Mathf.Max(.1f, deadZone))
            {
                dragged = true;
                view = view != null ? view : Camera.main;
                Vector3 right = view != null ? AimGeometry.Direction(view.transform.right) : Vector3.right;
                Vector3 forward = view != null ? AimGeometry.Direction(view.transform.forward) : Vector3.forward;
                rawDirection = AimGeometry.Direction(right * delta.x + forward * delta.y);
                hasDirection = true;
                distance = Mathf.Clamp01(delta.magnitude / Mathf.Max(1f, AimingSettings.Current.dragRadius)) * Spec.castRange;
            }
            else if (dragged && Spec.aimType == AimType.Area) distance = 0f;
            Rebuild();
        }
        private void LateUpdate()
        { if (!Active) return; if (!Ready(Slot)) Cancel(); else Rebuild(); }
        private void Rebuild()
        {
            Vector3 direction = rawDirection; Health target = null;
            if (Spec.aimType == AimType.Ally)
                target = AimGeometry.Select(gameObject, direction, Spec.castRange, Spec.coneAngle * .5f, true);
            else if (AimGeometry.UsesConeCapture(Spec)) target = AimGeometry.CaptureConeTarget(gameObject, rawDirection, Spec);
            else if (Spec.aimType != AimType.Self && Spec.aimType != AimType.SelfArea && Spec.aimType != AimType.Dash)
                direction = AimGeometry.Assist(gameObject, rawDirection, Spec, out target);
            Vector3 point = Spec.aimType == AimType.SelfArea || Spec.aimType == AimType.Self
                ? transform.position : transform.position + direction * distance;
            if (!dragged && Spec.aimType == AimType.Area && target != null)
                point = target.transform.position;
            bool captured = Spec.targetingMode == TargetingMode.Cone && target != null;
            if (captured) point = target.transform.position;
            Pose = new AimPose(direction, AimGeometry.ClampPoint(transform.position, point, Spec.castRange),
                Spec.targetingMode == TargetingMode.Ally || Spec.targetingMode == TargetingMode.Homing ? target : null, captured);
        }
        public bool Release()
        {
            if (!Active) return false;
            int slot = Slot;
            bool tap = !dragged && Time.unscaledTime - began <= AimingSettings.Current.tapSeconds;
            bool commit = !Cancelled && Ready(slot) && (!tap || Spec.allowTapCast);
            Rebuild(); AimPose pose = Pose;
            Cancel();
            return commit && abilities.CastAimed(slot, pose);
        }
        public void Cancel() { Slot = -1; Cancelled = false; }
        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
    }
}
