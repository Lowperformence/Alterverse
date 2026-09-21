using Alterverse.Combat;
using UnityEngine;

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

        [SerializeField] private float interval = 5.5f, telegraphDuration = 1.15f, radius = 5.2f, damage = 60f;
        private const int Segments = 64;
        private DimensionPlanet planet;
        private Health health;
        private LineRenderer boundary, charge;
        private Material ringMaterial;
        private float nextPulse, telegraphStart, flashUntil;
        private bool telegraphing;
        public bool IsTelegraphing => isActiveAndEnabled && telegraphing && planet != null && planet.Available && health != null && !health.IsDead;
        public float BlastRadius => radius;
        private readonly Color warning = new Color(1f, .48f, .12f);

        private void Awake()
        {
            planet = GetComponent<DimensionPlanet>();
            health = GetComponent<Health>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                ringMaterial = new Material(shader);
                ringMaterial.color = warning;
                boundary = CreateRing("DF_PulseBoundary", .09f);
                charge = CreateRing("DF_PulseCharge", .14f);
            }
            nextPulse = Time.time + 2.5f;
        }

        private void OnEnable(){if(!active.Contains(this))active.Add(this);}

        private LineRenderer CreateRing(string label, float width)
        {
            GameObject ring = new GameObject(label);
            ring.transform.SetParent(transform, false);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            line.sharedMaterial = ringMaterial;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private void DrawRing(LineRenderer line, float worldRadius)
        {
            if (line == null) return;
            Vector3 center = transform.position;
            center.y = .16f;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * worldRadius);
            }
        }

        private void Update()
        {
            if (planet == null || !planet.Available || health == null || health.IsDead)
            {
                HideRings();
                return;
            }
            if (Time.timeScale <= 0f) return;

            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)
            {
                if(telegraphing)UpdateTelegraphVisual();
                return;
            }

            if (!telegraphing)
            {
                if (Time.time < flashUntil) return;
                HideRings();
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
            if (boundary != null) { boundary.enabled = true; boundary.widthMultiplier = .09f; }
            if (charge != null) charge.enabled = true;
            if (ringMaterial != null) ringMaterial.color = warning;
            if(announceNetwork&&Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
                NetworkPulseStarted?.Invoke(gameObject.name,telegraphDuration);
        }

        private float UpdateTelegraphVisual()
        {
            float progress=Mathf.Clamp01((Time.time-telegraphStart)/Mathf.Max(.1f,telegraphDuration));
            DrawRing(boundary,radius);
            DrawRing(charge,radius*progress);
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

            Alterverse.Visuals.PrototypeVfx.Burst(transform.position, warning, radius * .55f, 10);
            telegraphing=false;
            flashUntil=Time.time+.18f;
            nextPulse=Time.time+Mathf.Max(.5f,interval);
            if(charge!=null)charge.enabled=false;
            if(boundary!=null)boundary.widthMultiplier=.24f;
            if(ringMaterial!=null)ringMaterial.color=Color.white;

            if(authoritative&&Alterverse.Multiplayer.CoopAuthority.Active&&Alterverse.Multiplayer.CoopAuthority.LocalIsHost)
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

        private void HideRings()
        {
            telegraphing = false;
            if (boundary != null) boundary.enabled = false;
            if (charge != null) charge.enabled = false;
        }

        private void OnDisable(){active.Remove(this);HideRings();}
        private void OnDestroy(){active.Remove(this);if(ringMaterial!=null)Destroy(ringMaterial);}
    }
}

