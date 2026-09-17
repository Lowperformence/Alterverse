using UnityEngine;
namespace Alterverse.Visuals
{
public sealed class ArenaLightPulse:MonoBehaviour
{
private Vector3 baseScale;private Vector3 basePosition;private float phase;private void Awake(){baseScale=transform.localScale;basePosition=transform.localPosition;phase=(transform.position.x*1.31f+transform.position.z*.73f)*.37f;}
private void Update(){float t=Time.time*.9f+phase;float pulse=1f+Mathf.Sin(t)*.08f;transform.localScale=new Vector3(baseScale.x*pulse,baseScale.y*(1f+Mathf.Sin(t*.7f)*.035f),baseScale.z*pulse);transform.localPosition=basePosition+Vector3.up*(Mathf.Sin(t*.55f)*.055f);}
}
}
