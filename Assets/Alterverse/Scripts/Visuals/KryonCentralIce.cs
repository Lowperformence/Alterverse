using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Small cracked ice landmark at LHS 1140 b's center, baked into existing ground maps.</summary>
    public static class KryonCentralIce
    {
        public const float Radius=4.6f;

        private readonly struct Fracture
        {
            public readonly Vector2[] Points;
            public readonly float Width;
            public Fracture(float width,params Vector2[] points){Width=width;Points=points;}
        }

        // Hand-placed uneven fractures and offshoots, never regular radial spokes or a targeting ring.
        private static readonly Fracture[] fractures={
            new Fracture(.105f,new Vector2(-4.35f,-.95f),new Vector2(-2.9f,-.55f),new Vector2(-1.9f,-.85f),new Vector2(-.7f,-.35f),new Vector2(.55f,-.7f),new Vector2(1.5f,.1f),new Vector2(2.65f,.45f),new Vector2(4.15f,1.55f)),
            new Fracture(.085f,new Vector2(-1.45f,4.25f),new Vector2(-1.25f,2.9f),new Vector2(-.55f,2.1f),new Vector2(-.85f,1.2f),new Vector2(-.7f,-.35f),new Vector2(-.1f,-1.65f),new Vector2(-.55f,-2.55f),new Vector2(.4f,-4.3f)),
            new Fracture(.065f,new Vector2(3.2f,3.05f),new Vector2(2.45f,2.5f),new Vector2(2.65f,1.45f),new Vector2(1.5f,.1f)),
            new Fracture(.06f,new Vector2(.55f,-.7f),new Vector2(1.6f,-1.75f),new Vector2(1.45f,-2.55f),new Vector2(2.65f,-3.45f)),
            new Fracture(.055f,new Vector2(-2.9f,-.55f),new Vector2(-2.55f,-1.65f),new Vector2(-3.15f,-2.65f)),
            new Fracture(.045f,new Vector2(-1.25f,2.9f),new Vector2(-2.45f,2.45f),new Vector2(-3.65f,2.5f)),
            new Fracture(.045f,new Vector2(2.65f,.45f),new Vector2(3.2f,-.6f),new Vector2(4.3f,-.9f)),
            new Fracture(.04f,new Vector2(-.55f,-2.55f),new Vector2(-1.65f,-2.6f),new Vector2(-2.05f,-3.85f)),
            new Fracture(.04f,new Vector2(-.85f,1.2f),new Vector2(-2.05f,1.05f),new Vector2(-2.5f,1.55f))
        };

        public static Color Sample(float x,float z,Color ground,out float normalRelief)
        {
            normalRelief=0f;
            float squared=x*x+z*z;
            if(squared>=Radius*Radius)return ground;
            float radius=Mathf.Sqrt(squared);
            float cover=1f-Smooth(4.08f,Radius,radius);
            float cloud=Mathf.PerlinNoise(x*.78f+47f,z*.78f+91f);
            float grain=Mathf.PerlinNoise(x*7.5f+11f,z*7.5f+29f)-.5f;
            float center=1f-Smooth(.8f,3.4f,radius);
            Color ice=Color.Lerp(new Color(.30f,.58f,.72f,1f),new Color(.83f,.92f,.96f,1f),
                Mathf.Clamp01(.20f+cloud*.55f+center*.24f));
            ice=Color.Lerp(ice,new Color(.73f,.84f,.90f,1f),Smooth(3.65f,4.4f,radius)*.7f);
            ice.r+=grain*.035f;ice.g+=grain*.035f;ice.b+=grain*.025f;

            Vector2 point=new Vector2(x,z);
            float nearest=100f;
            foreach(Fracture fracture in fractures)
                for(int i=1;i<fracture.Points.Length;i++)
                    nearest=Mathf.Min(nearest,Distance(point,fracture.Points[i-1],fracture.Points[i])/fracture.Width);
            float lip=1f-Smooth(.8f,2.8f,nearest);
            float split=1f-Smooth(.28f,1.15f,nearest);
            ice=Color.Lerp(ice,new Color(.86f,.95f,.98f,1f),lip*.65f);
            ice=Color.Lerp(ice,new Color(.075f,.235f,.34f,1f),split*.88f);
            normalRelief=(grain*.007f-split*.024f)*cover;
            return Color.Lerp(ground,ice,cover);
        }

        private static float Distance(Vector2 point,Vector2 a,Vector2 b)
        {
            Vector2 direction=b-a;
            float along=Mathf.Clamp01(Vector2.Dot(point-a,direction)/Mathf.Max(.0001f,direction.sqrMagnitude));
            return (point-(a+direction*along)).magnitude;
        }

        private static float Smooth(float inner,float outer,float value)
            =>Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(inner,outer,value));
    }
}
