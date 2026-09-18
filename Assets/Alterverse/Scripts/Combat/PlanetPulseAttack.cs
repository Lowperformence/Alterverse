using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    [RequireComponent(typeof(DimensionPlanet), typeof(Health))]
    public sealed class PlanetPulseAttack : MonoBehaviour
    {
        [SerializeField] private float blastRadius = 4.6f;
        [SerializeField] private float damage = 7f;
        [SerializeField] private float cooldown = 7.5f;
        [SerializeField] private float telegraphDuration = 1.15f;
        private DimensionPlanet planet;
        private Health health;
        private float nextPulse,nextTargetScan;private bool targetNearby;private const int HitBufferSize=48;private readonly Collider[] hitBuffer=new Collider[HitBufferSize];
        private float blastAt = -1f;
        private bool telegraphing;
        private GameObject warningDisc;
        public float BlastRadius => blastRadius;
        public bool IsTelegraphing => telegraphing;

        private void Awake()
        {
            planet = GetComponent<DimensionPlanet>();
            health = GetComponent<Health>();
            nextPulse = Time.time + Random.Range(2.5f, 4f);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || health == null || health.IsDead || planet == null || !planet.Available)
            { CancelTelegraph(); return; }

            if (telegraphing)
            {
                if (Time.time >= blastAt) Detonate();
                else UpdateWarning();
                return;
            }

            if (Time.time < nextPulse) return;
            if(Time.time>=nextTargetScan){nextTargetScan=Time.time+.25f;targetNearby=HasTargetInRange();}
            if(!targetNearby)return;
            BeginTelegraph();
        }

        private bool HasTargetInRange()
        {
            float sqr = blastRadius * blastRadius;
            foreach (TeamMember member in FindObjectsByType<TeamMember>())
            {
                Health hp = member != null ? member.GetComponent<Health>() : null;
                if (hp == null || hp.IsDead) continue;
                Vector3 d = member.transform.position - transform.position; d.y = 0;
                if (d.sqrMagnitude <= sqr) return true;
            }
            foreach (LaneMinion minion in FindObjectsByType<LaneMinion>())
            {
                Health hp = minion != null ? minion.GetComponent<Health>() : null;
                if (hp == null || hp.IsDead) continue;
                Vector3 d = minion.transform.position - transform.position; d.y = 0;
                if (d.sqrMagnitude <= sqr) return true;
            }
            return false;
        }

        private void BeginTelegraph()
        {
            telegraphing = true;
            blastAt = Time.time + telegraphDuration;
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

        private void Detonate()
        {
            telegraphing = false;
            if (warningDisc != null) Destroy(warningDisc);
            PrototypeVfx.RadialBurst(transform.position + Vector3.up * .15f, new Color(1f, .48f, .12f), blastRadius);
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
            nextPulse = Time.time + cooldown;
        }

        private void CancelTelegraph()
        {
            telegraphing = false;
            if (warningDisc != null) Destroy(warningDisc);
        }

        private void OnDisable() => CancelTelegraph();
    }
}
