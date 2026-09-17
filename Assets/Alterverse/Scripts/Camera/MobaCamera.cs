using UnityEngine;

namespace Alterverse.CameraSystem
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class MobaCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
        [SerializeField, Min(0.1f)] private float followSharpness = 12f;
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
        [SerializeField, Range(30f, 70f)] private float fieldOfView = 46f;

        private UnityEngine.Camera gameplayCamera;

        private void Awake()
        {
            gameplayCamera = GetComponent<UnityEngine.Camera>();
            gameplayCamera.fieldOfView = fieldOfView;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.LookAt(target.position + lookOffset, Vector3.up);
        }

        private void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.LookAt(target.position + lookOffset, Vector3.up);
        }
    }
}
