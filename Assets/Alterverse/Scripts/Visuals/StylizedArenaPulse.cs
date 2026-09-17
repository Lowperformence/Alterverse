using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class StylizedArenaPulse:MonoBehaviour
{
private Renderer target;private MaterialPropertyBlock block;private Color baseColor;private static readonly int BaseColorId=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");private void Awake(){target=GetComponent<Renderer>();block=new MaterialPropertyBlock();if(target!=null&&target.sharedMaterial!=null)baseColor=target.sharedMaterial.color;}private void Update(){if(target==null)return;float pulse=.86f+.14f*Mathf.Sin(Time.time*1.7f+transform.position.z*.11f);Color c=baseColor*pulse;c.a=baseColor.a;block.Clear();block.SetColor(BaseColorId,c);block.SetColor(ColorId,c);target.SetPropertyBlock(block);}
}
}
