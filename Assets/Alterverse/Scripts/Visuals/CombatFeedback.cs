using System.Collections;using System.Collections.Generic;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Visuals
{
[RequireComponent(typeof(Health))]public sealed class CombatFeedback:MonoBehaviour
{
private Health health;private float lastHealth;private Coroutine flashRoutine;private readonly List<Renderer> flashRenderers=new List<Renderer>();private readonly List<Color> baseColors=new List<Color>();
private void Awake(){health=GetComponent<Health>();lastHealth=health.Current;health.Changed+=OnHealthChanged;}private void Start(){lastHealth=health.Current;}private void OnDestroy(){if(health!=null)health.Changed-=OnHealthChanged;}
private void OnHealthChanged(float current,float max){if(current<lastHealth-.01f){if(flashRoutine!=null)StopCoroutine(flashRoutine);flashRoutine=StartCoroutine(FlashRoutine());PrototypeVfx.Burst(transform.position,new Color(.95f,.25f,.35f),.7f,5);}lastHealth=current;}
private void CaptureVisuals(){flashRenderers.Clear();baseColors.Clear();Renderer[] renderers=GetComponentsInChildren<Renderer>(true);for(int i=0;i<renderers.Length;i++){Renderer r=renderers[i];if(r==null||!r.enabled)continue;flashRenderers.Add(r);baseColors.Add(r.material.color);}}
private IEnumerator FlashRoutine(){CaptureVisuals();for(int i=0;i<flashRenderers.Count;i++)if(flashRenderers[i]!=null)flashRenderers[i].material.color=Color.white;yield return new WaitForSeconds(.07f);for(int i=0;i<flashRenderers.Count&&i<baseColors.Count;i++)if(flashRenderers[i]!=null)flashRenderers[i].material.color=baseColors[i];flashRoutine=null;}
}
}
