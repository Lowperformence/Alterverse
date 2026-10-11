using UnityEngine;
namespace Alterverse.Visuals
{
    // Sampled Rigify chest deformation, transferred with Rokoko 1.4.3.
    // Unit curve only; HeroVisualMotion owns amplitude, combat blending and joints.
    public static class HeroIdleBreath
    {
        static readonly float[] keys={0.0000000f,0.1671299f,0.3291996f,0.4813696f,0.6192781f,0.7391501f,0.8381537f,0.9142506f,0.9664595f,0.9947496f,0.9999715f,0.9838023f,0.9485869f,0.8971477f,0.8325660f,0.7580014f,0.6765354f,0.5908739f,0.5033450f,0.4157639f,0.3293136f,0.2446190f,0.1618059f,0.0804825f,-0.0000024f,-0.0805181f,-0.1618368f,-0.2446712f,-0.3293540f,-0.4158162f,-0.5034044f,-0.5909000f,-0.6765711f,-0.7580466f,-0.8326182f,-0.8971810f,-0.9486297f,-0.9838332f,-1.0000000f,-0.9947829f,-0.9665213f,-0.9142933f,-0.8382083f,-0.7392023f,-0.6193089f,-0.4814194f,-0.3292471f,-0.1671656f,0.0000000f};
        public static float Sample(float time)
        {
            float p=Mathf.Repeat(time/4.6f,1f)*(keys.Length-1);
            int i=(int)p;
            return Mathf.Lerp(keys[i],keys[i+1],p-i);
        }
    }
}
