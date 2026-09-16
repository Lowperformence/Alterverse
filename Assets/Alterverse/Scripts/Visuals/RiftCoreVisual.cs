using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed class RiftCoreVisual : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 55f;
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField] private float pulseAmount = 0.12f;
        private Vector3 baseScale;
        private Vector3 basePosition;

        private void Awake()
        {
            baseScale = transform.localScale;
            basePosition = transform.localPosition;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = baseScale * pulse;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * 1.5f) * 0.08f);
        }
    }
}
