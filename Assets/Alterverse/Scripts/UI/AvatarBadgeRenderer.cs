using Alterverse.Profile;
using UnityEngine;
namespace Alterverse.UI
{
    public static class AvatarBadgeRenderer
    {
        private static Texture2D atlas;
        private static bool loaded;
        private static readonly Texture2D[] extra=new Texture2D[10];
        private static GUIStyle betaLabel;
        public static void Draw(Rect rect,int avatarId,Texture2D pixel,float time)
        {
            if(avatarId==BattlePassProgress.BetaCrownAvatarId){DrawBetaCrown(rect);return;}
            if(avatarId>=17&&avatarId<=26)
            {
                int index=avatarId-17;
                if(extra[index]==null)extra[index]=Resources.Load<Texture2D>("Avatars/Avatar"+avatarId);
                if(extra[index]!=null){Color previous=GUI.color;GUI.color=Color.white;GUI.DrawTexture(rect,extra[index],ScaleMode.ScaleToFit,true);GUI.color=previous;}
                return;
            }
            if(!loaded){atlas=Resources.Load<Texture2D>("Avatars/CosmicAtlas");loaded=true;}
            if(atlas==null)return;
            // Existing ownership IDs stay valid; legacy starter IDs share the two starter illustrations.
            int tile=avatarId>=10&&avatarId<=15?avatarId-10:avatarId==6?8:6+(avatarId%2);
            int column=tile%3,row=tile/3;
            // Inset sample avoids adjacent-cell filtering. The atlas uses top-to-bottom art order.
            const float inset=.004f;
            Rect uv=new Rect(column/3f+inset,1-(row+1)/3f+inset,1/3f-2*inset,1/3f-2*inset);
            Color before=GUI.color;GUI.color=Color.white;GUI.DrawTextureWithTexCoords(rect,atlas,uv);GUI.color=before;
        }

        private static void DrawBetaCrown(Rect rect)
        {
            Color dark=new(.035f,.025f,.055f,.98f);
            Color gold=new(1f,.68f,.16f,1f);
            Color zero=new(.72f,.16f,.92f,1f);
            Color lumen=new(.10f,.92f,1f,1f);
            FrontEndTheme.Fill(rect,dark);
            FrontEndTheme.Fill(new Rect(rect.x,rect.y,rect.width,2),zero);
            FrontEndTheme.Fill(new Rect(rect.x,rect.yMax-2,rect.width,2),lumen);

            float cx=rect.center.x,baseY=rect.y+rect.height*.58f,w=rect.width*.58f,h=rect.height*.30f;
            FrontEndTheme.Fill(new Rect(cx-w*.5f,baseY,w,h*.25f),gold);
            FrontEndTheme.Fill(new Rect(cx-w*.43f,baseY-h*.52f,w*.16f,h*.62f),gold);
            FrontEndTheme.Fill(new Rect(cx-w*.08f,baseY-h*.78f,w*.16f,h*.88f),gold);
            FrontEndTheme.Fill(new Rect(cx+w*.27f,baseY-h*.52f,w*.16f,h*.62f),gold);
            FrontEndTheme.Fill(new Rect(cx-w*.46f,baseY+h*.24f,w*.92f,h*.12f),new Color(.68f,.36f,.08f,1f));

            betaLabel??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=10};
            Color before=GUI.color;GUI.color=Color.white;
            GUI.Label(new Rect(rect.x,rect.yMax-rect.height*.28f,rect.width,rect.height*.20f),"BETA",betaLabel);
            GUI.color=before;
        }
    }
}
