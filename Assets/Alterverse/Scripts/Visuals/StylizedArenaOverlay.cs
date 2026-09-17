using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class StylizedArenaOverlay:MonoBehaviour
{
private void Start(){BuildSide(-1,StylizedArtDirection.ZeroEnergy);BuildSide(1,StylizedArtDirection.LumenEnergy);}
private void BuildSide(int side,Color color){for(int lane=-1;lane<=1;lane++){float z=lane*16;for(int i=0;i<5;i++){float x=side*(8+i*6);CreateLine(new Vector3(x,.075f,z+(i%2==0?.85f:-.85f)),new Vector3(3.4f,.025f,.09f),color,side*(i%2==0?12:-12));}}for(int i=0;i<6;i++){float z=-27+i*10.8f,x=side*(17+(i%2)*5);CreateLine(new Vector3(x,.078f,z),new Vector3(2.6f,.025f,.08f),color,side*32);}}
private void CreateLine(Vector3 p,Vector3 scale,Color color,float yaw){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_ComicArenaMark";g.transform.SetParent(transform,false);g.transform.position=p;g.transform.localScale=scale;g.transform.rotation=Quaternion.Euler(0,yaw,0);Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=Color.Lerp(color,StylizedArtDirection.Ink,.35f);}
}
}
