using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    internal static class BattleItemIconRenderer
    {
        public static Color Accent(BattleItemType item)=>item switch
        {
            BattleItemType.Heal=>new Color(.12f,1f,.34f),
            BattleItemType.Shield=>new Color(.18f,.82f,1f),
            BattleItemType.Speed=>new Color(1f,.72f,.18f),
            BattleItemType.TeamHeal=>new Color(.28f,1f,.72f),
            _=>FrontEndTheme.Muted
        };

        public static void Draw(Rect rect,BattleItemType item,bool equipped=false)
        {
            Color accent=Accent(item);
            FrontEndTheme.Fill(rect,new Color(.035f,.045f,.062f,.98f));
            FrontEndTheme.Fill(new Rect(rect.x,rect.y,rect.width,2),accent);
            FrontEndTheme.Fill(new Rect(rect.x,rect.yMax-2,rect.width,2),new Color(accent.r,accent.g,accent.b,.38f));
            if(equipped)FrontEndTheme.Fill(new Rect(rect.x,rect.y,4,rect.height),accent);

            Vector2 c=rect.center;
            float s=Mathf.Min(rect.width,rect.height);
            Rect core=new(c.x-s*.16f,c.y-s*.16f,s*.32f,s*.32f);
            FrontEndTheme.Fill(core,new Color(accent.r,accent.g,accent.b,.28f));
            Rect inner=new(c.x-s*.085f,c.y-s*.085f,s*.17f,s*.17f);
            FrontEndTheme.Fill(inner,accent);

            switch(item)
            {
                case BattleItemType.Heal:
                    FrontEndTheme.Fill(new Rect(c.x-s*.035f,c.y-s*.18f,s*.07f,s*.36f),Color.white);
                    FrontEndTheme.Fill(new Rect(c.x-s*.18f,c.y-s*.035f,s*.36f,s*.07f),Color.white);
                    break;
                case BattleItemType.Shield:
                    DrawShield(c,s,accent);
                    break;
                case BattleItemType.Speed:
                    DrawSpeed(c,s,accent);
                    break;
                case BattleItemType.TeamHeal:
                    DrawTeamHeal(c,s,accent);
                    break;
            }

            // Small orbit markers keep every icon in the same Dimensions Front visual family.
            for(int i=0;i<4;i++)
            {
                float a=(Time.unscaledTime*.35f+i*.25f)*Mathf.PI*2f;
                float r=s*.31f;
                Rect mote=new(c.x+Mathf.Cos(a)*r-s*.018f,c.y+Mathf.Sin(a)*r-s*.018f,s*.036f,s*.036f);
                FrontEndTheme.Fill(mote,new Color(accent.r,accent.g,accent.b,.72f));
            }
        }

        private static void DrawShield(Vector2 c,float s,Color accent)
        {
            float w=s*.30f,h=s*.34f;
            FrontEndTheme.Fill(new Rect(c.x-w*.5f,c.y-h*.52f,w,h*.12f),Color.white);
            FrontEndTheme.Fill(new Rect(c.x-w*.5f,c.y-h*.40f,w*.11f,h*.54f),Color.white);
            FrontEndTheme.Fill(new Rect(c.x+w*.39f,c.y-h*.40f,w*.11f,h*.54f),Color.white);
            FrontEndTheme.Fill(new Rect(c.x-w*.26f,c.y+h*.08f,w*.52f,h*.10f),Color.white);
            FrontEndTheme.Fill(new Rect(c.x-w*.11f,c.y+h*.18f,w*.22f,h*.09f),accent);
        }

        private static void DrawSpeed(Vector2 c,float s,Color accent)
        {
            FrontEndTheme.Fill(new Rect(c.x-s*.22f,c.y-s*.09f,s*.30f,s*.07f),Color.white);
            FrontEndTheme.Fill(new Rect(c.x-s*.14f,c.y+s*.03f,s*.27f,s*.06f),Color.white);
            FrontEndTheme.Fill(new Rect(c.x+s*.04f,c.y-s*.16f,s*.07f,s*.32f),accent);
            FrontEndTheme.Fill(new Rect(c.x+s*.10f,c.y-s*.16f,s*.16f,s*.07f),accent);
            FrontEndTheme.Fill(new Rect(c.x+s*.10f,c.y+s*.09f,s*.16f,s*.07f),accent);
        }

        private static void DrawTeamHeal(Vector2 c,float s,Color accent)
        {
            FrontEndTheme.Fill(new Rect(c.x-s*.035f,c.y-s*.19f,s*.07f,s*.38f),Color.white);
            FrontEndTheme.Fill(new Rect(c.x-s*.19f,c.y-s*.035f,s*.38f,s*.07f),Color.white);
            FrontEndTheme.Fill(new Rect(c.x-s*.29f,c.y+s*.17f,s*.12f,s*.12f),accent);
            FrontEndTheme.Fill(new Rect(c.x+s*.17f,c.y+s*.17f,s*.12f,s*.12f),accent);
            FrontEndTheme.Fill(new Rect(c.x-s*.06f,c.y+s*.23f,s*.12f,s*.12f),accent);
        }
    }
}
