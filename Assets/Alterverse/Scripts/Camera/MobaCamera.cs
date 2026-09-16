using UnityEngine;

namespace Alterverse.CameraSystem
{
    public sealed class MobaCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
        [SerializeField, Min(0.1f)] private float followSharpness = 10f;
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1f, 0f);

        public void SetTarget(Transform newTarget) => target = newTarget;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.LookAt(target.position + lookOffset, Vector3.up);
        }
    }
}
