using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class ArenaAtmosphere:MonoBehaviour
{
private Transform[] shards;private Vector3[] origins;private void Start(){const int count=16;shards=new Transform[count];origins=new Vector3[count];for(int i=0;i<count;i++){int side=i%2==0?-1:1;float z=-31+(i/2)*8.8f;Vector3 p=new Vector3(side*(30+(i%3)*3.2f),1.3f+(i%4)*.45f,z);GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=side<0?"DF_ZeroAmbientShard":"DF_LumenAmbientShard";g.transform.SetParent(transform,false);g.transform.position=p;g.transform.localScale=new Vector3(.12f,.55f+.1f*(i%3),.12f);g.transform.rotation=Quaternion.Euler(18,i*37,24);Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=Color.Lerp(side<0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy,StylizedArtDirection.Ink,.38f);shards[i]=g.transform;origins[i]=p;}}
private void Update(){if(shards==null)return;float t=Time.time;for(int i=0;i<shards.Length;i++){Transform s=shards[i];if(s==null)continue;Vector3 p=origins[i];p.y+=Mathf.Sin(t*(.7f+(i%3)*.12f)+i)*.35f;s.position=p;s.Rotate(8*Time.deltaTime,18*Time.deltaTime,5*Time.deltaTime,Space.Self);}}
}
}
