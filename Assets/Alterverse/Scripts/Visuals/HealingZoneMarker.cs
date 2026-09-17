using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class HealingZoneMarker:MonoBehaviour
{
private Transform marker;private void Start(){marker=new GameObject("DF_HealingPlus").transform;marker.SetParent(transform,false);marker.localPosition=new Vector3(0,.18f,0);Color green=new Color(.12f,1f,.32f);Part(new Vector3(0,.02f,0),new Vector3(.55f,.05f,1.8f),green);Part(new Vector3(0,.025f,0),new Vector3(1.8f,.055f,.55f),green);}private void Update(){if(marker!=null){float p=1f+Mathf.Sin(Time.time*2f)*.06f;marker.localScale=new Vector3(p,1,p);}}
private void Part(Vector3 p,Vector3 s,Color color){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_HealingPlusPart";g.transform.SetParent(marker,false);g.transform.localPosition=p;g.transform.localScale=s;Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=color;}
}
}
