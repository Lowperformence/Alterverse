using Alterverse.Profile;using UnityEngine;
namespace Alterverse.UI
{
public static class ProfileStatsPanel
{
private static GUIStyle valueStyle,labelStyle;public static void Draw(Rect area,Texture2D pixel){valueStyle??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=18};labelStyle??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=10};ProfileStats s=ProfileStatsSummary.Current;string[] labels={"MATCHES","WINS","LOSSES","WIN RATE"};string[] values={s.Matches.ToString(),s.Wins.ToString(),s.Losses.ToString(),s.WinRate.ToString("0")+"%"};float cell=area.width/4f;for(int i=0;i<4;i++){Rect r=new Rect(area.x+i*cell,area.y,cell,area.height);GUI.Label(new Rect(r.x,r.y,r.width,22),values[i],valueStyle);GUI.Label(new Rect(r.x,r.y+23,r.width,18),labels[i],labelStyle);if(i>0&&pixel!=null){Color old=GUI.color;GUI.color=new Color(1,1,1,.12f);GUI.DrawTexture(new Rect(r.x,r.y+5,1,area.height-10),pixel);GUI.color=old;}}}
}
}
