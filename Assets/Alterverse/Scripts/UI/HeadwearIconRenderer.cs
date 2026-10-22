using UnityEngine;

namespace Alterverse.UI
{
    internal static class HeadwearIconRenderer
    {
        static GUIStyle label;
        static Texture2D ufoIcon;
        static float ufoLastLoad=-1f;
        public static void Draw(Rect rect,Alterverse.Shop.UniversalHeroCosmetic item=Alterverse.Shop.UniversalHeroCosmetic.BetaCap)
        {
            if(item==Alterverse.Shop.UniversalHeroCosmetic.UfoTestHat)
            {
                // An Editor refresh can import the icon after its first draw. Cache only
                // successful loads; throttle retries while the resource is unavailable.
                float now=Time.unscaledTime;
                if(ufoIcon==null&&(ufoLastLoad<0f||now<ufoLastLoad||now-ufoLastLoad>=1f))
                {ufoLastLoad=now;ufoIcon=Resources.Load<Texture2D>("Headwear/UfoTestHatIcon");}
                if(ufoIcon!=null)GUI.DrawTexture(rect,ufoIcon,ScaleMode.ScaleToFit,true);
                else GUI.Label(rect,"UFO");
                return;
            }
            Color old=GUI.color;
            Vector2 c=rect.center;
            float s=Mathf.Min(rect.width,rect.height);
            for(int i=0;i<10;i++)
            {
                float y=i/10f;
                float width=Mathf.Sqrt(1f-(1f-y)*(1f-y))*s*.72f;
                FrontEndTheme.Fill(new Rect(c.x-width*.5f,c.y-s*.38f+i*s*.045f,width,s*.046f),new Color(.10f,.16f,.27f));
            }
            FrontEndTheme.Fill(new Rect(c.x-s*.36f,c.y+s*.03f,s*.72f,s*.055f),new Color(.35f,.77f,.96f));
            FrontEndTheme.Fill(new Rect(c.x-s*.06f,c.y+s*.09f,s*.53f,s*.13f),new Color(.06f,.09f,.15f));
            FrontEndTheme.Fill(new Rect(c.x-s*.06f,c.y+s*.20f,s*.53f,s*.025f),new Color(.35f,.77f,.96f));
            FrontEndTheme.Fill(new Rect(c.x-s*.025f,c.y-s*.405f,s*.05f,s*.035f),new Color(.95f,.77f,.38f));
            label??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            label.fontSize=Mathf.Max(9,Mathf.RoundToInt(s*.16f));
            GUI.color=Color.white;
            GUI.Label(new Rect(c.x-s*.31f,c.y-s*.25f,s*.62f,s*.26f),"BETA",label);
            GUI.color=old;
        }
    }
}
