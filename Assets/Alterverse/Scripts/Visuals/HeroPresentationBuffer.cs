using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Small render-only snapshot buffer. Never moves the network/gameplay actor.</summary>
    public sealed class HeroPresentationBuffer
    {
        private struct Sample { public Vector3 position; public Quaternion rotation; public float time; }
        private readonly Sample[] samples = new Sample[8];
        private int count;
        public const float Delay = .10f;
        public bool Ready => count > 0;
        public void Clear() => count = 0;
        public void Push(Vector3 position, Quaternion rotation, float time, bool reset)
        {
            if (reset || count > 0 && time < samples[count - 1].time) Clear();
            var sample = new Sample { position = position, rotation = rotation, time = time };
            if (count > 0 && time == samples[count - 1].time) { samples[count - 1] = sample; return; }
            if (count == samples.Length) { System.Array.Copy(samples, 1, samples, 0, count - 1); count--; }
            samples[count++] = sample;
        }
        public bool Evaluate(float now, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
            if (!Ready) return false;
            float time = now - Delay;
            Sample first = samples[0], last = samples[count - 1];
            if (time <= first.time) { position = first.position; rotation = first.rotation; return true; }
            for (int i = 1; i < count; i++)
            {
                Sample a = samples[i - 1], b = samples[i];
                if (time > b.time) continue;
                float t = Mathf.InverseLerp(a.time, b.time, time);
                position = Vector3.Lerp(a.position, b.position, t);
                rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
                return true;
            }
            // Hold when packets stop; never invent travel through a wall or another actor.
            position = last.position; rotation = last.rotation; return true;
        }
    }
}
