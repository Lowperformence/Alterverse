using UnityEngine;
namespace Alterverse.UI
{
    public static class AvatarBadgeRenderer
    {
        private static Texture2D atlas;
        private static bool loaded;
        public static void Draw(Rect rect,int avatarId,Texture2D pixel,float time)
        {
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
    }
}
