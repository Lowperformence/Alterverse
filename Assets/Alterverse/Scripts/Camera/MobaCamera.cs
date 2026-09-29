using UnityEngine;

namespace Alterverse.CameraSystem
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class MobaCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 14.4f, -10.8f);
        [SerializeField, Min(0.1f)] private float followSharpness = 12f;
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
        [SerializeField, Range(30f, 70f)] private float fieldOfView = 46f;

        private UnityEngine.Camera gameplayCamera;
        private Vector3 lastTargetPosition;
        private Alterverse.Characters.HeroLocomotion locomotion;

        private void Awake()
        {
            gameplayCamera = GetComponent<UnityEngine.Camera>();
            gameplayCamera.fieldOfView = fieldOfView;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            locomotion=target!=null?target.GetComponent<Alterverse.Characters.HeroLocomotion>():null;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if((target.position-lastTargetPosition).sqrMagnitude>144f){SnapToTarget();return;}
            lastTargetPosition=target.position;
            Vector3 desiredPosition = target.position + offset;
            float blend = 1f - Mathf.Exp(-(locomotion!=null&&locomotion.OwnsMovementFrame?Mathf.Max(24,followSharpness):followSharpness) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            // Following position must not steer camera-relative input by changing camera yaw.
            transform.rotation=Quaternion.LookRotation(lookOffset-offset,Vector3.up);
        }

        private void SnapToTarget()
        {
            if (target == null) return;
            lastTargetPosition=target.position;
            transform.position = target.position + offset;
            // Following position must not steer camera-relative input by changing camera yaw.
            transform.rotation=Quaternion.LookRotation(lookOffset-offset,Vector3.up);
        }
    }
}
