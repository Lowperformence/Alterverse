using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
public static class PrototypeVfx
{
private static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
private static MaterialPropertyBlock colorBlock;private static Mesh ringMesh;private static Material ringMaterial;
public static void RadialBurst(Vector3 position,Color color,float radius){Shockwave(position,color,radius);}
public static void Burst(Vector3 position,Color color,float size=1f,int pieces=7){for(int i=0;i<pieces;i++){float angle=360f/pieces*i;Vector3 direction=Quaternion.Euler(0,angle,0)*Vector3.forward;GameObject shard=Create(PrimitiveType.Cube,"DF_VFX_Burst",position+Vector3.up*.8f+direction*.2f,color);shard.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);shard.AddComponent<TransientScaleVfx>().Configure(new Vector3(.10f,.10f,.35f)*size,new Vector3(.02f,.02f,1.65f)*size,.24f,220f);}}
public static void Ring(Vector3 position,Color color,float radius=1.5f,float duration=.35f)
{
    // A Cylinder is a filled disc. At large radii (Hot Zone / objective bursts) it could cover
    // most of the visible battlefield for a few frames and read as a completely blue floor.
    // Use a true torus ring instead so the center never paints over the arena surface.
    if(ringMesh==null)
    {
        ringMesh=ProductionMeshFactory.Ring(1f,.055f,48,4);
        ringMesh.name="DF_TransientVfxRingMesh";
        ringMesh.hideFlags=HideFlags.DontSave;
    }
    if(ringMaterial==null)
    {
        Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
        if(shader==null)shader=Shader.Find("Unlit/Color");
        if(shader!=null)
        {
            ringMaterial=new Material(shader){name="DF_TransientVfxRingMaterial",hideFlags=HideFlags.DontSave};
            ringMaterial.color=Color.white;
            if(ringMaterial.HasProperty("_BaseColor"))ringMaterial.SetColor("_BaseColor",Color.white);
        }
    }
    GameObject ring=new("DF_VFX_Ring");
    ring.transform.position=position+Vector3.up*.065f;
    ring.AddComponent<MeshFilter>().sharedMesh=ringMesh;
    MeshRenderer renderer=ring.AddComponent<MeshRenderer>();
    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    if(ringMaterial!=null)renderer.sharedMaterial=ringMaterial;
    colorBlock??=new MaterialPropertyBlock();colorBlock.Clear();
    colorBlock.SetColor(BaseColor,color);colorBlock.SetColor(ColorId,color);renderer.SetPropertyBlock(colorBlock);
    ring.AddComponent<TransientScaleVfx>().Configure(Vector3.one*.25f,Vector3.one*Mathf.Max(.01f,radius),duration,90f);
}
public static void Slash(Transform source,Color color,float length=1.7f){GameObject slash=Create(PrimitiveType.Cube,"DF_VFX_Slash",source.position+Vector3.up*.75f+source.forward,color);slash.transform.rotation=source.rotation*Quaternion.Euler(0,25,-25);slash.AddComponent<TransientScaleVfx>().Configure(new Vector3(.12f,.7f,length),new Vector3(.025f,.08f,length*1.2f),.18f,320f);}
public static void ShadowTrail(Vector3 from,Vector3 to,Color color){Beam(from+Vector3.up*.45f,to+Vector3.up*.45f,color,.65f,.28f,"DF_VFX_ShadowTrail");}
public static void SoulLink(Vector3 from,Vector3 to,Color color){Beam(from+Vector3.up*.8f,to+Vector3.up*.8f,color,.08f,.32f,"DF_VFX_SoulLink");}
public static void FutureMark(Transform target,Color color,float duration){GameObject mark=Create(PrimitiveType.Cylinder,"DF_VFX_FutureMark",target.position+Vector3.up*2.25f,color);mark.transform.SetParent(target,true);mark.transform.rotation=Quaternion.Euler(90,0,0);mark.AddComponent<TransientScaleVfx>().Configure(new Vector3(.18f,.02f,.18f),new Vector3(1.15f,.02f,1.15f),duration,-240);Ring(target.position,color,1.4f,.55f);}
public static void ProjectileLine(Vector3 from,Vector3 to,Color color,float width=.11f){Beam(from+Vector3.up*.65f,to+Vector3.up*.65f,color,width,.16f,"DF_VFX_Projectile");Burst(to,color,.55f,4);}
public static void BioBolt(Vector3 from,Vector3 to,Color bio,Color teamAccent){Vector3 delta=to-from;if(delta.sqrMagnitude<.01f)return;Color core=Color.Lerp(bio,Color.white,.38f);Beam(from,to,bio,.15f,.16f,"DF_VFX_XenoBioBolt");Beam(from,to,core,.055f,.13f,"DF_VFX_XenoBioCore");Burst(from,teamAccent,.10f,2);Burst(to,bio,.38f,4);Ring(to,bio,.34f,.18f);}
public static void Shockwave(Vector3 position,Color color,float radius=2.4f){Ring(position,color,radius,.3f);for(int i=0;i<6;i++){Vector3 d=Quaternion.Euler(0,i*60,0)*Vector3.forward;GameObject g=Create(PrimitiveType.Cube,"DF_VFX_ShockShard",position+Vector3.up*.18f+d*.4f,color);g.transform.rotation=Quaternion.LookRotation(d);g.AddComponent<TransientScaleVfx>().Configure(new Vector3(.16f,.08f,.35f),new Vector3(.04f,.03f,radius*.72f),.28f,80);}}
public static void GravityField(Vector3 position,Color color,float radius=2f){Ring(position,color,radius,.48f);Ring(position+Vector3.up*.08f,Color.Lerp(color,Color.white,.4f),radius*.65f,.42f);for(int i=0;i<4;i++){float a=i*90*Mathf.Deg2Rad;GameObject g=Create(PrimitiveType.Sphere,"DF_VFX_GravityMote",position+new Vector3(Mathf.Cos(a)*radius*.55f,.45f,Mathf.Sin(a)*radius*.55f),color);g.AddComponent<TransientScaleVfx>().Configure(Vector3.one*.18f,Vector3.one*.035f,.45f,180);}}
public static void Bloom(Vector3 position,Color color,float radius=2f){Ring(position,color,radius,.45f);for(int i=0;i<6;i++){float a=i*60*Mathf.Deg2Rad;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));GameObject petal=Create(PrimitiveType.Cube,"DF_VFX_BloomPetal",position+Vector3.up*.2f+d*.35f,color);petal.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,35);petal.AddComponent<TransientScaleVfx>().Configure(new Vector3(.18f,.04f,.38f),new Vector3(.5f,.02f,radius*.65f),.42f,70);}}
public static void Guard(Vector3 position,Color color,float radius=1.5f){for(int i=0;i<6;i++){float a=i*60*Mathf.Deg2Rad;Vector3 p=position+Vector3.up*.75f+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);GameObject panel=Create(PrimitiveType.Cube,"DF_VFX_GuardPanel",p,color);panel.transform.LookAt(position+Vector3.up*.75f);panel.transform.localScale=new Vector3(.55f,.7f,.05f);panel.AddComponent<TransientScaleVfx>().Configure(new Vector3(.1f,.2f,.03f),new Vector3(.55f,.7f,.05f),.34f,0);}}
private static void Beam(Vector3 from,Vector3 to,Color color,float width,float duration,string name)
{
    if((to-from).sqrMagnitude<.01f)return;
    HeroVfxPool.Trace(from,to,color,width,duration);
}
private static GameObject Create(PrimitiveType type,string name,Vector3 position,Color color){GameObject target=GameObject.CreatePrimitive(type);target.name=name;target.transform.position=position;Collider collider=target.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}Renderer renderer=target.GetComponent<Renderer>();if(renderer!=null){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;colorBlock??=new MaterialPropertyBlock();colorBlock.Clear();colorBlock.SetColor(BaseColor,color);colorBlock.SetColor(ColorId,color);renderer.SetPropertyBlock(colorBlock);}return target;}
}
}

