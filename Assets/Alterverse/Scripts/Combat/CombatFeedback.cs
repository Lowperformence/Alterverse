using System.Collections;using Alterverse.Audio;using UnityEngine;
namespace Alterverse.Combat
{
[RequireComponent(typeof(Health))]public sealed class CombatFeedback:MonoBehaviour
{
private Health health;private Coroutine flashRoutine;private Renderer[] flashRenderers;private MaterialPropertyBlock[] savedBlocks;private float previousHealth;private void Awake(){health=GetComponent<Health>();previousHealth=health.Current;health.Changed+=OnHealthChanged;}private void Start()=>previousHealth=health.Current;private void OnDestroy(){RestoreFlash();if(health!=null)health.Changed-=OnHealthChanged;}private void OnDisable(){if(flashRoutine!=null)StopCoroutine(flashRoutine);flashRoutine=null;RestoreFlash();}private void OnHealthChanged(float current,float max){float delta=previousHealth-current;previousHealth=current;if(delta<=.01f)return;if(flashRoutine!=null)StopCoroutine(flashRoutine);RestoreFlash();flashRoutine=StartCoroutine(Flash());SpawnImpact(transform.position+Vector3.up*1.1f,delta);bool heavy=delta>=Mathf.Max(45f,max*.16f);AudioMixState.MarkCombat();if(IsVisibleCombat()&&AudioCuePolicy.Allow(heavy?"heavy-hit":"hit",heavy?AudioCuePriority.Critical:AudioCuePriority.Combat))DimensionAudio.Play(heavy?DimensionSound.HitHeavy:DimensionSound.HitLight,heavy?.72f:.48f,Random.Range(.96f,1.045f));}private bool IsVisibleCombat(){Camera cam=Camera.main;if(cam==null)return false;Vector3 viewport=cam.WorldToViewportPoint(transform.position+Vector3.up);return viewport.z>0f&&viewport.x>=-.05f&&viewport.x<=1.05f&&viewport.y>=-.05f&&viewport.y<=1.05f;}private IEnumerator Flash()
{
flashRenderers=GetComponentsInChildren<Renderer>(true);
savedBlocks=new MaterialPropertyBlock[flashRenderers.Length];
for(int i=0;i<flashRenderers.Length;i++)
{
Renderer r=flashRenderers[i];if(r==null||!r.enabled)continue;
savedBlocks[i]=new MaterialPropertyBlock();r.GetPropertyBlock(savedBlocks[i]);
MaterialPropertyBlock bright=new MaterialPropertyBlock();r.GetPropertyBlock(bright);
bright.SetColor("_BaseColor",Color.white);bright.SetColor("_Color",Color.white);r.SetPropertyBlock(bright);
}
yield return new WaitForSeconds(.07f);
RestoreFlash();flashRoutine=null;
}
private void RestoreFlash()
{
if(flashRenderers!=null&&savedBlocks!=null)
for(int i=0;i<flashRenderers.Length;i++)if(flashRenderers[i]!=null&&savedBlocks[i]!=null)flashRenderers[i].SetPropertyBlock(savedBlocks[i]);
flashRenderers=null;savedBlocks=null;
}
private static void SpawnImpact(Vector3 position,float damage){GameObject impact=GameObject.CreatePrimitive(PrimitiveType.Sphere);impact.name="DF_HitImpact";impact.transform.position=position;impact.transform.localScale=Vector3.one*Mathf.Clamp(.25f+damage*.008f,.3f,.65f);Collider collider=impact.GetComponent<Collider>();if(collider!=null)collider.enabled=false;Renderer renderer=impact.GetComponent<Renderer>();if(renderer!=null)renderer.material.color=new Color(.82f,.25f,1f);impact.AddComponent<TransientVfx>().Initialize(.22f,2.4f);}}
public sealed class TransientVfx:MonoBehaviour{private float lifetime,growSpeed,age;public void Initialize(float duration,float growth){lifetime=duration;growSpeed=growth;}private void Update(){age+=Time.deltaTime;transform.localScale+=Vector3.one*growSpeed*Time.deltaTime;if(age>=lifetime)Destroy(gameObject);}}
}
