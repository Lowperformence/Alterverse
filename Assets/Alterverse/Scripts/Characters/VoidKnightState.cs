using Alterverse.Audio;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;
namespace Alterverse.Characters
{
    [DisallowMultipleComponent]
    public sealed class VoidKnightState:MonoBehaviour
    {
        private Health health;private HeroCombatStatus status;private int hits;private float guardUntil;private bool counter;private Vector3 guardDirection;
        private float cloakUntil;
        private double lastCloakSample=double.NegativeInfinity;
        public float CloakRemaining=>isActiveAndEnabled&&health!=null&&!health.IsDead&&GetComponent<HeroIdentity>()?.Hero==HeroId.VoidKnight?Mathf.Max(0,cloakUntil-Time.time):0f;
        public bool Cloaked=>CloakRemaining>0f;
        public static bool HiddenFrom(GameObject observer,Health target)
        {
            if(observer==null||target==null||observer==target.gameObject||target.GetComponent<VoidKnightState>()?.Cloaked!=true)return false;
            TeamMember viewer=observer.GetComponent<TeamMember>(),owner=target.GetComponent<TeamMember>();
            return viewer!=null&&owner!=null&&viewer.Team!=owner.Team;
        }
        private void EnsureCloakPresentation()
        {
            if(GetComponent<VoidKnightStealth>()==null)gameObject.AddComponent<VoidKnightStealth>();
        }
        public void BeginSweepCloak()
        {
            if(!Authoritative)return;
            cloakUntil=Time.time+VoidKnightBalance.SweepCloakSeconds;
            EnsureCloakPresentation();
            HeroSkillVfx.VoidKnightSmoke(gameObject,transform.position);
        }
        // Server timestamps prevent an old snapshot or delayed reliable effect from restarting the cloak.
        public void ApplyCloakSnapshot(float remaining,double sampledAt,double serverNow)
        {
            if(CoopAuthority.CanSimulateHostGameplay||double.IsNaN(sampledAt)||double.IsInfinity(sampledAt)||
                double.IsNaN(serverNow)||double.IsInfinity(serverNow)||float.IsNaN(remaining)||float.IsInfinity(remaining)||
                remaining<0||remaining>VoidKnightBalance.SweepCloakSeconds||sampledAt<=lastCloakSample)return;
            lastCloakSample=sampledAt;
            cloakUntil=Time.time+Mathf.Max(0,remaining-Mathf.Max(0,(float)(serverNow-sampledAt)));
            if(Cloaked)EnsureCloakPresentation();
        }
        public int Charges=>hits;
        public bool Guarding=>Time.time<guardUntil&&health!=null&&!health.IsDead&&status?.Stunned!=true;
        private void Awake(){health=GetComponent<Health>();if(health!=null)health.Died+=ResetLife;status=GetComponent<HeroCombatStatus>()??gameObject.AddComponent<HeroCombatStatus>();}
        private bool Authoritative=>isActiveAndEnabled&&GetComponent<HeroIdentity>()?.Hero==HeroId.VoidKnight&&health!=null&&!health.IsDead&&CoopAuthority.CanSimulateHostGameplay;
        public bool BeginGuard()
        {
            if(!Authoritative||Guarding||!status.TryRoot(this,VoidKnightBalance.GuardSeconds,true))return false;
            guardUntil=Time.time+VoidKnightBalance.GuardSeconds;guardDirection=transform.forward;counter=false;return true;
        }
        public float ReduceFrontalDamage(float amount,GameObject source)
        {
            if(!Authoritative||!Guarding||source==null)return amount;
            Vector3 direction=AimGeometry.Flat(source.transform.position-transform.position);
            if(direction.sqrMagnitude<.00001f||Vector3.Dot(guardDirection.normalized,direction.normalized)<Mathf.Cos(VoidKnightBalance.GuardHalfAngle*Mathf.Deg2Rad))return amount;
            if(!counter){counter=true;HeroCombatAudio.Impact(gameObject,1,transform.position);HeroSkillVfx.Support(gameObject,health,true,.15f);}
            return amount*VoidKnightBalance.GuardMultiplier;
        }
        public void RecordBasicHit(){if(Authoritative)hits=Mathf.Min(VoidKnightBalance.PassiveHits,hits+1);}
        public void RecordSkillHit()
        {
            if(!Authoritative||hits<VoidKnightBalance.PassiveHits)return;
            hits=0;health.AddShield(VoidKnightBalance.PassiveShield,VoidKnightBalance.PassiveSeconds,false);HeroSkillVfx.Support(gameObject,health,true,VoidKnightBalance.PassiveSeconds);
        }
        public void CancelGuard(){guardUntil=0;counter=false;status?.ReleaseRoot(this);}
        private void Update()
        {
            if(!Authoritative||status.Stunned){if(health==null||health.IsDead)hits=0;CancelGuard();return;}
            if(guardUntil>0&&Time.time>=guardUntil){bool follow=counter;Vector3 direction=guardDirection;CancelGuard();if(follow)GetComponent<HeroAbilityExecution>()?.VoidKnightCounter(direction);}
        }
        private void ResetLife(){hits=0;cloakUntil=0;CancelGuard();}
        private void OnDisable()=>ResetLife();
        private void OnDestroy(){if(health!=null)health.Died-=ResetLife;}
    }
}

