using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Combat
{
    public static class BattleItemRuntime
    {
        public static bool TryActivateEquipped(GameObject actor,Health focusedTarget)
        {
            if(actor==null)return false;
            BattleItemType item=BattleItemInventory.Equipped;
            if(item==BattleItemType.None)return false;

            TeamMember member=actor.GetComponent<TeamMember>();
            Health health=actor.GetComponent<Health>();
            if(member==null||health==null||health.IsDead)return false;

            bool activated=item switch
            {
                BattleItemType.Heal=>BattleItemPickup.Spawn(item,member.Team,DropPosition(actor)),
                BattleItemType.Shield=>BattleItemPickup.Spawn(item,member.Team,DropPosition(actor)),
                BattleItemType.Speed=>BattleItemPickup.Spawn(item,member.Team,DropPosition(actor)),
                BattleItemType.Meteor=>TryMeteor(actor,focusedTarget),
                _=>false
            };

            return activated&&BattleItemInventory.TryConsumeEquipped(item);
        }

        private static Vector3 DropPosition(GameObject actor)
        {
            Vector3 forward=actor.transform.forward;forward.y=0;
            if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            return actor.transform.position+forward.normalized*1.65f;
        }

        private static bool TryMeteor(GameObject actor,Health target)
        {
            if(!ValidEnemyHero(actor,target))target=NearestEnemyHero(actor,15f);
            if(target==null)return false;
            BattleItemMeteorStrike.Create(actor,target,40f);
            return true;
        }

        private static bool ValidEnemyHero(GameObject actor,Health target)
        {
            if(target==null||target.IsDead||target.GetComponent<HeroIdentity>()==null)return false;
            TeamMember own=actor.GetComponent<TeamMember>(),other=target.GetComponent<TeamMember>();
            return own!=null&&other!=null&&own.Team!=other.Team;
        }

        private static Health NearestEnemyHero(GameObject actor,float range)
        {
            Health best=null;float bestSqr=range*range;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];
                if(!ValidEnemyHero(actor,h))continue;
                Vector3 d=h.transform.position-actor.transform.position;d.y=0;
                float sqr=d.sqrMagnitude;if(sqr<bestSqr){bestSqr=sqr;best=h;}
            }
            return best;
        }
    }

    public sealed class BattleItemPickup:MonoBehaviour
    {
        private const float Lifetime=10f,PickupDelay=.25f,Radius=1.35f;
        private BattleItemType type;
        private int team;
        private float spawned,nextScan;
        private Transform visual,orbit;
        private Material energyMat,darkMat;
        private bool claimed;

        public static bool Spawn(BattleItemType item,int ownerTeam,Vector3 position)
        {
            if(item!=BattleItemType.Heal&&item!=BattleItemType.Shield&&item!=BattleItemType.Speed)return false;
            GameObject go=new("DF_BattleItemDrop_"+item);
            Vector3 ground=position;ground.y=LunarSurfaceField.Surface(position.x,position.z).y+.10f;
            go.transform.position=ground;
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;if(world!=null)go.transform.SetParent(world,true);
            BattleItemPickup pickup=go.AddComponent<BattleItemPickup>();
            pickup.type=item;pickup.team=Mathf.Clamp(ownerTeam,0,1);pickup.spawned=Time.time;
            if(!pickup.Build()){Destroy(go);return false;}
            return true;
        }

        private bool Build()
        {
            Color color=type==BattleItemType.Heal?new Color(.12f,1f,.34f):
                type==BattleItemType.Shield?new Color(.18f,.82f,1f):
                new Color(1f,.72f,.18f);
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader==null)return false;
            energyMat=new Material(shader){color=color};darkMat=new Material(shader){color=new Color(.10f,.12f,.16f)};
            if(energyMat.HasProperty("_EmissionColor")){energyMat.EnableKeyword("_EMISSION");energyMat.SetColor("_EmissionColor",color*3f);}
            if(energyMat.HasProperty("_Smoothness"))energyMat.SetFloat("_Smoothness",.62f);
            if(darkMat.HasProperty("_Metallic"))darkMat.SetFloat("_Metallic",.45f);

            visual=new GameObject("DF_BattleItemVisual").transform;visual.SetParent(transform,false);visual.localPosition=Vector3.up*.72f;
            Part("Core",PrimitiveType.Sphere,visual,Vector3.zero,new Vector3(.34f,.34f,.34f),energyMat);
            Part("ShellL",PrimitiveType.Cube,visual,new Vector3(-.27f,0,0),new Vector3(.12f,.42f,.24f),darkMat).localRotation=Quaternion.Euler(18,20,12);
            Part("ShellR",PrimitiveType.Cube,visual,new Vector3(.27f,0,0),new Vector3(.12f,.42f,.24f),darkMat).localRotation=Quaternion.Euler(-18,-20,-12);

            orbit=new GameObject("DF_BattleItemOrbit").transform;orbit.SetParent(visual,false);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f;
                Part("Shard",PrimitiveType.Cube,orbit,new Vector3(Mathf.Cos(a)*.52f,0,Mathf.Sin(a)*.52f),new Vector3(.08f,.22f,.08f),energyMat)
                    .localRotation=Quaternion.Euler(25,i*90,35);
            }

            if(type==BattleItemType.Heal){Part("PlusV",PrimitiveType.Cube,visual,new Vector3(0,0,.28f),new Vector3(.10f,.34f,.06f),energyMat);Part("PlusH",PrimitiveType.Cube,visual,new Vector3(0,0,.28f),new Vector3(.34f,.10f,.06f),energyMat);}
            else if(type==BattleItemType.Shield)Part("ShieldMark",PrimitiveType.Cube,visual,new Vector3(0,0,.28f),new Vector3(.32f,.30f,.06f),energyMat).localRotation=Quaternion.Euler(0,0,45);
            else {Part("SpeedMark",PrimitiveType.Cube,visual,new Vector3(0,0,.28f),new Vector3(.38f,.08f,.06f),energyMat).localRotation=Quaternion.Euler(0,0,-18);Part("SpeedMark2",PrimitiveType.Cube,visual,new Vector3(-.08f,.13f,.28f),new Vector3(.28f,.07f,.06f),energyMat).localRotation=Quaternion.Euler(0,0,-18);}
            return true;
        }

        private Transform Part(string name,PrimitiveType primitive,Transform parent,Vector3 pos,Vector3 scale,Material mat)
        {
            GameObject go=GameObject.CreatePrimitive(primitive);go.name="DF_BattleItem"+name;go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=scale;
            Collider c=go.GetComponent<Collider>();if(c!=null)c.enabled=false;
            Renderer r=go.GetComponent<Renderer>();if(r!=null){r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            return go.transform;
        }

        private void Update()
        {
            if(Time.timeScale<=0f||claimed)return;
            float age=Time.time-spawned;if(age>=Lifetime){Destroy(gameObject);return;}
            if(visual!=null)
            {
                visual.localPosition=Vector3.up*(.72f+Mathf.Sin(Time.time*3.2f)*.06f);
                visual.localScale=Vector3.one*(1f+Mathf.Sin(Time.time*4.4f)*.05f);
                orbit?.Rotate(Vector3.up,70f*Time.deltaTime,Space.Self);
            }
            if(age<PickupDelay||Time.time<nextScan)return;
            nextScan=Time.time+.08f;
            float radiusSqr=Radius*Radius;
            for(int i=0;i<Health.Active.Count;i++)
            {
                Health h=Health.Active[i];if(h==null||h.IsDead||h.GetComponent<HeroIdentity>()==null)continue;
                TeamMember member=h.GetComponent<TeamMember>();if(member==null||member.Team!=team)continue;
                Vector3 d=h.transform.position-transform.position;d.y=0;if(d.sqrMagnitude>radiusSqr)continue;
                Apply(h);return;
            }
        }

        private void Apply(Health hero)
        {
            if(claimed)return;claimed=true;
            switch(type)
            {
                case BattleItemType.Heal:
                    hero.Heal(hero.Max*.20f,false);
                    HeroSkillVfx.Support(hero.gameObject,hero,false,.45f);
                    break;
                case BattleItemType.Shield:
                    hero.AddShield(hero.Max*.20f,5f,false);
                    HeroSkillVfx.Support(hero.gameObject,hero,true,5f);
                    break;
                case BattleItemType.Speed:
                    (hero.GetComponent<HeroCombatStatus>()??hero.gameObject.AddComponent<HeroCombatStatus>()).BoostSpeed(1.10f,5f);
                    PrototypeVfx.Ring(hero.transform.position,new Color(1f,.72f,.18f),1.1f,.3f);
                    break;
            }
            PrototypeVfx.Burst(hero.transform.position,energyMat!=null?energyMat.color:Color.white,.6f,5);
            Destroy(gameObject);
        }

        private void OnDestroy(){if(energyMat!=null)Destroy(energyMat);if(darkMat!=null)Destroy(darkMat);}
    }

    public sealed class BattleItemMeteorStrike:MonoBehaviour
    {
        private const float Delay=1.15f,ImpactRadius=2.1f;
        private Health target;
        private GameObject source,warning,meteor;
        private LineRenderer warningLine,trail;
        private Material warningMat,rockMat;
        private Mesh meteorMesh;
        private Vector3 impact;
        private float hitAt,damage;

        public static void Create(GameObject source,Health target,float damage)
        {
            if(source==null||target==null||target.IsDead)return;
            GameObject go=new("DF_BattleItemMeteorStrike");
            Transform world=GameObject.Find("DF_MatchWorld")?.transform;if(world!=null)go.transform.SetParent(world,true);
            BattleItemMeteorStrike strike=go.AddComponent<BattleItemMeteorStrike>();
            strike.source=source;strike.target=target;strike.damage=damage;strike.impact=target.transform.position;strike.impact.y=.14f;strike.hitAt=Time.time+Delay;
            strike.Build(world);
        }

        private void Build(Transform world)
        {
            Shader ink=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default");
            Shader rock=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(ink==null||rock==null)return;
            warningMat=new Material(ink){name="DF_ItemMeteorWarning"};rockMat=new Material(rock){name="DF_ItemMeteorRock",color=new Color(.19f,.14f,.11f)};
            if(rockMat.HasProperty("_EmissionColor")){rockMat.EnableKeyword("_EMISSION");rockMat.SetColor("_EmissionColor",new Color(.55f,.15f,.025f));}

            warning=new GameObject("DF_ItemMeteorTarget");if(world!=null)warning.transform.SetParent(world,false);
            warningLine=warning.AddComponent<LineRenderer>();warningLine.sharedMaterial=warningMat;warningLine.loop=true;warningLine.useWorldSpace=true;
            warningLine.positionCount=40;warningLine.widthMultiplier=.09f;warningLine.startColor=warningLine.endColor=new Color(1f,.44f,.08f);
            DrawWarning();

            meteorMesh=LunarFragmentGeometry.Create(13);
            meteor=ProductionMeshFactory.MeshObject("DF_ItemFallingMeteor",world,meteorMesh,rockMat);
            meteor.transform.localScale=Vector3.one*.62f;meteor.transform.position=impact+Vector3.up*9f;
            trail=meteor.AddComponent<LineRenderer>();trail.sharedMaterial=warningMat;trail.useWorldSpace=true;trail.positionCount=2;
            trail.startWidth=.16f;trail.endWidth=.02f;trail.startColor=new Color(1f,.55f,.15f);trail.endColor=new Color(.35f,.12f,.03f);
            trail.shadowCastingMode=ShadowCastingMode.Off;
        }

        private void DrawWarning()
        {
            if(warningLine==null)return;
            for(int i=0;i<40;i++){float a=i*Mathf.PI*2f/40f;warningLine.SetPosition(i,impact+new Vector3(Mathf.Cos(a)*ImpactRadius,0,Mathf.Sin(a)*ImpactRadius));}
        }

        private void Update()
        {
            if(Time.timeScale<=0f)return;
            if(Time.time<hitAt)
            {
                float remain=Mathf.Max(0,hitAt-Time.time),height=Mathf.Lerp(.35f,9f,Mathf.Clamp01(remain/Delay));
                if(meteor!=null)
                {
                    meteor.transform.position=new Vector3(impact.x,height,impact.z);meteor.transform.Rotate(85*Time.deltaTime,38*Time.deltaTime,54*Time.deltaTime);
                    if(trail!=null){trail.SetPosition(0,meteor.transform.position);trail.SetPosition(1,meteor.transform.position+new Vector3(.14f,1.5f,.1f));}
                }
                return;
            }

            Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.MeteorImpact,impact,.5f);
            CombatImpactPool.Emit(new Vector3(impact.x,.3f,impact.z),new Color(1f,.63f,.23f),1);
            PrototypeVfx.RadialBurst(impact,new Color(1f,.48f,.12f),ImpactRadius);
            if(target!=null&&!target.IsDead)
            {
                TeamMember sourceTeam=source!=null?source.GetComponent<TeamMember>():null;
                TeamMember targetTeam=target.GetComponent<TeamMember>();
                Vector3 d=target.transform.position-impact;d.y=0;
                if(sourceTeam!=null&&targetTeam!=null&&sourceTeam.Team!=targetTeam.Team&&d.sqrMagnitude<=ImpactRadius*ImpactRadius)
                    target.TakeDamage(damage,source);
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if(warning!=null)Destroy(warning);if(meteor!=null)Destroy(meteor);
            if(warningMat!=null)Destroy(warningMat);if(rockMat!=null)Destroy(rockMat);if(meteorMesh!=null)Destroy(meteorMesh);
        }
    }
}
