// Offline-only math/byte adapters. This does not depend on a Unity installation.
// Gradient noise is deterministic; Unity's native Perlin implementation may differ.
using System;
namespace Alterverse.Characters { /* HERO_ENUM */ public static class HeroIdentity { public static readonly int HeroCount=Enum.GetValues<HeroId>().Length; } }
namespace UnityEngine
{
    public class Object { public static void Destroy(Object value){} }
    public class Texture2D:Object { public int width,height; }
    public class Sprite:Object
    {
        public string name;
        public static Sprite Create(Texture2D t,Rect r,Vector2 p,float units,uint border,SpriteMeshType mesh)=>new();
    }
    public enum SpriteMeshType { FullRect }
    public static class Resources { public static T Load<T>(string path) where T:class=>null; }
    public static class Debug { public static void LogWarning(object message)=>Console.WriteLine(message); }
    public struct Rect { public Rect(float x,float y,float w,float h){} }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
    public struct Color32
    {
        public byte r,g,b,a;
        public static implicit operator Color32(Color c)=>new(){r=(byte)Mathf.RoundToInt(Mathf.Clamp01(c.r)*255),g=(byte)Mathf.RoundToInt(Mathf.Clamp01(c.g)*255),b=(byte)Mathf.RoundToInt(Mathf.Clamp01(c.b)*255),a=(byte)Mathf.RoundToInt(Mathf.Clamp01(c.a)*255)};
        public static implicit operator Color(Color32 c)=>new(c.r/255f,c.g/255f,c.b/255f,c.a/255f);
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color operator*(Color c,float n)=>new(c.r*n,c.g*n,c.b*n,c.a*n);
        public static Color operator+(Color a,Color b)=>new(a.r+b.r,a.g+b.g,a.b+b.b,a.a+b.a);
        public static Color Lerp(Color a,Color b,float t){t=Mathf.Clamp01(t);return new(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
    }
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Clamp01(float x)=>Math.Clamp(x,0,1);
        public static int RoundToInt(float x)=>(int)MathF.Round(x);
        public static float Pow(float x,float y)=>MathF.Pow(x,y);
        public static float Abs(float x)=>MathF.Abs(x);
        public static float Exp(float x)=>MathF.Exp(x);
        public static float Sqrt(float x)=>MathF.Sqrt(x);
        public static float Sin(float x)=>MathF.Sin(x);
        public static float Min(float x,float y)=>MathF.Min(x,y);
        public static float Max(float x,float y)=>MathF.Max(x,y);
        public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
        private static float Fade(float t)=>t*t*t*(t*(t*6-15)+10);
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static uint Hash(int x,int y)
        {
            uint h=unchecked((uint)x*0x8da6b343u^(uint)y*0xd8163841u);
            h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;return h^(h>>16);
        }
        private static float Grad(int x,int y,float dx,float dy)=> (Hash(x,y)&7) switch
        {
            0=>dx,1=>-dx,2=>dy,3=>-dy,4=>(dx+dy)*.70710678f,5=>(dx-dy)*.70710678f,6=>(dy-dx)*.70710678f,_=>(-dx-dy)*.70710678f
        };
        public static float PerlinNoise(float x,float y)
        {
            int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y);float fx=x-ix,fy=y-iy;
            float a=Lerp(Grad(ix,iy,fx,fy),Grad(ix+1,iy,fx-1,fy),Fade(fx));
            float b=Lerp(Grad(ix,iy+1,fx,fy-1),Grad(ix+1,iy+1,fx-1,fy-1),Fade(fx));
            return Clamp01(.5f+Lerp(a,b,Fade(fy))*.75f);
        }
    }
}
