using UnityEngine;
namespace Alterverse.Match
{
public sealed class DimensionsFrontVisual:MonoBehaviour
{
[SerializeField]private float arenaHalfWidth=39f;[SerializeField]private float frontLength=68f;[SerializeField]private float smoothing=6f;private DimensionsFront front;private Transform boundary;private Renderer boundaryRenderer;private float targetX;private readonly Color zeroColor=new(.55f,.08f,.82f),lumenColor=new(.08f,.72f,.78f);
private void Start(){front=Object.FindAnyObjectByType<DimensionsFront>();GameObject strip=GameObject.CreatePrimitive(PrimitiveType.Cube);strip.name="AV_LiveRealityFront";strip.transform.SetParent(transform,false);strip.transform.position=new Vector3(0,.12f,0);strip.transform.localScale=new Vector3(.42f,.08f,frontLength);Collider collider=strip.GetComponent<Collider>();if(collider!=null)collider.enabled=false;boundary=strip.transform;boundaryRenderer=strip.GetComponent<Renderer>();if(front!=null){targetX=ControlToX(front.LumenControl);front.ControlChanged+=OnControlChanged;}ApplyColor();}
private void Update(){if(boundary==null)return;Vector3 p=boundary.position;p.x=Mathf.Lerp(p.x,targetX,1f-Mathf.Exp(-smoothing*Time.deltaTime));boundary.position=p;float pulse=.75f+Mathf.Sin(Time.time*5f)*.25f;boundary.localScale=new Vector3(.35f+pulse*.16f,.08f+pulse*.05f,frontLength);ApplyColor();}
private void OnControlChanged(float zero,float lumen){targetX=ControlToX(lumen);}private float ControlToX(float lumen)=>Mathf.Lerp(arenaHalfWidth,-arenaHalfWidth,lumen/100f);private void ApplyColor(){if(boundaryRenderer==null||front==null)return;boundaryRenderer.material.color=Color.Lerp(zeroColor,lumenColor,front.LumenControl/100f);}private void OnDestroy(){if(front!=null)front.ControlChanged-=OnControlChanged;}
}}
