using UnityEngine;
namespace Alterverse.UI
{
public static class DimensionUiMotif
{
public static void DrawSplitPanel(Rect r,Texture2D pixel,Color zero,Color lumen,float time){if(pixel==null||r.width<=0||r.height<=0)return;Color old=GUI.color;GUI.color=new Color(0,0,0,.72f);GUI.DrawTexture(r,pixel);float inset=Mathf.Min(3f,Mathf.Min(r.width,r.height)*.08f),innerH=Mathf.Max(0,r.height-inset*2),split=r.x+r.width*.5f+Mathf.Sin(time*.8f)*r.width*.012f;GUI.color=new Color(zero.r,zero.g,zero.b,.13f);GUI.DrawTexture(new Rect(r.x+inset,r.y+inset,Mathf.Max(0,split-r.x-inset),innerH),pixel);GUI.color=new Color(lumen.r,lumen.g,lumen.b,.13f);GUI.DrawTexture(new Rect(split,r.y+inset,Mathf.Max(0,r.xMax-split-inset),innerH),pixel);if(r.width>=40&&r.height>=14){GUI.color=new Color(1,1,1,.08f);GUI.DrawTexture(new Rect(r.x+8,r.y+7,r.width*.27f,2),pixel);GUI.DrawTexture(new Rect(r.xMax-r.width*.22f-8,r.yMax-9,r.width*.22f,2),pixel);}for(int i=0;i<5;i++){float y=r.y+r.height*(.12f+i*.19f),drift=Mathf.Sin(time*1.2f+i*1.7f)*Mathf.Min(3f,r.width*.01f);GUI.color=i%2==0?new Color(zero.r,zero.g,zero.b,.48f):new Color(lumen.r,lumen.g,lumen.b,.48f);GUI.DrawTexture(new Rect(split-1+drift,y,2,Mathf.Max(1,r.height*.08f)),pixel);}GUI.color=old;}
public static void DrawCornerCuts(Rect r,Texture2D pixel,Color accent){if(pixel==null||r.width<=0||r.height<=0)return;Color old=GUI.color;GUI.color=new Color(accent.r,accent.g,accent.b,.7f);float w=Mathf.Min(r.width*.45f,Mathf.Max(8,r.width*.08f)),h=Mathf.Min(18,r.height*.18f),t=Mathf.Min(2f,Mathf.Max(1f,r.height*.08f));GUI.DrawTexture(new Rect(r.x,r.y,w,t),pixel);GUI.DrawTexture(new Rect(r.x,r.y,t,h),pixel);GUI.DrawTexture(new Rect(r.xMax-w,r.yMax-t,w,t),pixel);GUI.DrawTexture(new Rect(r.xMax-t,r.yMax-h,t,h),pixel);GUI.color=old;}
}
}
