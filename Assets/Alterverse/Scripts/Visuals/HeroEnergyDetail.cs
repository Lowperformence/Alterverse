using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class HeroEnergyDetail:MonoBehaviour
{
private Vector3 baseScale;private Quaternion baseRotation;private float phase;private Renderer cachedRenderer;private void Awake(){baseScale=transform.localScale;baseRotation=transform.localRotation;phase=transform.localPosition.x*5.7f+transform.localPosition.y*3.1f+transform.localPosition.z*4.3f;cachedRenderer=GetComponent<Renderer>();}
private void Update(){if(Time.timeScale<=0f||(cachedRenderer!=null&&!cachedRenderer.isVisible))return;float t=Time.time*2.2f+phase,p=1f+Mathf.Sin(t)*.09f;transform.localScale=new Vector3(baseScale.x*p,baseScale.y*(1f+Mathf.Sin(t*.73f)*.05f),baseScale.z*p);transform.localRotation=baseRotation*Quaternion.Euler(0,Mathf.Sin(t*.55f)*5f,0);}
}
}
