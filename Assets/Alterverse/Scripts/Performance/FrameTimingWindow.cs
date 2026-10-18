using System;

namespace Alterverse.Performance
{
    /// <summary>Bounded, reusable frame samples. Reports wall-clock pacing, including stalls.</summary>
    public sealed class FrameTimingWindow
    {
        private readonly float[] samples=new float[2048];
        private int frames;
        private double elapsed;
        private float maximum;
        public float Fps{get;private set;}
        public float P95Milliseconds{get;private set;}
        public float MaxMilliseconds{get;private set;}
        public bool Observe(float seconds)
        {
            if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return false;
            samples[frames%samples.Length]=seconds;
            frames++;elapsed+=seconds;maximum=Math.Max(maximum,seconds);
            if(elapsed<10)return false;
            int count=Math.Min(frames,samples.Length);
            Array.Sort(samples,0,count);
            Fps=(float)(frames/elapsed);
            P95Milliseconds=samples[Math.Max(0,(int)Math.Ceiling(count*.95)-1)]*1000;
            MaxMilliseconds=maximum*1000;
            frames=0;elapsed=0;maximum=0;
            return true;
        }
        public void Reset(){frames=0;elapsed=0;maximum=0;Fps=P95Milliseconds=MaxMilliseconds=0;}
    }
}
