using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed class TransientScaleVfx : MonoBehaviour
    {
        private Vector3 startScale;
        private Vector3 endScale;
        private float lifetime;
        private float age;
        private float spinSpeed;

        public void Configure(Vector3 from, Vector3 to, float duration, float spinDegreesPerSecond = 0f)
        {
            startScale = from;
            endScale = to;
            lifetime = Mathf.Max(0.05f, duration);
            spinSpeed = spinDegreesPerSecond;
            transform.localScale = startScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

            if (Mathf.Abs(spinSpeed) > 0.01f)
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            if (age >= lifetime)
                Destroy(gameObject);
        }
    }
}
