using System.Collections;
using Alterverse.Audio;
using Alterverse.Combat;
using Alterverse.Onboarding;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEngine;
namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health))]
    public sealed class BaseRecall : MonoBehaviour
    {
        [SerializeField,Min(1f)] private float channelDuration=5f;
        [SerializeField] private float movementCancelDistance=.18f;
        private Health health;
        private Vector3 spawnPoint,channelStart;
        private Coroutine recallRoutine;
        private RecallCosmeticVfx cosmeticVfx;
        public bool IsRecalling=>recallRoutine!=null;
        public float Progress{get;private set;}
        private void Awake()
        {
            health=GetComponent<Health>();spawnPoint=transform.position;
            health.Died+=CancelRecall;health.Damaged+=OnDamaged;
            if(RecallCosmeticEntitlement.Owned)cosmeticVfx=gameObject.AddComponent<RecallCosmeticVfx>();
        }
        private void OnDisable()=>StopRecall(false);
        private void OnDestroy()
        {
            if(health!=null){health.Died-=CancelRecall;health.Damaged-=OnDamaged;}
        }
        private void OnDamaged(float damage,float absorbed,GameObject source)
        {
            // A shielded hit or same-frame heal must not hide an interrupt.
            if(damage>0||absorbed>0)CancelRecall();
        }
        public void BeginRecall()
        {
            if(!isActiveAndEnabled||Time.timeScale<=0||health==null||health.IsDead||IsRecalling)return;
            DimensionAudio.Play(DimensionSound.RecallStart,.55f);
            recallRoutine=StartCoroutine(RecallRoutine());
        }
        public void CancelRecall()=>StopRecall(true);
        private void StopRecall(bool playSound)
        {
            bool wasRecalling=recallRoutine!=null;
            if(recallRoutine!=null)StopCoroutine(recallRoutine);
            recallRoutine=null;Progress=0f;cosmeticVfx?.SetRecallState(false,0f);
            if(wasRecalling&&playSound)DimensionAudio.Play(DimensionSound.RecallCancel,.45f);
        }
        private IEnumerator RecallRoutine()
        {
            channelStart=transform.position;Progress=0f;cosmeticVfx?.SetRecallState(true,0f);
            while(Progress<1f)
            {
                if(health.IsDead||HorizontalDistance(channelStart,transform.position)>movementCancelDistance)
                {
                    recallRoutine=null;Progress=0f;cosmeticVfx?.SetRecallState(false,0f);
                    DimensionAudio.Play(DimensionSound.RecallCancel,.45f);yield break;
                }
                if(Time.timeScale>0f)Progress+=Time.deltaTime/channelDuration;
                cosmeticVfx?.SetRecallState(true,Progress);yield return null;
            }
            // A pause may start in the frame between completion and this resume.
            while(Time.timeScale<=0f)yield return null;
            cosmeticVfx?.SetRecallState(false,1f);
            DimensionAudio.Play(RecallCosmeticEntitlement.Owned?DimensionSound.RealityShatter:DimensionSound.RecallComplete,RecallCosmeticEntitlement.Owned?.78f:.58f);
            OnboardingSessionGuard.Recall(this);
            CharacterController controller=GetComponent<CharacterController>();
            if(controller!=null)controller.enabled=false;
            transform.position=spawnPoint;
            GetComponent<HeroLocomotion>()?.ResetMotion();GetComponent<HeroMotor>()?.ClearInput();
            if(controller!=null)controller.enabled=true;
            recallRoutine=null;Progress=0f;
        }
        private static float HorizontalDistance(Vector3 a,Vector3 b)
        {a.y=0;b.y=0;return Vector3.Distance(a,b);}
    }
}
