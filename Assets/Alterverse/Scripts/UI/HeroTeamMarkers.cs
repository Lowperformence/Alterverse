using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.UI
{
public sealed class HeroTeamMarkers:MonoBehaviour
{
private static readonly Color EnemyColor=new(1f,.16f,.13f,.98f);
private static readonly Color AllyColor=new(.20f,.86f,1f,.94f);
private static readonly Color FrameColor=new(.018f,.022f,.03f,.94f);
private static readonly Color ShieldColor=new(.30f,.68f,1f,1f);
private Texture2D pixel;private Camera cam;private GUIStyle style;private TeamMember player;private HeroIdentity[] heroes=System.Array.Empty<HeroIdentity>();private float nextRefresh;
private void Awake(){pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();cam=Camera.main;RefreshTargets();}
private void Update(){if(cam==null)cam=Camera.main;if(Time.unscaledTime>=nextRefresh)RefreshTargets();}
private void RefreshTargets(){nextRefresh=Time.unscaledTime+1f;if(player==null){GameObject p=GameObject.Find("AV_Player");if(p!=null)player=p.GetComponent<TeamMember>();}heroes=FindObjectsByType<HeroIdentity>(FindObjectsInactive.Exclude);}
private void OnGUI()
{
if(cam==null||pixel==null||Time.timeScale<=0f||player==null)return;
foreach(HeroIdentity hero in heroes)
{
if(hero==null||hero.gameObject.name=="AV_Player")continue;
Health hp=hero.GetComponent<Health>();TeamMember tm=hero.GetComponent<TeamMember>();
if(hp==null||hp.IsDead||tm==null)continue;
var motion=hero.GetComponent<Alterverse.Visuals.HeroVisualMotion>();
Vector3 anchor=Alterverse.Visuals.HeroPresentationAnchor.Position(hero.transform,motion);
Vector3 p=cam.WorldToScreenPoint(anchor+Vector3.up*1.65f);
if(p.z<=0)continue;
p.y=Screen.height-p.y;
if(p.x<-80f||p.x>Screen.width+80f||p.y<-80f||p.y>Screen.height+80f)continue;

bool enemy=tm.Team!=player.Team;
float scale=Mathf.Clamp(Screen.height/900f,.72f,1.2f);
float w=(enemy?60f:46f)*scale,h=8f*scale;
Color team=enemy?EnemyColor:AllyColor;
Color old=GUI.color;
Rect frame=new Rect(p.x-w*.5f,p.y,w,h);

GUI.color=FrameColor;GUI.DrawTexture(frame,pixel);
GUI.color=new Color(FrontEndTheme.Accent.r,FrontEndTheme.Accent.g,FrontEndTheme.Accent.b,.7f);
GUI.DrawTexture(new Rect(frame.x,frame.y,frame.width,1f*scale),pixel);
GUI.DrawTexture(new Rect(frame.x,frame.yMax-1f*scale,frame.width,1f*scale),pixel);

float inset=2f*scale;
float innerWidth=Mathf.Max(0f,frame.width-inset*2f);
GUI.color=team;
GUI.DrawTexture(new Rect(frame.x+inset,frame.y+inset,innerWidth*Mathf.Clamp01(hp.Normalized),Mathf.Max(1f,frame.height-inset*2f)),pixel);

EnsureStyle(scale);
GUI.color=Color.white;
GUI.Label(new Rect(p.x-w*.8f,p.y-16f*scale,w*1.6f,14f*scale),$"{hp.Current:0} / {hp.Max:0}",style);
DrawShield(frame,hp,scale);

if(enemy)DrawEnemyCrystal(p.x,p.y-24f*scale,scale);
else
{
GUI.color=AllyColor;
GUI.DrawTexture(new Rect(p.x-4f*scale,p.y-4f*scale,8f*scale,2f*scale),pixel);
}
GUI.color=old;
}
}
private void DrawShield(Rect healthFrame,Health hp,float scale)
{
float shield=hp.Shield;
if(!(shield>0f))return;
// The already-replicated total includes base and temporary ability shields.
// Base shield capacity keeps a normal shield readable even on high-HP heroes;
// the number also represents shields above that capacity without truncation.
float capacity=hp.HeroBaseShieldMax>0f?hp.HeroBaseShieldMax:Mathf.Max(1f,hp.Max);
Rect frame=new Rect(healthFrame.x,healthFrame.yMax+2f*scale,healthFrame.width,6f*scale);
float inset=1f*scale,width=Mathf.Max(0f,frame.width-inset*2f);
GUI.color=FrameColor;GUI.DrawTexture(frame,pixel);
GUI.color=new Color(.035f,.10f,.18f,.95f);
GUI.DrawTexture(new Rect(frame.x+inset,frame.y+inset,width,frame.height-inset*2f),pixel);
GUI.color=ShieldColor;
float fill=Mathf.Min(width,Mathf.Max(2f*scale,width*Mathf.Clamp01(shield/Mathf.Max(1f,capacity))));
GUI.DrawTexture(new Rect(frame.x+inset,frame.y+inset,fill,frame.height-inset*2f),pixel);
GUI.color=Color.white;
GUI.Label(new Rect(frame.x,frame.yMax+1f*scale,frame.width,12f*scale),$"+{Mathf.CeilToInt(shield)}",style);
}
private void DrawEnemyCrystal(float cx,float cy,float scale)
{
float size=10f*scale;
Matrix4x4 oldMatrix=GUI.matrix;
Color oldColor=GUI.color;
GUIUtility.RotateAroundPivot(45f,new Vector2(cx,cy));
GUI.color=new Color(.025f,.015f,.02f,.96f);
GUI.DrawTexture(new Rect(cx-size*.62f,cy-size*.62f,size*1.24f,size*1.24f),pixel);
GUI.color=EnemyColor;
GUI.DrawTexture(new Rect(cx-size*.5f,cy-size*.5f,size,size),pixel);
float inner=size*.42f;
GUI.color=new Color(1f,.66f,.58f,.98f);
GUI.DrawTexture(new Rect(cx-inner*.5f,cy-inner*.5f,inner,inner),pixel);
GUI.matrix=oldMatrix;
GUI.color=oldColor;
}
private void EnsureStyle(float scale){int size=Mathf.RoundToInt(10*scale);if(style!=null&&style.fontSize==size)return;style=new GUIStyle(GUI.skin.label){font=FrontEndTypography.Display,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=size,normal={textColor=Color.white}};}
private void OnDestroy(){if(pixel!=null)Destroy(pixel);}
}
}



