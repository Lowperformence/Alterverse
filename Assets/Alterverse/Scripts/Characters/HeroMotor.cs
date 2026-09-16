using UnityEngine;
using UnityEngine.InputSystem;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class HeroMotor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float rotationSpeed = 14f;
        [SerializeField] private Transform cameraTransform;

        private CharacterController controller;
        private Vector2 moveInput;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            ReadKeyboardInput();
            Move(moveInput);
        }

        private void ReadKeyboardInput()
        {
            if (Keyboard.current == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            float x = 0f;
            float y = 0f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void Move(Vector2 input)
        {
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 direction = forward * input.y + right * input.x;
            direction = Vector3.ClampMagnitude(direction, 1f);

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            Vector3 velocity = direction * moveSpeed;
            if (!controller.isGrounded) velocity.y = -2f;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
