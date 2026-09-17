using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class HeroVisualMotion:MonoBehaviour
{
private HeroIdentity identity;private Health health;private Transform visualRoot;private Vector3 basePos;private float seed;private void Start(){identity=GetComponent<HeroIdentity>();health=GetComponent<Health>();seed=Mathf.Abs(GetInstanceID()%97)*.13f;visualRoot=transform.Find("DF_VisualRoot");if(visualRoot!=null)basePos=visualRoot.localPosition;}
private void LateUpdate(){if(visualRoot==null||health==null)return;float t=Time.time+seed;if(health.IsDead){visualRoot.localPosition=Vector3.Lerp(visualRoot.localPosition,basePos+Vector3.down*.5f,1-Mathf.Exp(-8*Time.deltaTime));visualRoot.localRotation=Quaternion.Slerp(visualRoot.localRotation,Quaternion.Euler(0,0,72),1-Mathf.Exp(-6*Time.deltaTime));return;}float bob=Mathf.Sin(t*(identity!=null&&identity.Hero==HeroId.Rook?2.2f:3.1f))*.025f;float lean=Mathf.Sin(t*1.7f)*1.5f;visualRoot.localPosition=basePos+Vector3.up*bob;visualRoot.localRotation=Quaternion.Euler(0,0,lean);Transform orbitL=visualRoot.Find("DF_OrbitL"),orbitR=visualRoot.Find("DF_OrbitR");if(orbitL!=null){orbitL.localPosition=new Vector3(Mathf.Cos(t*1.4f)*.72f,.48f+Mathf.Sin(t*1.4f)*.12f,Mathf.Sin(t*1.4f)*.28f);}if(orbitR!=null){orbitR.localPosition=new Vector3(Mathf.Cos(t*1.4f+Mathf.PI)*.68f,.3f+Mathf.Sin(t*1.4f+Mathf.PI)*.12f,Mathf.Sin(t*1.4f+Mathf.PI)*.25f);}}
}
}
