using Alterverse.Characters;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.UI
{
public static class StylizedHeroCard
{
public static void Draw(Rect area,HeroId hero,Texture2D pixel,float time){Color accent=StylizedArtDirection.HeroAccent(hero);GUI.color=new Color(.01f,.01f,.025f,.82f);GUI.DrawTexture(area,pixel);GUI.color=new Color(accent.r,accent.g,accent.b,.22f);GUI.DrawTexture(new Rect(area.x+4,area.y+4,area.width-8,area.height-8),pixel);float cx=area.center.x,bottom=area.yMax-18,bob=Mathf.Sin(time*1.3f)*3;GUI.color=StylizedArtDirection.Ink;GUI.DrawTexture(new Rect(cx-area.width*.17f,area.y+area.height*.35f+bob,area.width*.34f,area.height*.48f),pixel);GUI.DrawTexture(new Rect(cx-area.width*.105f,area.y+area.height*.17f+bob,area.width*.21f,area.width*.21f),pixel);float shoulder=hero==HeroId.Kaelon||hero==HeroId.Rook?.27f:.20f;GUI.DrawTexture(new Rect(cx-area.width*shoulder,area.y+area.height*.43f+bob,area.width*shoulder*2,area.height*.16f),pixel);GUI.color=accent;float slash=Mathf.Sin(time*2f)*4;GUI.DrawTexture(new Rect(cx+area.width*.17f,area.y+area.height*.25f+slash,4,area.height*.55f),pixel);GUI.DrawTexture(new Rect(area.x+12,bottom,area.width-24,3),pixel);GUI.color=Color.white;}
}
}
