using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Olive, blue-grey mineral and pale frost colours inspired by the supplied planet reference.</summary>
    public static class Hd137010Surface
    {
        public static Color ColorAt(float x,float z,float tone)
        {
            float continent=Mathf.PerlinNoise(x*.060f+19f,z*.060f+53f);
            float mineral=Mathf.PerlinNoise(x*.115f+71f,z*.115f+29f);
            float frost=Mathf.PerlinNoise(x*.20f+103f,z*.20f+13f);
            Color olive=Color.Lerp(new Color(.25f,.30f,.18f,1f),new Color(.40f,.44f,.28f,1f),
                Mathf.InverseLerp(.30f,.72f,continent));
            Color blueGrey=Color.Lerp(new Color(.18f,.32f,.37f,1f),new Color(.34f,.48f,.51f,1f),mineral);
            float cool=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.36f,.62f,continent));
            Color ground=Color.Lerp(olive,blueGrey,cool*.88f);
            float pale=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.53f,.75f,frost))*(.22f+cool*.44f);
            ground=Color.Lerp(ground,new Color(.71f,.77f,.75f,1f),pale);
            // Existing crater tone supplies depth; the bright reference does not flatten the geology.
            float relief=.88f+.24f*Mathf.InverseLerp(.30f,.68f,tone);
            ground.r*=relief;ground.g*=relief;ground.b*=relief;
            return ground;
        }
    }
}
