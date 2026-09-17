using UnityEngine;
namespace Alterverse.UI
{
public static class DimensionUiMotif
{
public static void DrawSplitPanel(Rect r,Texture2D pixel,Color zero,Color lumen,float time){if(pixel==null)return;Color old=GUI.color;GUI.color=new Color(0,0,0,.72f);GUI.DrawTexture(r,pixel);float split=r.x+r.width*.5f+Mathf.Sin(time*.8f)*r.width*.012f;GUI.color=new Color(zero.r,zero.g,zero.b,.13f);GUI.DrawTexture(new Rect(r.x+3,r.y+3,Mathf.Max(0,split-r.x-3),r.height-6),pixel);GUI.color=new Color(lumen.r,lumen.g,lumen.b,.13f);GUI.DrawTexture(new Rect(split,r.y+3,Mathf.Max(0,r.xMax-split-3),r.height-6),pixel);GUI.color=new Color(1,1,1,.08f);GUI.DrawTexture(new Rect(r.x+8,r.y+7,r.width*.27f,2),pixel);GUI.DrawTexture(new Rect(r.xMax-r.width*.22f-8,r.yMax-9,r.width*.22f,2),pixel);for(int i=0;i<5;i++){float y=r.y+r.height*(.12f+i*.19f);float drift=Mathf.Sin(time*1.2f+i*1.7f)*3f;GUI.color=i%2==0?new Color(zero.r,zero.g,zero.b,.48f):new Color(lumen.r,lumen.g,lumen.b,.48f);GUI.DrawTexture(new Rect(split-1+drift,y,2,r.height*.08f),pixel);}GUI.color=old;}
public static void DrawCornerCuts(Rect r,Texture2D pixel,Color accent){if(pixel==null)return;Color old=GUI.color;GUI.color=new Color(accent.r,accent.g,accent.b,.7f);float w=Mathf.Max(12,r.width*.08f);GUI.DrawTexture(new Rect(r.x,r.y,w,2),pixel);GUI.DrawTexture(new Rect(r.x,r.y,2,Mathf.Min(18,r.height*.18f)),pixel);GUI.DrawTexture(new Rect(r.xMax-w,r.yMax-2,w,2),pixel);GUI.DrawTexture(new Rect(r.xMax-2,r.yMax-Mathf.Min(18,r.height*.18f),2,Mathf.Min(18,r.height*.18f)),pixel);GUI.color=old;}
}
}
