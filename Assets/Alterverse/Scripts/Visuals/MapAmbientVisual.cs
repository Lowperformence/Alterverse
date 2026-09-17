using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed class MapAmbientVisual : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 18f;
        [SerializeField] private float bobSpeed = 1.35f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float pulseSpeed = 1.8f;
        [SerializeField] private float pulseAmount = 0.06f;

        private Vector3 basePosition;
        private Vector3 baseScale;
        private float phase;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            phase = Mathf.Abs(transform.position.x * 0.37f + transform.position.z * 0.19f);
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            float bob = Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseAmount;
            transform.localPosition = basePosition + Vector3.up * bob;
            transform.localScale = baseScale * pulse;
        }
    }
}
