using System.Collections;
using Alterverse.Characters;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// DARK 00-only combat upgrade for the Hot Zone orb.
    /// Adds a one-second red eye laser (15 damage, 5 second cooldown after firing)
    /// and +100% movement speed while preserving the normal HotZoneTank AI/pathing.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    public sealed class Dark00OrbCombatUpgrade : MonoBehaviour
    {
        private const float ScanInterval=.20f;
        private const string ManagerName="DF_Dark00OrbCombatUpgrade";
        private static bool installed;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics(){installed=false;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(installed)return;
            installed=true;
            GameObject manager=new(ManagerName);
            DontDestroyOnLoad(manager);
            manager.AddComponent<Dark00OrbCombatUpgrade>();
        }

        private void Update()
        {
            if(!ArenaVariantRuntime.IsDark00||Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+ScanInterval;

            var active=HotZoneTank.Active;
            for(int i=0;i<active.Count;i++)
            {
                HotZoneTank tank=active[i];
                if(tank==null||tank.GetComponent<Dark00OrbCombatDriver>()!=null)continue;
                tank.gameObject.AddComponent<Dark00OrbCombatDriver>();
            }
        }
    }

    [DefaultExecutionOrder(1260)]
    internal sealed class Dark00OrbCombatDriver : MonoBehaviour
    {
        private const float ExtraMovementMultiplier=1f; // +100% => 200% total speed.
        private const float ArenaHalfWidth=39f;
        private const float ArenaHalfHeight=32f;
        private const float LaserRange=14f;
        private const float LaserDuration=1f;
        private const float LaserCooldown=5f;
        private const float LaserDamage=15f;

        private HotZoneTank tank;
        private HeroLocomotion locomotion;
        private Health health;
        private Vector3 previousPosition;
        private bool movementInitialized;
        private float nextLaserAt;
        private bool laserActive;
        private LineRenderer laserLine;
        private Material laserMaterial;
        private Transform eye;

        private void OnEnable()
        {
            tank=GetComponent<HotZoneTank>();
            locomotion=GetComponent<HeroLocomotion>();
            health=GetComponent<Health>();
            previousPosition=transform.position;
            movementInitialized=true;
            nextLaserAt=Time.time+1.25f;
        }

        private void Update()
        {
            if(!ArenaVariantRuntime.IsDark00)
            {
                Destroy(this);
                return;
            }
            if(health==null||health.IsDead||tank==null)return;
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            if(laserActive||Time.time<nextLaserAt)return;

            Health target=AcquireLaserTarget();
            if(target!=null)StartCoroutine(FireLaser(target));
        }

        private Health AcquireLaserTarget()
        {
            Health best=null;
            float bestSqr=LaserRange*LaserRange;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health candidate=Health.Active[i];
                if(candidate==null||candidate==health||candidate.IsDead||!candidate.isActiveAndEnabled)continue;
                TeamMember member=candidate.GetComponentInParent<TeamMember>();
                if(member==null||member.Team==tank.Team)continue;

                Vector3 delta=candidate.transform.position-transform.position;
                delta.y=0f;
                float sqr=delta.sqrMagnitude;
                if(sqr>=bestSqr)continue;
                bestSqr=sqr;
                best=candidate;
            }
            return best;
        }

        private IEnumerator FireLaser(Health target)
        {
            laserActive=true;
            EnsureLaserVisual();
            if(laserLine!=null)laserLine.enabled=true;

            float endsAt=Time.time+LaserDuration;
            while(Time.time<endsAt)
            {
                if(!ArenaVariantRuntime.IsDark00||health==null||health.IsDead||target==null||target.IsDead)break;

                Vector3 start=EyePosition();
                Vector3 end=target.transform.position+Vector3.up*.65f;
                if(laserLine!=null)
                {
                    laserLine.SetPosition(0,start);
                    laserLine.SetPosition(1,end);
                    float pulse=.13f+Mathf.Sin(Time.time*32f)*.025f;
                    laserLine.startWidth=pulse;
                    laserLine.endWidth=pulse*.72f;
                }

                Vector3 look=end-transform.position;look.y=0f;
                if(look.sqrMagnitude>.001f&&locomotion!=null)
                    locomotion.FaceAction(look,.10f);

                yield return null;
            }

            if(laserLine!=null)laserLine.enabled=false;

            if(ArenaVariantRuntime.IsDark00&&
               Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState&&
               health!=null&&!health.IsDead&&target!=null&&!target.IsDead)
            {
                Vector3 delta=target.transform.position-transform.position;
                delta.y=0f;
                if(delta.sqrMagnitude<=LaserRange*LaserRange)
                    target.TakeDamage(LaserDamage,gameObject);
            }

            // Five full seconds of downtime after the one-second laser finishes.
            nextLaserAt=Time.time+LaserCooldown;
            laserActive=false;
        }

        private void EnsureLaserVisual()
        {
            if(laserLine!=null)return;

            GameObject beam=new("DF_Dark00RedEyeLaser");
            beam.transform.SetParent(transform,false);
            laserLine=beam.AddComponent<LineRenderer>();
            laserLine.useWorldSpace=true;
            laserLine.positionCount=2;
            laserLine.numCapVertices=6;
            laserLine.numCornerVertices=2;
            laserLine.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            laserLine.receiveShadows=false;
            laserLine.alignment=LineAlignment.View;

            Shader shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color")??Shader.Find("Sprites/Default");
            if(shader!=null)
            {
                laserMaterial=new Material(shader){name="DF_Dark00RedEyeLaserMaterial"};
                Color red=new(1f,.01f,.005f,1f);
                if(laserMaterial.HasProperty("_BaseColor"))laserMaterial.SetColor("_BaseColor",red);
                if(laserMaterial.HasProperty("_Color"))laserMaterial.SetColor("_Color",red);
                if(laserMaterial.HasProperty("_EmissionColor"))
                {
                    laserMaterial.EnableKeyword("_EMISSION");
                    laserMaterial.SetColor("_EmissionColor",red*5f);
                }
                laserLine.sharedMaterial=laserMaterial;
            }
            laserLine.startColor=new Color(1f,.03f,.01f,1f);
            laserLine.endColor=new Color(1f,.16f,.04f,.92f);
            laserLine.enabled=false;
        }

        private Vector3 EyePosition()
        {
            if(eye==null)
            {
                Transform[] children=GetComponentsInChildren<Transform>(true);
                for(int i=0;i<children.Length;i++)
                    if(children[i]!=null&&children[i].name=="RedEye")
                    {
                        eye=children[i];
                        break;
                    }
            }
            return eye!=null?eye.position:transform.position+Vector3.up*.70f+transform.forward*1.55f;
        }

        private void LateUpdate()
        {
            Vector3 current=transform.position;
            if(!movementInitialized)
            {
                previousPosition=current;
                movementInitialized=true;
                return;
            }

            if(!ArenaVariantRuntime.IsDark00||
               !Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay||
               health==null||health.IsDead||locomotion==null)
            {
                previousPosition=current;
                return;
            }

            Vector3 baseMovement=current-previousPosition;
            baseMovement.y=0f;
            if(baseMovement.sqrMagnitude>.000001f)
            {
                // Add one extra copy of the normal movement: +100%, i.e. exactly double speed.
                locomotion.Move(baseMovement*ExtraMovementMultiplier,ArenaHalfWidth,ArenaHalfHeight);
            }
            previousPosition=transform.position;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            if(laserLine!=null)Destroy(laserLine.gameObject);
            if(laserMaterial!=null)Destroy(laserMaterial);
        }
    }
}
