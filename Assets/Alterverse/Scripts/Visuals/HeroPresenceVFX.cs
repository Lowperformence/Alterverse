using Alterverse.Characters;using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class HeroPresenceVFX:MonoBehaviour
{
private HeroIdentity identity;private Transform root;private Transform[] motes;private Vector3[] origins,baseScales;private Renderer visibilityRenderer;private MaterialPropertyBlock colorBlock;private static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
private void Start()
{
identity=GetComponent<HeroIdentity>();root=transform.Find("DF_VisualRoot");if(identity==null||root==null)return;visibilityRenderer=root.GetComponentInChildren<Renderer>();Color accent=StylizedArtDirection.HeroAccent(identity.Hero);int count=(identity.Hero==HeroId.Kaelon||identity.Hero==HeroId.Rook)?3:5;motes=new Transform[count];origins=new Vector3[count];baseScales=new Vector3[count];for(int i=0;i<count;i++){GameObject g=GameObject.CreatePrimitive(i%2==0?PrimitiveType.Cube:PrimitiveType.Sphere);g.name="DF_PresenceMote";g.transform.SetParent(root,false);float a=i*Mathf.PI*2/count;float radius=.5f+.075f*(i%2);Vector3 p=new Vector3(Mathf.Cos(a)*radius,.14f+i*.105f,Mathf.Sin(a)*radius);g.transform.localPosition=p;float moteSize=identity.Hero==HeroId.Rook?.075f:identity.Hero==HeroId.Kaelon?.055f:.05f;Vector3 scale=Vector3.one*moteSize;g.transform.localScale=scale;Collider c=g.GetComponent<Collider>();if(c!=null){c.enabled=false;Object.Destroy(c);}Renderer r=g.GetComponent<Renderer>();if(r!=null){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;colorBlock??=new MaterialPropertyBlock();colorBlock.Clear();Color mote=Color.Lerp(accent,Color.white,.22f);colorBlock.SetColor(BaseColor,mote);colorBlock.SetColor(ColorId,mote);r.SetPropertyBlock(colorBlock);}motes[i]=g.transform;origins[i]=p;baseScales[i]=scale;}
}
private void LateUpdate()
{
if(motes==null||identity==null||Time.timeScale<=0f||(visibilityRenderer!=null&&!visibilityRenderer.isVisible))return;float t=Time.time,dt=Time.deltaTime;for(int i=0;i<motes.Length;i++){Transform m=motes[i];if(m==null)continue;Vector3 p=origins[i];switch(identity.Hero){case HeroId.Veyr:p.x+=Mathf.Sin(t*3.2f+i)*.14f;p.y+=Mathf.Repeat(t*.26f+i*.18f,.58f);break;case HeroId.Nyra:p.z+=Mathf.Sin(t*2.55f+i)*.11f;p.y+=Mathf.Sin(t*1.8f+i)*.06f;break;case HeroId.Elyra:float a=t*1.18f+i*Mathf.PI*2/motes.Length;p=new Vector3(Mathf.Cos(a)*.64f,.31f+Mathf.Sin(a*1.7f)*.23f,Mathf.Sin(a)*.64f);break;case HeroId.Mira:p.y+=Mathf.Sin(t*1.55f+i)*.15f;p.x*=1+Mathf.Sin(t+i)*.13f;break;case HeroId.Traveler:float ta=t*.78f+i*Mathf.PI*2/motes.Length;p=new Vector3(Mathf.Cos(ta)*.58f,.19f+Mathf.Sin(ta*2f)*.17f,Mathf.Sin(ta)*.58f);break;case HeroId.Kaelon:p.y=.16f+i*.19f;p.x=Mathf.Sign(p.x)*.52f;break;default:p.y+=Mathf.Abs(Mathf.Sin(t*2.15f+i))*.09f;p.x+=Mathf.Sin(t*1.5f+i)*.055f;break;}m.localPosition=p;float pulse=.88f+Mathf.Sin(t*2.4f+i*1.7f)*.12f;m.localScale=baseScales[i]*pulse;m.Rotate((26+i*3)*dt,(44+i*4)*dt,(18+i*2)*dt,Space.Self);}
}
}
}
