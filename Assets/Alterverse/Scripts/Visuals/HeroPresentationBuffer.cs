using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Small render-only snapshot buffer. Never moves the network/gameplay actor.</summary>
    public sealed class HeroPresentationBuffer
    {
        private struct Sample { public Vector3 position; public Quaternion rotation; public float time; }
        private readonly Sample[] samples = new Sample[8];
        private int count;
        private bool sourceTimeline;
        private float sourceClockOffset;
        public const float Delay = .10f;
        private readonly float interpolationDelay;
        public HeroPresentationBuffer(float delay = Delay) => interpolationDelay = Mathf.Max(0f, delay);
        public bool Ready => count > 0;
        public void Clear() { count = 0; sourceTimeline = false; }
        public void Push(Vector3 position, Quaternion rotation, float time, bool reset)
        {
            if (sourceTimeline || reset || count > 0 && time < samples[count - 1].time) Clear();
            Append(position,rotation,time);
        }
        // Keep server sampling intervals intact. Arrival jitter must not become gait speed.
        // Only a clock offset is estimated; gameplay roots and collision remain authoritative.
        public void PushRemote(Vector3 position,Quaternion rotation,float sampledAt,float receivedAt,bool reset)
        {
            if(float.IsNaN(sampledAt)||float.IsInfinity(sampledAt)||
                float.IsNaN(receivedAt)||float.IsInfinity(receivedAt))return;
            if(reset||!sourceTimeline||count>0&&sampledAt<samples[count-1].time)
            {
                Clear();sourceTimeline=true;sourceClockOffset=receivedAt-sampledAt;
            }
            else sourceClockOffset=Mathf.Min(sourceClockOffset,receivedAt-sampledAt);
            Append(position,rotation,sampledAt);
        }
        private void Append(Vector3 position,Quaternion rotation,float time)
        {
            var sample = new Sample { position = position, rotation = rotation, time = time };
            if (count > 0 && time == samples[count - 1].time) { samples[count - 1] = sample; return; }
            if (count == samples.Length) { System.Array.Copy(samples, 1, samples, 0, count - 1); count--; }
            samples[count++] = sample;
        }
        public bool Evaluate(float now, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
            if (!Ready) return false;
            float time = now - (sourceTimeline ? sourceClockOffset : 0f) - interpolationDelay;
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


