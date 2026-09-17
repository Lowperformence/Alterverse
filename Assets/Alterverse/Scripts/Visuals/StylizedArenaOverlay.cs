using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class StylizedArenaOverlay:MonoBehaviour
{
private void Start(){BuildSide(-1,StylizedArtDirection.ZeroEnergy);BuildSide(1,StylizedArtDirection.LumenEnergy);BuildFrontApproaches();BuildObjectiveRing(new Vector3(0,.082f,27),StylizedArtDirection.LumenEnergy);BuildObjectiveRing(new Vector3(0,.082f,-27),StylizedArtDirection.WarmAccent);}
private void BuildSide(int side,Color color){for(int lane=-1;lane<=1;lane++){float z=lane*16;for(int i=0;i<5;i++){float x=side*(8+i*6);CreateLine(new Vector3(x,.075f,z+(i%2==0?.85f:-.85f)),new Vector3(3.4f,.025f,.09f),color,side*(i%2==0?12:-12));}for(int edge=-1;edge<=1;edge++)for(int i=0;i<4;i++){float x=side*(8+i*8);CreateLine(new Vector3(x,.071f,z+edge*2.1f),new Vector3(5.2f,.018f,.045f),color,0);}}for(int i=0;i<6;i++){float z=-27+i*10.8f,x=side*(17+(i%2)*5);CreateLine(new Vector3(x,.078f,z),new Vector3(2.6f,.025f,.08f),color,side*32);}}
private void BuildFrontApproaches(){for(int lane=-1;lane<=1;lane++){float z=lane*16;for(int side=-1;side<=1;side++){Color c=side<0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;for(int i=0;i<3;i++){float x=side*(2.2f+i*1.65f);CreateLine(new Vector3(x,.09f,z),new Vector3(1.1f,.028f,.13f),c,side*(i-1)*13);}}}}
private void BuildObjectiveRing(Vector3 center,Color color){const int segments=12;float radius=4.15f;for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;Vector3 p=center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);CreateLine(p,new Vector3(1.25f,.03f,.12f),color,-a*Mathf.Rad2Deg+90);}for(int side=-1;side<=1;side++){for(int i=0;i<3;i++){float x=side*(5.2f+i*2f);CreateLine(center+new Vector3(x,0,0),new Vector3(1.25f,.025f,.08f),color,0);}}}
private void CreateLine(Vector3 p,Vector3 scale,Color color,float yaw){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_ComicArenaMark";g.transform.SetParent(transform,false);g.transform.position=p;g.transform.localScale=scale;g.transform.rotation=Quaternion.Euler(0,yaw,0);Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=Color.Lerp(color,StylizedArtDirection.Ink,.35f);}
}
}
