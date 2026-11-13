using System.Collections.Generic;
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
        private static readonly List<VoidKnightState> activeStates=new(4);
        private static float lastCloakExpiry=-1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeStates.Clear();
            lastCloakExpiry=-1f;
        }

        private Health health;
        private HeroCombatStatus status;
        private HeroIdentity identity;
        private TeamMember teamMember;
        private int hits;
        private float guardUntil;
        private bool counter;
        private Vector3 guardDirection;
        private float cloakUntil;
        private double lastCloakSample=double.NegativeInfinity;

        public float CloakRemaining
        {
            get
            {
                if(!isActiveAndEnabled||health==null||health.IsDead||cloakUntil<=Time.time)return 0f;
                if(identity==null)identity=GetComponent<HeroIdentity>();
                return identity!=null&&identity.Hero==HeroId.VoidKnight?Mathf.Max(0f,cloakUntil-Time.time):0f;
            }
        }

        public bool Cloaked=>CloakRemaining>0f;

        public static bool HiddenFrom(GameObject observer,Health target)
        {
            if(Time.time>=lastCloakExpiry||observer==null||target==null||observer==target.gameObject)return false;
            VoidKnightState state=null;
            for(int i=0;i<activeStates.Count;i++)
            {
                VoidKnightState candidate=activeStates[i];
                if(candidate!=null&&(candidate.health==target||candidate.gameObject==target.gameObject))
                {
                    state=candidate;
                    break;
                }
            }
            if(state==null)state=target.GetComponent<VoidKnightState>();
            if(state==null||!state.Cloaked)return false;
            TeamMember viewer=observer.GetComponent<TeamMember>();
            TeamMember owner=state.teamMember!=null?state.teamMember:(state.teamMember=target.GetComponent<TeamMember>());
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
            if(cloakUntil>lastCloakExpiry)lastCloakExpiry=cloakUntil;
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
            if(cloakUntil>lastCloakExpiry)lastCloakExpiry=cloakUntil;
            if(Cloaked)EnsureCloakPresentation();
        }

        public int Charges=>hits;
        public bool Guarding=>Time.time<guardUntil&&health!=null&&!health.IsDead&&status?.Stunned!=true;

        private void Awake()
        {
            health=GetComponent<Health>();
            identity=GetComponent<HeroIdentity>();
            teamMember=GetComponent<TeamMember>();
            if(health!=null)health.Died+=ResetLife;
            status=GetComponent<HeroCombatStatus>()??gameObject.AddComponent<HeroCombatStatus>();
            if(!activeStates.Contains(this))activeStates.Add(this);
        }

        private void OnEnable()
        {
            if(!activeStates.Contains(this))activeStates.Add(this);
        }

        private bool Authoritative
        {
            get
            {
                if(!isActiveAndEnabled||health==null||health.IsDead||!CoopAuthority.CanSimulateHostGameplay)return false;
                if(identity==null)identity=GetComponent<HeroIdentity>();
                return identity!=null&&identity.Hero==HeroId.VoidKnight;
            }
        }

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
            if(!counter){counter=true;HeroCombatAudio.Impact(gameObject,1,transform.position);HeroSkillVfx.Support(gameObject,health,true,.15f,shieldUsesTotal:true);}
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
        private void OnDisable(){ResetLife();activeStates.Remove(this);}
        private void OnDestroy(){activeStates.Remove(this);if(health!=null)health.Died-=ResetLife;}
    }
}
