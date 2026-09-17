using Alterverse.Profile;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.UI
{
public static class AvatarBadgeRenderer
{
private static GUIStyle badgeStyle;public static void Draw(Rect rect,int avatarId,Texture2D pixel,float time){if(pixel==null)return;badgeStyle??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};Color old=GUI.color;Color a=avatarId%2==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy,b=avatarId%2==0?StylizedArtDirection.LumenEnergy:StylizedArtDirection.ZeroEnergy;GUI.color=new Color(0.025f,.03f,.055f,.96f);GUI.DrawTexture(rect,pixel);float cut=Mathf.Max(3,rect.width*.06f);GUI.color=a;GUI.DrawTexture(new Rect(rect.x,rect.y,cut,rect.height),pixel);GUI.color=b;GUI.DrawTexture(new Rect(rect.xMax-cut,rect.y,cut,rect.height),pixel);float pulse=.5f+.5f*Mathf.Sin(time*1.7f+avatarId);GUI.color=new Color(1,1,1,.14f+.14f*pulse);GUI.DrawTexture(new Rect(rect.center.x-1,rect.y+rect.height*.12f,2,rect.height*.76f),pixel);GUI.color=old;badgeStyle.fontSize=Mathf.Clamp((int)(rect.height*.36f),12,24);string label=avatarId==AvatarInventory.FounderAvatarId?"◆":avatarId>=10?"✦":(avatarId+1).ToString();GUI.Label(rect,label,badgeStyle);}
}
}
