using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.UI
{
public sealed class HeroTeamMarkers:MonoBehaviour
{
private Texture2D pixel;private Camera cam;private GUIStyle style;private TeamMember player;private HeroIdentity[] heroes=System.Array.Empty<HeroIdentity>();private float nextRefresh;
private void Awake(){pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();cam=Camera.main;RefreshTargets();}
private void Update(){if(cam==null)cam=Camera.main;if(Time.unscaledTime>=nextRefresh)RefreshTargets();}
private void RefreshTargets(){nextRefresh=Time.unscaledTime+1f;if(player==null){GameObject p=GameObject.Find("AV_Player");if(p!=null)player=p.GetComponent<TeamMember>();}heroes=FindObjectsByType<HeroIdentity>(FindObjectsInactive.Exclude);}
private void OnGUI(){if(cam==null||pixel==null||Time.timeScale<=0f||player==null)return;foreach(HeroIdentity hero in heroes){if(hero==null||hero.gameObject.name=="AV_Player")continue;Health hp=hero.GetComponent<Health>();TeamMember tm=hero.GetComponent<TeamMember>();if(hp==null||hp.IsDead||tm==null)continue;Vector3 p=cam.WorldToScreenPoint(hero.transform.position+Vector3.up*1.65f);if(p.z<=0)continue;p.y=Screen.height-p.y;bool enemy=tm.Team!=player.Team;float scale=Mathf.Clamp(Screen.height/900f,.72f,1.2f);float w=(enemy?54f:38f)*scale,h=6f*scale;Color old=GUI.color;float innerW=Mathf.Max(0f,w-4f);GUI.color=new Color(.04f,.04f,.05f,.92f);GUI.DrawTexture(new Rect(p.x-w*.5f,p.y,w,h),pixel);GUI.color=enemy?new Color(1f,.12f,.12f,.96f):new Color(.2f,.8f,1f,.88f);GUI.DrawTexture(new Rect(p.x-w*.5f+2,p.y+2,innerW*Mathf.Clamp01(hp.Normalized),Mathf.Max(1,h-4)),pixel);EnsureStyle(scale);GUI.color=Color.white;GUI.Label(new Rect(p.x-w*.75f,p.y-15f*scale,w*1.5f,14f*scale),$"{hp.Current:0} / {hp.Max:0}",style);if(enemy){GUI.color=new Color(1f,.12f,.12f,.98f);float s=10f*scale;GUI.DrawTexture(new Rect(p.x-s*.5f,p.y-14f*scale,s,s),pixel);EnsureStyle(scale);GUI.Label(new Rect(p.x-40*scale,p.y-31*scale,80*scale,14*scale),"ENEMY",style);}GUI.color=old;}}
private void EnsureStyle(float scale){int size=Mathf.RoundToInt(10*scale);if(style!=null&&style.fontSize==size)return;style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=size,normal={textColor=Color.white}};}
private void OnDestroy(){if(pixel!=null)Destroy(pixel);}
}
}