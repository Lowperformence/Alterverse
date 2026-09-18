using Alterverse.Characters;using UnityEngine;
namespace Alterverse.Combat
{
public sealed class CentralHealingPickup:MonoBehaviour
{
[SerializeField]private float radius=1.5f,healPercent=.286f,respawnSeconds=22f;private Transform visual;private float readyAt;private bool available=true;private void Start(){BuildVisual();}private void Update(){if(Time.timeScale<=0f)return;if(!available){if(Time.time>=readyAt){available=true;if(visual!=null)visual.gameObject.SetActive(true);}return;}foreach(TeamMember member in FindObjectsByType<TeamMember>()){if(member==null)continue;Health h=member.GetComponent<Health>();if(h==null||h.IsDead||h.Normalized>=.995f)continue;Vector3 d=member.transform.position-transform.position;d.y=0;if(d.sqrMagnitude<=radius*radius){h.Heal(h.Max*healPercent);available=false;readyAt=Time.time+respawnSeconds;if(visual!=null)visual.gameObject.SetActive(false);break;}}if(visual!=null){visual.Rotate(0,55f*Time.deltaTime,0);visual.localPosition=new Vector3(0,.85f+Mathf.Sin(Time.time*2.5f)*.08f,0);}}
private void BuildVisual(){visual=new GameObject("DF_CentralHealVisual").transform;visual.SetParent(transform,false);Color green=new Color(.12f,1f,.34f);Part("Vertical",new Vector3(0,0,0),new Vector3(.18f,.75f,.18f),green);Part("Horizontal",new Vector3(0,0,0),new Vector3(.75f,.18f,.18f),green);}
private void Part(string n,Vector3 p,Vector3 s,Color color){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_Heal"+n;g.transform.SetParent(visual,false);g.transform.localPosition=p;g.transform.localScale=s;Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=color;}
}
}
