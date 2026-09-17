using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.UI
{
public sealed class CombatTeamMarkers:MonoBehaviour
{
private Texture2D pixel;private Camera cam;private TeamMember playerTeam;private GUIStyle enemyLabel;private float nextResolve;
private void Awake(){pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();cam=Camera.main;ResolvePlayer();enemyLabel=new GUIStyle(GUI.skin.label){fontSize=11,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};}
private void Update(){if(cam==null)cam=Camera.main;if(playerTeam==null&&Time.unscaledTime>=nextResolve)ResolvePlayer();}
private void ResolvePlayer(){nextResolve=Time.unscaledTime+.5f;GameObject p=GameObject.Find("AV_Player");if(p!=null)playerTeam=p.GetComponent<TeamMember>();}
private void OnGUI(){if(pixel==null||cam==null||playerTeam==null||Time.timeScale<=0f)return;float scale=Mathf.Clamp(Screen.height/900f,.72f,1.2f);foreach(HeroIdentity hero in FindObjectsByType<HeroIdentity>()){if(hero==null||hero.gameObject.name=="AV_Player")continue;TeamMember tm=hero.GetComponent<TeamMember>();Health hp=hero.GetComponent<Health>();if(tm==null||hp==null||hp.IsDead)continue;bool enemy=playerTeam.IsEnemy(tm);DrawHeroMarker(hero.transform,hp,enemy,scale);}}
private void DrawHeroMarker(Transform hero,Health hp,bool enemy,float scale){Vector3 s=cam.WorldToScreenPoint(hero.position+Vector3.up*2.25f);if(s.z<=0)return;s.y=Screen.height-s.y;float w=enemy?58*scale:42*scale,h=5*scale;Color old=GUI.color;if(enemy){Color red=new Color(1f,.16f,.18f,.98f);GUI.color=red;float marker=11*scale;GUI.DrawTexture(new Rect(s.x-marker*.5f,s.y-19*scale,marker,marker*.55f),pixel);GUI.DrawTexture(new Rect(s.x-1.5f*scale,s.y-14*scale,3*scale,8*scale),pixel);GUI.color=new Color(0,0,0,.82f);GUI.DrawTexture(new Rect(s.x-w*.5f-1,s.y-4*scale,w+2,h+2),pixel);GUI.color=red;GUI.DrawTexture(new Rect(s.x-w*.5f,s.y-3*scale,w*hp.Normalized,h),pixel);GUI.color=Color.white;GUI.Label(new Rect(s.x-38*scale,s.y-34*scale,76*scale,14*scale),"ENEMY",enemyLabel);}else{GUI.color=new Color(.25f,.78f,1f,.8f);GUI.DrawTexture(new Rect(s.x-w*.5f,s.y-2*scale,w*hp.Normalized,3*scale),pixel);}GUI.color=old;}
private void OnDestroy(){if(pixel!=null)Destroy(pixel);}
}
}
