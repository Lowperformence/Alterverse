using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.Characters
{
    [DefaultExecutionOrder(-25)]
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class HeroMotor : MonoBehaviour
    {
        public enum FighterState
        {
            Idle,
            Running,
            Attacking
        }

        [Header("Movement")]
        [SerializeField, Min(.1f)] private float fallbackMoveSpeed = 6.5f;
        [SerializeField, Min(.1f)] private float acceleration = 28f;
        [SerializeField, Min(.1f)] private float deceleration = 36f;
        [SerializeField, Min(.1f)] private float turnAcceleration = 72f;
        [SerializeField, Range(0f, .35f)] private float inputDeadZone = .08f;
        [SerializeField, Range(0f, 1f)] private float attackMovementMultiplier = .88f;

        [Header("Rotation")]
        [SerializeField, Min(.1f)] private float rotationSharpness = 16f;
        [SerializeField, Min(.1f)] private float steeringSharpness = 28f;
        [SerializeField] private Transform cameraTransform;

        [Header("Arena")]
        [SerializeField] private float arenaHalfWidth = ArenaLayout.MovementHalfWidth;
        [SerializeField] private float arenaHalfHeight = ArenaLayout.MovementHalfHeight;

        private HeroLocomotion body;
        private HeroCombatProfile profile;
        private HeroIdentity identity;
        private PlanetBuffReceiver planetBuff;
        private SoftTargeting targeting;
        private Vector2 moveInput;
        private Vector3 planarVelocity;
        private Vector3 lastMoveDirection = Vector3.forward;
        private Vector3 attackFacing;
        private float attackUntil;
        private Health basicFacingTarget;
        private float basicFacingUntil;
        private int externalFrame = -1;

        public FighterState State { get; private set; } = FighterState.Idle;
        public Vector3 LastMoveDirection => lastMoveDirection;
        public Vector3 SmoothedVelocity => planarVelocity;

        // Touch abilities run before this component's Update. Resolve their direction from
        // the current input instead of the previous movement frame.
        public Vector3 CurrentMoveDirection
        {
            get
            {
                Vector2 filtered = FilterInput(moveInput);
                if (filtered.sqrMagnitude <= .0001f) return Vector3.zero;

                if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
                Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
                forward.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, forward);
                return Vector3.ClampMagnitude(forward * filtered.y + right * filtered.x, 1f);
            }
        }

        public bool HasMoveInput => FilterInput(moveInput).sqrMagnitude > .0001f;

        public float MoveSpeed =>
            (profile != null ? profile.MoveSpeed : fallbackMoveSpeed) *
            (planetBuff != null ? planetBuff.MoveSpeedMultiplier : 1f) *
            body.SpeedMultiplier;

        private void Awake()
        {
            body = GetComponent<HeroLocomotion>() ?? gameObject.AddComponent<HeroLocomotion>();
            profile = GetComponent<HeroCombatProfile>();
            identity = GetComponent<HeroIdentity>();
            planetBuff = GetComponent<PlanetBuffReceiver>() ?? gameObject.AddComponent<PlanetBuffReceiver>();
            targeting = GetComponent<SoftTargeting>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // A remote human is driven by the owner's authoritative pose stream. Simulating
            // keyboard/touch locomotion again on this machine would double-apply movement.
            Alterverse.Multiplayer.CoopHeroSlot coopSlot = GetComponent<Alterverse.Multiplayer.CoopHeroSlot>();
            if (coopSlot != null && coopSlot.IsHuman && !coopSlot.IsLocalHuman)
            {
                ClearInput();
                ResetSmoothMotion();
                return;
            }

            if (!body.CanMove)
            {
                ClearInput();
                ResetSmoothMotion();
                return;
            }

            if (externalFrame != Time.frameCount) ReadKeyboardInput();

            Vector3 inputDirection = CurrentMoveDirection;
            if (inputDirection.sqrMagnitude > .0001f) lastMoveDirection = inputDirection.normalized;

            UpdateState(inputDirection);

            if (body.OwnsMovementFrame)
            {
                // Dashes/forced movement own displacement and facing for this frame.
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, deceleration * dt);
                return;
            }

            // Timmy has the same run speed as the assassin he chases. Slowing
            // him for his claw animation pushes an accepted melee target out of
            // reach and prevents the next held attack from ever starting.
            float stateMultiplier = State == FighterState.Attacking && identity?.Hero != HeroId.Timmy
                ? attackMovementMultiplier : 1f;
            Vector3 desiredVelocity = inputDirection * MoveSpeed * stateMultiplier;
            float rate = SelectVelocityRate(desiredVelocity);
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, rate * dt);

            if (planarVelocity.sqrMagnitude < .0004f && desiredVelocity.sqrMagnitude < .0004f)
                planarVelocity = Vector3.zero;

            Vector3 actual = body.Move(planarVelocity * dt, arenaHalfWidth, arenaHalfHeight);

            // Remove only the velocity component that is driving into a wall. Tangential
            // velocity survives, so CharacterController wall sliding stays smooth.
            if (body.LastCollisionNormal.sqrMagnitude > .01f)
            {
                planarVelocity = Vector3.ProjectOnPlane(planarVelocity, body.LastCollisionNormal);
                planarVelocity.y = 0f;
            }
            else if (actual.sqrMagnitude < .000001f && planarVelocity.sqrMagnitude > .01f && inputDirection.sqrMagnitude <= .0001f)
            {
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, deceleration * dt);
            }

            UpdateRotation(inputDirection, dt);
        }

        private void UpdateState(Vector3 inputDirection)
        {
            if (Time.time < attackUntil)
            {
                State = FighterState.Attacking;
                return;
            }

            attackFacing = Vector3.zero;
            State = inputDirection.sqrMagnitude > .0001f || planarVelocity.sqrMagnitude > .01f
                ? FighterState.Running
                : FighterState.Idle;
        }

        private float SelectVelocityRate(Vector3 desiredVelocity)
        {
            if (desiredVelocity.sqrMagnitude <= .0001f) return deceleration;
            if (planarVelocity.sqrMagnitude <= .0001f) return acceleration;

            float rate = desiredVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
            if (Vector3.Dot(planarVelocity, desiredVelocity) < 0f) rate = Mathf.Max(acceleration, deceleration);

            // Keep gentle corrections and starts soft, but shed the old direction promptly
            // when steering through a turn. Blend from 30 to 120 degrees to avoid a threshold snap.
            float alignment = Vector3.Dot(planarVelocity.normalized, desiredVelocity.normalized);
            float turnWeight = Mathf.InverseLerp(.8660254f, -.5f, alignment);
            return Mathf.Lerp(rate, Mathf.Max(rate, turnAcceleration), turnWeight);
        }

        private bool TryGetAttackFacing(out Vector3 facing)
        {
            facing = Vector3.zero;
            if (Time.time < basicFacingUntil && HeroCombatRules.Enemy(gameObject, basicFacingTarget))
            {
                facing = AimGeometry.Flat(basicFacingTarget.transform.position - transform.position);
                if (facing.sqrMagnitude > .0001f) return true;
            }
            if (State != FighterState.Attacking) return false;

            // SoftTargeting can be added by the combat component after HeroMotor.Awake, so
            // resolve it lazily as well. While the focus is valid, track its live position.
            if (targeting == null) targeting = GetComponent<SoftTargeting>();
            Health focus = targeting != null ? targeting.Current : null;
            if (focus != null)
            {
                facing = focus.transform.position - transform.position;
                facing.y = 0f;
                if (facing.sqrMagnitude > .0001f)
                {
                    attackFacing = facing.normalized;
                    facing = attackFacing;
                    return true;
                }
            }

            if (attackFacing.sqrMagnitude > .0001f)
            {
                facing = attackFacing.normalized;
                return true;
            }

            return false;
        }

        private void UpdateRotation(Vector3 inputDirection, float dt)
        {
            Vector3 facing = Vector3.zero;
            bool actionFacingActive = TryGetAttackFacing(out Vector3 combatFacing);

            // Combat focus wins while attacking. Translation stays independent, so the hero
            // can strafe or backpedal without ever firing/punching over the shoulder.
            if (actionFacingActive)
                facing = combatFacing;
            else if (inputDirection.sqrMagnitude > .0001f)
                facing = inputDirection.normalized;
            else if (planarVelocity.sqrMagnitude > .01f)
                facing = planarVelocity.normalized;

            if (facing.sqrMagnitude <= .0001f)
            {
                body.FaceMovement(Vector3.zero);
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(facing, Vector3.up);
            // Steering faces the current input instead of waiting for velocity to catch up.
            // Attack target facing retains its existing response and priority.
            float response = !actionFacingActive && inputDirection.sqrMagnitude > .0001f
                ? Mathf.Max(rotationSharpness, steeringSharpness) : rotationSharpness;
            float slerpT = 1f - Mathf.Exp(-response * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, slerpT);

            // Keep the shared late-facing owner on the same priority rule. During combat the
            // action direction wins; otherwise movement owns facing as before.
            if (actionFacingActive)
                body.FaceAction(combatFacing, Mathf.Max(.08f, dt * 2f));
            else
                body.FaceMovement(transform.forward);
        }

        private Vector2 FilterInput(Vector2 input)
        {
            float magnitude = Mathf.Clamp01(input.magnitude);
            if (magnitude <= inputDeadZone) return Vector2.zero;
            float scaled = Mathf.InverseLerp(inputDeadZone, 1f, magnitude);
            return input.normalized * scaled;
        }

        private void ReadKeyboardInput()
        {
            var keyboard = Keyboard.current;
            moveInput = keyboard == null
                ? Vector2.zero
                : Vector2.ClampMagnitude(new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)), 1f);
        }

        /// <summary>Existing mobile joystick/touch input entry point.</summary>
        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
            externalFrame = Time.frameCount;
        }

        /// <summary>
        /// Marks the locomotion state as attacking without taking damage execution away from
        /// the combat system. Movement stays independent while target-facing gets priority.
        /// </summary>
        public void NotifyAttack(float seconds, Vector3 facing)
        {
            float duration = Mathf.Max(.05f, seconds);
            attackUntil = Mathf.Max(attackUntil, Time.time + duration);
            facing.y = 0f;
            if (facing.sqrMagnitude > .0001f)
            {
                attackFacing = facing.normalized;
                body?.FaceAction(attackFacing, duration);
            }
            State = FighterState.Attacking;
        }

        public void TrackBasicTarget(Health target, float seconds)
        {
            if (!HeroCombatRules.Enemy(gameObject, target)) return;
            float until = Time.time + Mathf.Max(.02f, seconds);
            // Per-frame hold refreshes must not shorten an accepted windup's facing.
            basicFacingUntil = target == basicFacingTarget ? Mathf.Max(basicFacingUntil, until) : until;
            basicFacingTarget = target;
        }

        public void KeepBasicTargetFacing(float range)
        {
            // Small presentation hysteresis prevents movement facing taking over
            // whenever the held target oscillates just outside acquisition range.
            // Never acquire, attack, or refresh a dead/far-away target here.
            if (!HeroCombatRules.Enemy(gameObject, basicFacingTarget) ||
                HeroCombatRules.TargetDistance(transform.position, basicFacingTarget) > range + SoftTargeting.BasicFocusEdge) return;
            basicFacingUntil = Mathf.Max(basicFacingUntil, Time.time + .12f);
        }

        public void ClearInput()
        {
            moveInput = Vector2.zero;
            externalFrame = -1;
        }

        private void ResetSmoothMotion()
        {
            planarVelocity = Vector3.zero;
            attackFacing = Vector3.zero;
            attackUntil = 0f;
            basicFacingTarget = null;
            basicFacingUntil = 0f;
            State = FighterState.Idle;
        }

        private void OnDisable()
        {
            ClearInput();
            ResetSmoothMotion();
            if (body != null) body.ResetMotion();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                ClearInput();
                ResetSmoothMotion();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                ClearInput();
                ResetSmoothMotion();
            }
        }
    }
}
