using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(DimensionPlanet), typeof(Health))]
    public sealed class PlanetPulseAttack : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<PlanetPulseAttack> active=new();
        public static event System.Action<string,float> NetworkPulseStarted;
        public static event System.Action<string> NetworkPulseDetonated;
        public static System.Collections.Generic.IReadOnlyList<PlanetPulseAttack> Active=>active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetRegistry()=>active.Clear();

        [SerializeField] private float blastRadius = 4.6f;
        [SerializeField] private float damage = 7f;
        [SerializeField] private float cooldown = 7.5f;
        [SerializeField] private float telegraphDuration = 1.15f;
        private DimensionPlanet planet;
        private Health health;
        private float nextPulse,nextTargetScan;private bool targetNearby;private const int HitBufferSize=48;private readonly Collider[] hitBuffer=new Collider[HitBufferSize];
        private float blastAt = -1f,telegraphStartedAt=-1f;
        private bool telegraphing;
        private GameObject warningDisc;
        public float BlastRadius => blastRadius;
        public bool IsTelegraphing => telegraphing;
        public Vector3 ImpactPosition=>transform.position;
        public float TelegraphAge=>telegraphing?Mathf.Max(0f,Time.time-telegraphStartedAt):0f;
        public float TimeRemaining=>telegraphing?Mathf.Max(0f,blastAt-Time.time):0f;

        private void Awake()
        {
            planet = GetComponent<DimensionPlanet>();
            health = GetComponent<Health>();
            nextPulse = Time.time + Random.Range(2.5f, 4f);
        }

        private void OnEnable(){if(!active.Contains(this))active.Add(this);}

        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
            {
                if(telegraphing&&Time.timeScale>0f)UpdateWarning();
                return;
            }

            if (Time.timeScale <= 0f || health == null || health.IsDead || planet == null || !planet.Available)
            { CancelTelegraph(); return; }

            if (telegraphing)
            {
                if (Time.time >= blastAt) Detonate(true);
                else UpdateWarning();
                return;
            }

            if (Time.time < nextPulse) return;
            if(Time.time>=nextTargetScan){nextTargetScan=Time.time+.25f;targetNearby=HasTargetInRange();}
            if(!targetNearby)return;
            BeginTelegraph(true);
        }

        private bool HasTargetInRange()
        {
            int hits=Physics.OverlapSphereNonAlloc(transform.position,blastRadius,hitBuffer);
            for(int i=0;i<hits;i++)
            {
                Collider c=hitBuffer[i];if(c==null)continue;
                Health hp=c.GetComponentInParent<Health>();if(hp==null||hp.IsDead||hp==health)continue;
                if(hp.GetComponent<TeamMember>()!=null||hp.GetComponent<LaneMinion>()!=null)return true;
            }
            return false;
        }

        private void BeginTelegraph(bool announceNetwork)
        {
            telegraphing = true;
            telegraphStartedAt=Time.time;
            blastAt = Time.time + telegraphDuration;
            if(announceNetwork&&Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
                NetworkPulseStarted?.Invoke(gameObject.name,telegraphDuration);
            warningDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            warningDisc.name = "DF_PlanetPulseWarning";
            GameObject world=GameObject.Find("DF_MatchWorld");if(world!=null)warningDisc.transform.SetParent(world.transform,true);
            warningDisc.transform.position = transform.position + Vector3.down * 1.15f;
            warningDisc.transform.localScale = new Vector3(blastRadius * 2f, .025f, blastRadius * 2f);
            Collider c = warningDisc.GetComponent<Collider>(); if (c != null) c.enabled = false;
            Renderer r = warningDisc.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(1f, .32f, .08f, .32f);
        }

        private void UpdateWarning()
        {
            if (warningDisc == null) return;
            float remaining = Mathf.Clamp01((blastAt - Time.time) / telegraphDuration);
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * .035f * (1f - remaining);
            warningDisc.transform.localScale = new Vector3(blastRadius * 2f * pulse, .025f, blastRadius * 2f * pulse);
        }

        private void Detonate(bool authoritative)
        {
            telegraphing = false;
            telegraphStartedAt=-1f;
            if (warningDisc != null) Destroy(warningDisc);
            PrototypeVfx.RadialBurst(transform.position + Vector3.up * .15f, new Color(1f, .48f, .12f), blastRadius);
            if(authoritative)
            {
                int hits=Physics.OverlapSphereNonAlloc(transform.position,blastRadius,hitBuffer);
                for(int i=0;i<hits;i++)
                {
                    Collider c=hitBuffer[i];if(c==null)continue;
                    Health hp=c.GetComponentInParent<Health>();
                    if(hp==null||hp==health||hp.IsDead)continue;
                    bool duplicate=false;for(int j=0;j<i;j++){Collider prior=hitBuffer[j];if(prior!=null&&prior.GetComponentInParent<Health>()==hp){duplicate=true;break;}}
                    if(duplicate||hp.GetComponent<RealityCore>()!=null||hp.GetComponent<LaneTower>()!=null||hp.GetComponent<DimensionPlanet>()!=null)continue;
                    if(hp.GetComponent<TeamMember>()==null&&hp.GetComponent<LaneMinion>()==null)continue;
                    hp.TakeDamage(damage,gameObject);
                }
            }
            if(authoritative&&Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
                NetworkPulseDetonated?.Invoke(gameObject.name);
            nextPulse = Time.time + cooldown;
        }

        public static void ApplyNetworkPulseStart(string ownerName,float duration)
        {
            if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            for(int i=0;i<active.Count;i++)
            {
                PlanetPulseAttack pulse=active[i];
                if(pulse==null||pulse.gameObject.name!=ownerName)continue;
                pulse.telegraphDuration=Mathf.Max(.1f,duration);
                pulse.BeginTelegraph(false);
                return;
            }
        }

        public static void ApplyNetworkPulseDetonate(string ownerName)
        {
            if(Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            for(int i=0;i<active.Count;i++)
            {
                PlanetPulseAttack pulse=active[i];
                if(pulse!=null&&pulse.gameObject.name==ownerName){pulse.Detonate(false);return;}
            }
        }

        private void CancelTelegraph()
        {
            telegraphing = false;
            telegraphStartedAt=-1f;
            if (warningDisc != null) Destroy(warningDisc);
        }

        private void OnDisable(){active.Remove(this);CancelTelegraph();}
    }
}
