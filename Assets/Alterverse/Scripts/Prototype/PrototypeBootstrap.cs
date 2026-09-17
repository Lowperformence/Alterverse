using Alterverse.AI;
using Alterverse.CameraSystem;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.UI;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Prototype
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildOnStart = true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate(){if(Object.FindAnyObjectByType<PrototypeBootstrap>()==null)new GameObject("AV_PrototypeBootstrap").AddComponent<PrototypeBootstrap>();}
        private void Start(){if(buildOnStart)BuildPrototype();}

        public void BuildPrototype()
        {
            if(GameObject.Find("AV_Player")!=null)return;BuildArena();
            GameObject player=GameObject.CreatePrimitive(PrimitiveType.Capsule);player.name="AV_Player";player.transform.position=new Vector3(-22,1,-3);Destroy(player.GetComponent<CapsuleCollider>());
            CharacterController cc=player.AddComponent<CharacterController>();cc.height=2;cc.radius=.5f;Health ph=player.AddComponent<Health>();ph.Configure(140);player.AddComponent<TeamMember>().Initialize(0);player.AddComponent<HeroMotor>();player.AddComponent<HeroRespawn>();player.AddComponent<BasicAttack>();VeyrAbilities abilities=player.AddComponent<VeyrAbilities>();SetColor(player,new Color(.055f,.035f,.09f));player.AddComponent<Alterverse.Combat.CombatFeedback>();AddVeyrBlade(player.transform);
            GameObject enemy=GameObject.CreatePrimitive(PrimitiveType.Capsule);enemy.name="AV_DimensionZeroHunter";enemy.transform.position=new Vector3(10,1,0);Health eh=enemy.AddComponent<Health>();eh.Configure(180);enemy.AddComponent<TeamMember>().Initialize(1);PrototypeEnemyBot bot=enemy.AddComponent<PrototypeEnemyBot>();bot.SetTarget(ph);SetColor(enemy,new Color(.62f,.055f,.09f));enemy.AddComponent<Alterverse.Combat.CombatFeedback>();AddEnemyBlade(enemy.transform);
            SpawnHealingZone zeroZone=GameObject.Find("AV_ZeroSpawnZone")?.GetComponent<SpawnHealingZone>();if(zeroZone!=null){zeroZone.InitializeTeam(0);zeroZone.Initialize(ph);}SpawnHealingZone lumenZone=GameObject.Find("AV_LumenSpawnZone")?.GetComponent<SpawnHealingZone>();if(lumenZone!=null){lumenZone.InitializeTeam(1);lumenZone.Initialize(eh);}
            UnityEngine.Camera cam=UnityEngine.Camera.main;if(cam==null){GameObject c=new("Main Camera");c.tag="MainCamera";cam=c.AddComponent<UnityEngine.Camera>();}cam.backgroundColor=new Color(.018f,.015f,.045f);MobaCamera mc=cam.GetComponent<MobaCamera>();if(mc==null)mc=cam.gameObject.AddComponent<MobaCamera>();mc.SetTarget(player.transform);new GameObject("AV_HUD").AddComponent<PrototypeHUD>().Initialize(ph,abilities,eh);
            new GameObject("AV_LaneWaveSpawner").AddComponent<LaneWaveSpawner>();new GameObject("AV_5v5Roster").AddComponent<FiveVsFiveTestRoster>();
        }

        private static void BuildArena()
        {
            Color zeroGround=new(.045f,.035f,.075f),lumenGround=new(.065f,.15f,.17f),lane=new(.16f,.105f,.22f);
            CreateGround("AV_DimensionZeroGround",new Vector3(-15,-.12f,0),new Vector3(30,.2f,60),zeroGround);CreateGround("AV_LumenGround",new Vector3(15,-.12f,0),new Vector3(30,.2f,60),lumenGround);
            CreateLane("AV_TopLane",12f,lane);CreateLane("AV_MidLane",0f,lane);CreateLane("AV_BotLane",-12f,lane);CreateJungle(-1);CreateJungle(1);
            CreateVisualBlock("AV_ZeroBasePad",new Vector3(-25,.04f,0),new Vector3(7,.05f,18),new Color(.11f,.04f,.17f),Vector3.zero);CreateVisualBlock("AV_LumenBasePad",new Vector3(25,.04f,0),new Vector3(7,.05f,18),new Color(.06f,.27f,.30f),Vector3.zero);
            CreateBaseCore(new Vector3(-25,1.15f,0),new Color(.52f,.08f,.76f),"AV_ZeroRealityCore");CreateBaseCore(new Vector3(25,1.15f,0),new Color(.08f,.72f,.78f),"AV_LumenRealityCore");CreateSpawnZone(new Vector3(-23,.07f,-3),new Color(.52f,.08f,.76f),"AV_ZeroSpawnZone");CreateSpawnZone(new Vector3(23,.07f,3),new Color(.08f,.72f,.78f),"AV_LumenSpawnZone");CreateTowers();
            for(int i=-12;i<=12;i++){float z=i*2.35f,x=Mathf.Sin(i*1.73f)*.55f;CreateVisualBlock("AV_RealityScar",new Vector3(x,.055f,z),new Vector3(.34f,.045f,1.95f),new Color(.48f,.07f,.72f),new Vector3(0,Mathf.Sin(i)*13f,0));if(i%3==0)CreateShard(new Vector3(x+(i%2==0?1.15f:-1.15f),.22f,z+.55f),new Color(.55f,.10f,.82f),.42f,false);}
            for(int z=-24;z<=24;z+=8){CreateObelisk(new Vector3(-26,1.25f,z),new Color(.13f,.035f,.22f),false);CreateObelisk(new Vector3(26,1.25f,z),new Color(.055f,.31f,.35f),true);}for(int z=-18;z<=18;z+=9){CreateCrystal(new Vector3(-9,.65f,z),new Color(.42f,.07f,.68f));CreateCrystal(new Vector3(9,.65f,z),new Color(.06f,.62f,.70f));}
            CreateObjectiveZone(new Vector3(0,.07f,20),new Color(.16f,.55f,.78f),"AV_NorthObjective");CreateObjectiveZone(new Vector3(0,.07f,-20),new Color(.72f,.16f,.48f),"AV_SouthObjective");CreateRiftCore();
        }

        private static void CreateSpawnZone(Vector3 p,Color color,string name){GameObject zone=GameObject.CreatePrimitive(PrimitiveType.Cylinder);zone.name=name;zone.transform.position=p;zone.transform.localScale=new Vector3(4.5f,.025f,4.5f);DisableCollider(zone);SetColor(zone,color);zone.AddComponent<SpawnHealingZone>();}
        private static void CreateLane(string name,float z,Color color){CreateVisualBlock(name,new Vector3(0,.035f,z),new Vector3(54,.045f,2.2f),color,Vector3.zero);CreateVisualBlock(name+"_ZeroConnector",new Vector3(-22,.04f,z*.52f),new Vector3(12,.045f,1.5f),color,new Vector3(0,z>0?-20f:z<0?20f:0f,0));CreateVisualBlock(name+"_LumenConnector",new Vector3(22,.04f,z*.52f),new Vector3(12,.045f,1.5f),color,new Vector3(0,z>0?20f:z<0?-20f:0f,0));}
        private static void CreateTowers(){float[] lanes={12f,0f,-12f};foreach(float z in lanes){CreateTower(new Vector3(-17,1.25f,z),0,new Color(.43f,.08f,.62f),"AV_ZeroTower");CreateTower(new Vector3(17,1.25f,z),1,new Color(.07f,.58f,.64f),"AV_LumenTower");}}
        private static void CreateTower(Vector3 p,int team,Color color,string name){GameObject tower=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tower.name=name;tower.transform.position=p;tower.transform.localScale=new Vector3(.9f,1.25f,.9f);SetColor(tower,color);Health health=tower.AddComponent<Health>();health.Configure(260f,true);LaneTower laneTower=tower.AddComponent<LaneTower>();laneTower.Initialize(team);CreateVisualCylinder(name+"_Base",new Vector3(p.x,.08f,p.z),new Vector3(1.25f,.03f,1.25f),color);}
        private static void CreateJungle(int side){float x=side*13.5f;Color path=side<0?new Color(.10f,.065f,.14f):new Color(.075f,.22f,.23f),camp=side<0?new Color(.34f,.08f,.48f):new Color(.08f,.46f,.50f);CreateVisualBlock(side<0?"AV_ZeroJungleNorth":"AV_LumenJungleNorth",new Vector3(x,.025f,6),new Vector3(12,.035f,1.15f),path,new Vector3(0,side*18f,0));CreateVisualBlock(side<0?"AV_ZeroJungleSouth":"AV_LumenJungleSouth",new Vector3(x,.025f,-6),new Vector3(12,.035f,1.15f),path,new Vector3(0,-side*18f,0));CreateCampMarker(new Vector3(x-side*2.2f,.08f,6.8f),camp,side<0?"AV_ZeroCampNorth":"AV_LumenCampNorth");CreateCampMarker(new Vector3(x-side*2.2f,.08f,-6.8f),camp,side<0?"AV_ZeroCampSouth":"AV_LumenCampSouth");CreateJungleLandmark(new Vector3(x+side*3f,.75f,side*8.5f),camp,side<0?"AV_ZeroJungleLandmark":"AV_LumenJungleLandmark");}
        private static void CreateJungleLandmark(Vector3 p,Color color,string name){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=p;g.transform.localScale=new Vector3(.8f,1.7f,.8f);g.transform.rotation=Quaternion.Euler(15,45,15);DisableCollider(g);SetColor(g,color);g.AddComponent<MapAmbientVisual>();}
        private static void CreateCampMarker(Vector3 p,Color color,string name){CreateVisualCylinder(name,p,new Vector3(1.35f,.025f,1.35f),color);CreateShard(p+Vector3.up*.55f,color,.45f,true);}
        private static void CreateObjectiveZone(Vector3 p,Color color,string name){CreateVisualCylinder(name,p,new Vector3(2.5f,.025f,2.5f),color);for(int i=0;i<4;i++){float a=i*90f*Mathf.Deg2Rad;CreateShard(p+new Vector3(Mathf.Cos(a)*2f,.55f,Mathf.Sin(a)*2f),color,.4f,true);}}
        private static void CreateBaseCore(Vector3 p,Color color,string name){GameObject core=GameObject.CreatePrimitive(PrimitiveType.Cylinder);core.name=name;core.transform.position=p;core.transform.localScale=new Vector3(1.4f,1.15f,1.4f);DisableCollider(core);SetColor(core,color);}
        private static void CreateRiftCore(){CreateVisualCylinder("AV_RiftOuter",new Vector3(0,.075f,0),new Vector3(3.25f,.035f,3.25f),new Color(.20f,.045f,.29f));CreateVisualCylinder("AV_RiftInner",new Vector3(0,.095f,0),new Vector3(2.35f,.045f,2.35f),new Color(.55f,.065f,.80f));CreateVisualCylinder("AV_RiftEye",new Vector3(0,.115f,0),new Vector3(1.15f,.05f,1.15f),new Color(.08f,.025f,.12f));GameObject core=GameObject.CreatePrimitive(PrimitiveType.Cube);core.name="AV_RiftCore";core.transform.position=new Vector3(0,1.45f,0);core.transform.localScale=Vector3.one*.72f;core.transform.rotation=Quaternion.Euler(25,45,20);DisableCollider(core);SetColor(core,new Color(.72f,.18f,1f));core.AddComponent<RiftCoreVisual>();CreateShard(new Vector3(-1.15f,1.05f,.25f),new Color(.50f,.08f,.78f),.55f,true);CreateShard(new Vector3(1.05f,.85f,-.35f),new Color(.10f,.58f,.70f),.48f,true);CreateShard(new Vector3(.35f,1.2f,1.15f),new Color(.62f,.10f,.88f),.38f,true);}
        private static void CreateGround(string name,Vector3 p,Vector3 scale,Color color){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=p;g.transform.localScale=scale;SetColor(g,color);}
        private static void CreateObelisk(Vector3 p,Color color,bool future){GameObject o=GameObject.CreatePrimitive(future?PrimitiveType.Cube:PrimitiveType.Cylinder);o.name=future?"AV_LumenStructure":"AV_ZeroObelisk";o.transform.position=p;o.transform.localScale=future?new Vector3(.65f,2.3f,.65f):new Vector3(.8f,1.8f,.8f);o.transform.rotation=Quaternion.Euler(0,future?45:0,future?8:0);SetColor(o,color);}
        private static void CreateCrystal(Vector3 p,Color color){GameObject c=GameObject.CreatePrimitive(PrimitiveType.Cube);c.name="AV_RealityCrystal";c.transform.position=p;c.transform.localScale=new Vector3(.38f,1.25f,.38f);c.transform.rotation=Quaternion.Euler(18,45,18);DisableCollider(c);SetColor(c,color);c.AddComponent<MapAmbientVisual>();}
        private static void CreateShard(Vector3 p,Color color,float size,bool animated){GameObject s=GameObject.CreatePrimitive(PrimitiveType.Cube);s.name="AV_RiftShard";s.transform.position=p;s.transform.localScale=new Vector3(size*.35f,size,size*.35f);s.transform.rotation=Quaternion.Euler(20,45,25);DisableCollider(s);SetColor(s,color);if(animated)s.AddComponent<MapAmbientVisual>();}
        private static void CreateVisualCylinder(string name,Vector3 p,Vector3 scale,Color color){GameObject c=GameObject.CreatePrimitive(PrimitiveType.Cylinder);c.name=name;c.transform.position=p;c.transform.localScale=scale;DisableCollider(c);SetColor(c,color);}
        private static void CreateVisualBlock(string name,Vector3 p,Vector3 scale,Color color,Vector3 rotation){GameObject b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;b.transform.position=p;b.transform.localScale=scale;b.transform.rotation=Quaternion.Euler(rotation);DisableCollider(b);SetColor(b,color);}
        private static void AddVeyrBlade(Transform p)=>AddBlade(p,"Veyr_ShadowBlade",new Vector3(.55f,.15f,.15f),new Vector3(.12f,1.45f,.18f),new Color(.025f,.015f,.04f),-18);
        private static void AddEnemyBlade(Transform p)=>AddBlade(p,"Hunter_RiftBlade",new Vector3(-.55f,.1f,.15f),new Vector3(.12f,1.15f,.18f),new Color(.38f,.02f,.035f),18);
        private static void AddBlade(Transform p,string n,Vector3 pos,Vector3 scale,Color color,float z){GameObject b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=n;b.transform.SetParent(p,false);b.transform.localPosition=pos;b.transform.localRotation=Quaternion.Euler(8,0,z);b.transform.localScale=scale;DisableCollider(b);SetColor(b,color);}
        private static void DisableCollider(GameObject g){Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;}
        private static void SetColor(GameObject g,Color color){Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=color;}
    }
}
