using Alterverse.Combat;using Alterverse.Match;using UnityEngine;
namespace Alterverse.UI
{
public sealed class TargetIndicatorHUD:MonoBehaviour
{
private SoftTargeting targeting;private Texture2D pixel;private GUIStyle label;private void Start(){pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();}private void Update(){if(targeting==null){GameObject p=GameObject.Find("AV_Player");if(p!=null)targeting=p.GetComponent<SoftTargeting>();}}
private void OnGUI(){Health h=targeting!=null?targeting.Current:null;if(h==null||Camera.main==null)return;Vector3 s=Camera.main.WorldToScreenPoint(h.transform.position+Vector3.up*1.25f);if(s.z<=0)return;s.y=Screen.height-s.y;float size=h.GetComponent<DimensionPlanet>()!=null?62:46;Color old=GUI.color;GUI.color=h.GetComponent<DimensionPlanet>()!=null?new Color(1,.72f,.18f,.9f):new Color(1,.25f,.28f,.9f);float t=3;GUI.DrawTexture(new Rect(s.x-size*.5f,s.y-size*.5f,size*.28f,t),pixel);GUI.DrawTexture(new Rect(s.x+size*.22f,s.y-size*.5f,size*.28f,t),pixel);GUI.DrawTexture(new Rect(s.x-size*.5f,s.y+size*.5f-t,size*.28f,t),pixel);GUI.DrawTexture(new Rect(s.x+size*.22f,s.y+size*.5f-t,size*.28f,t),pixel);GUI.DrawTexture(new Rect(s.x-size*.5f,s.y-size*.5f,t,size*.28f),pixel);GUI.DrawTexture(new Rect(s.x-size*.5f,s.y+size*.22f,t,size*.28f),pixel);GUI.DrawTexture(new Rect(s.x+size*.5f-t,s.y-size*.5f,t,size*.28f),pixel);GUI.DrawTexture(new Rect(s.x+size*.5f-t,s.y+size*.22f,t,size*.28f),pixel);GUI.color=old;label??=new GUIStyle(GUI.skin.label){fontSize=11,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};GUI.Label(new Rect(s.x-70,s.y-size*.5f-22,140,20),h.GetComponent<DimensionPlanet>()!=null?"OBJECTIVE":"TARGET",label);}
private void OnDestroy(){if(pixel!=null)Destroy(pixel);}
}
}
