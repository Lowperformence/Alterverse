using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Match
{
    [RequireComponent(typeof(DimensionPlanet), typeof(Health))]
    public sealed class PlanetPulseAttack : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<PlanetPulseAttack> active=new();
        public static event System.Action<string,float> NetworkPulseStarted;
        public static event System.Action<string> NetworkPulseDetonated;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()=>active.Clear();

        [SerializeField] private float interval = 5.5f, telegraphDuration = 1.15f, radius = 4.16f, damage = 60f;
        private DimensionPlanet planet;
        private Health health;
        private GameObject telegraphRoot;
        private Transform telegraphDisc;
        private Renderer telegraphRenderer;
        private Material telegraphMaterial;
        private float nextPulse, telegraphStart, flashUntil;
        private bool telegraphing;
        public static System.Collections.Generic.IReadOnlyList<PlanetPulseAttack> Active=>active;
        public bool IsTelegraphing => isActiveAndEnabled && telegraphing && planet != null && planet.Available && health != null && !health.IsDead;
        public Vector3 ImpactPosition=>transform.position;
        public float BlastRadius => radius;
        public float TelegraphAge=>telegraphing?Mathf.Max(0f,Time.time-telegraphStart):0f;
        public float TimeRemaining=>telegraphing?Mathf.Max(0f,telegraphDuration-TelegraphAge):0f;

        // Keep the authored warning colour unchanged. Only the target presentation is redesigned.
        private readonly Color warning = new Color(1f, .48f, .12f);

        private void Awake()
        {
            planet = GetComponent<DimensionPlanet>();
            health = GetComponent<Health>();
            telegraphMaterial=RuntimeShaderLibrary.CreateGroundTelegraph(warning,.08f,.5f,.045f);
            BuildTelegraphVisual();
            nextPulse = Time.time + 2.5f;
        }

        private void OnEnable(){if(!active.Contains(this))active.Add(this);}

        private void BuildTelegraphVisual()
        {
            if(telegraphMaterial==null)return;
            if(telegraphRoot!=null)Destroy(telegraphRoot);

            telegraphRoot=new GameObject("DF_PulseGroundTarget");
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;
            if(world!=null)telegraphRoot.transform.SetParent(world,true);

            GameObject disc=GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name="DF_PulseGroundDisc";
            disc.transform.SetParent(telegraphRoot.transform,false);
            disc.transform.localRotation=Quaternion.Euler(90f,0f,0f);
            disc.transform.localScale=new Vector3(radius*2f,radius*2f,1f);
            Collider col=disc.GetComponent<Collider>();if(col!=null)col.enabled=false;

            telegraphRenderer=disc.GetComponent<Renderer>();
            if(telegraphRenderer!=null)
            {
                telegraphRenderer.sharedMaterial=telegraphMaterial;
                telegraphRenderer.shadowCastingMode=ShadowCastingMode.Off;
                telegraphRenderer.receiveShadows=false;
                telegraphRenderer.enabled=false;
            }
            telegraphDisc=disc.transform;
            UpdateTelegraphTransform();
        }

        private void UpdateTelegraphTransform()
        {
            if(telegraphRoot==null)return;
            Vector3 p=transform.position;
            // Match the old warning rings: this is a ground target, not a plane through the planet model.
            telegraphRoot.transform.position=new Vector3(p.x,.105f,p.z);
            telegraphRoot.transform.rotation=Quaternion.identity;
            if(telegraphDisc!=null)telegraphDisc.localScale=new Vector3(radius*2f,radius*2f,1f);
        }

        private void Update()
        {
            if (planet == null || !planet.Available || health == null || health.IsDead)
            {
                HideTelegraph();
                return;
            }
            if (Time.timeScale <= 0f) return;

            UpdateTelegraphTransform();

            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
            {
                if(telegraphing)UpdateTelegraphVisual();
                return;
            }

            if (!telegraphing)
            {
                if (Time.time < flashUntil) return;
                HideTelegraph();
                if (Time.time < nextPulse) return;
                BeginTelegraph(true);
            }

            if(!telegraphing)return;
            float progress=UpdateTelegraphVisual();
            if(progress<1f)return;
            Explode(true);
        }

        private void BeginTelegraph(bool announceNetwork)
        {
            telegraphing=true;
            telegraphStart=Time.time;
            UpdateTelegraphTransform();
            if(telegraphRenderer!=null)telegraphRenderer.enabled=true;
            if(telegraphMaterial!=null)
            {
                telegraphMaterial.SetColor("_BaseColor",warning);
                telegraphMaterial.SetFloat("_FillAlpha",.075f);
                telegraphMaterial.SetFloat("_RingAlpha",.5f);
                telegraphMaterial.SetFloat("_RingWidth",.042f);
            }
            if(announceNetwork&&Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority)
                NetworkPulseStarted?.Invoke(gameObject.name,telegraphDuration);
        }

        private float UpdateTelegraphVisual()
        {
            float progress=Mathf.Clamp01((Time.time-telegraphStart)/Mathf.Max(.1f,telegraphDuration));
            if(telegraphMaterial!=null)
            {
                // Exact radius never moves. Urgency increases inward through opacity/ring thickness only.
                float beat=.5f+.5f*Mathf.Sin(Time.time*(5.5f+progress*7f));
                telegraphMaterial.SetColor("_BaseColor",warning);
                telegraphMaterial.SetFloat("_FillAlpha",Mathf.Lerp(.075f,.19f,progress)+beat*.018f);
                telegraphMaterial.SetFloat("_RingAlpha",.5f);
                telegraphMaterial.SetFloat("_RingWidth",Mathf.Lerp(.040f,.072f,progress));
            }
            return progress;
        }

        private void Explode(bool authoritative)
        {
            if(authoritative)
            {
                foreach (Health victim in FindObjectsByType<Health>())
                {
                    if (victim == null || victim.IsDead || victim == health) continue;
                    if (victim.GetComponent<TeamMember>() == null && victim.GetComponent<LaneMinion>() == null) continue;
                    Vector3 delta = victim.transform.position - transform.position;
                    delta.y = 0;
                    if (delta.sqrMagnitude <= radius * radius) victim.TakeDamage(damage, gameObject);
                }
            }

            Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Explosion,transform.position,.6f);
            if(!AttackExplosionVfx.TryPlay(AttackExplosionVfx.Kind.FluxNuke,transform.position))
                PrototypeVfx.Burst(transform.position, warning, radius * .55f, 10);
            telegraphing=false;
            flashUntil=Time.time+.18f;
            nextPulse=Time.time+Mathf.Max(.5f,interval);
            if(telegraphRenderer!=null)telegraphRenderer.enabled=true;
            if(telegraphMaterial!=null)
            {
                // Keep the same programmed warning colour during the brief detonation flash.
                telegraphMaterial.SetColor("_BaseColor",warning);
                telegraphMaterial.SetFloat("_FillAlpha",.24f);
                telegraphMaterial.SetFloat("_RingAlpha",.5f);
                telegraphMaterial.SetFloat("_RingWidth",.085f);
            }

            if(authoritative&&Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority)
                NetworkPulseDetonated?.Invoke(gameObject.name);
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
                if(pulse!=null&&pulse.gameObject.name==ownerName){pulse.Explode(false);return;}
            }
        }

        private void HideTelegraph()
        {
            telegraphing=false;
            if(telegraphRenderer!=null)telegraphRenderer.enabled=false;
        }

        private void OnDisable(){active.Remove(this);HideTelegraph();}

        private void OnDestroy()
        {
            active.Remove(this);
            if(telegraphRoot!=null)Destroy(telegraphRoot);
            if(telegraphMaterial!=null)Destroy(telegraphMaterial);
        }
    }
}
