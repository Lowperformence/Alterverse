using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed class RiftPulse : MonoBehaviour
    {
        [SerializeField] private float pulseAmount = 0.14f;
        [SerializeField] private float pulseSpeed = 2.4f;
        [SerializeField] private float rotationSpeed = 18f;

        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = new Vector3(baseScale.x * pulse, baseScale.y, baseScale.z * pulse);
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
