using System.Collections.Generic;using Alterverse.Profile;using UnityEngine;
namespace Alterverse.UI
{
public static class ProfileAvatarPanel
{
public static int Draw(Rect area,Texture2D pixel,float time,int index){List<AvatarChoice> choices=AvatarSelection.Owned();if(choices.Count==0)return 0;index=Mathf.Clamp(index,0,choices.Count-1);AvatarChoice current=choices[index];float badge=Mathf.Min(area.height*.56f,96f);Rect badgeRect=new Rect(area.center.x-badge*.5f,area.y+6,badge,badge);AvatarBadgeRenderer.Draw(badgeRect,current.Id,pixel,time);GUI.Label(new Rect(area.x,area.y+badge+12,area.width,24),current.Label,new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold});GUI.Label(new Rect(area.x,area.y+badge+36,area.width,20),current.Theme,new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=11});if(GUI.Button(new Rect(area.x+8,area.center.y-20,42,40),"‹"))index=(index-1+choices.Count)%choices.Count;if(GUI.Button(new Rect(area.xMax-50,area.center.y-20,42,40),"›"))index=(index+1)%choices.Count;if(GUI.Button(new Rect(area.center.x-62,area.yMax-38,124,34),PlayerProfile.Avatar==choices[index].Id?"EQUIPPED":"EQUIP"))PlayerProfile.Avatar=choices[index].Id;return index;}
}
}
