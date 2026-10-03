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
        [SerializeField, Range(0f, .35f)] private float inputDeadZone = .08f;
        [SerializeField, Range(0f, 1f)] private float attackMovementMultiplier = .88f;

        [Header("Rotation")]
        [SerializeField, Min(.1f)] private float rotationSharpness = 16f;
        [SerializeField] private Transform cameraTransform;

        [Header("Arena")]
        [SerializeField] private float arenaHalfWidth = 41f;
        [SerializeField] private float arenaHalfHeight = 35f;

        private HeroLocomotion body;
        private HeroCombatProfile profile;
        private PlanetBuffReceiver planetBuff;
        private Vector2 moveInput;
        private Vector3 planarVelocity;
        private Vector3 lastMoveDirection = Vector3.forward;
        private Vector3 attackFacing;
        private float attackUntil;
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
            planetBuff = GetComponent<PlanetBuffReceiver>() ?? gameObject.AddComponent<PlanetBuffReceiver>();
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

            float stateMultiplier = State == FighterState.Attacking ? attackMovementMultiplier : 1f;
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

            // Reversals should not skate through zero. Brake first, then accelerate in the
            // new direction. Normal acceleration remains soft and predictable.
            if (Vector3.Dot(planarVelocity, desiredVelocity) < 0f) return Mathf.Max(acceleration, deceleration);
            return desiredVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
        }

        private void UpdateRotation(Vector3 inputDirection, float dt)
        {
            Vector3 facing = Vector3.zero;

            // Steering wins while moving. At rest an attack can smoothly turn toward its
            // target, matching the existing combat-facing rule without snapping.
            if (planarVelocity.sqrMagnitude > .01f)
                facing = planarVelocity.normalized;
            else if (inputDirection.sqrMagnitude > .0001f)
                facing = inputDirection.normalized;
            else if (State == FighterState.Attacking && attackFacing.sqrMagnitude > .0001f)
                facing = attackFacing.normalized;

            if (facing.sqrMagnitude <= .0001f)
            {
                body.FaceMovement(Vector3.zero);
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(facing, Vector3.up);
            float slerpT = 1f - Mathf.Exp(-rotationSharpness * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, slerpT);

            // HeroLocomotion remains the shared facing owner for bots/dashes. Feeding it the
            // already-smoothed forward vector prevents its LateUpdate from snapping the player.
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
        /// the combat system. Movement and target-facing are blended instead of hard-locked.
        /// </summary>
        public void NotifyAttack(float seconds, Vector3 facing)
        {
            attackUntil = Mathf.Max(attackUntil, Time.time + Mathf.Max(.05f, seconds));
            facing.y = 0f;
            if (facing.sqrMagnitude > .0001f) attackFacing = facing.normalized;
            State = FighterState.Attacking;
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
