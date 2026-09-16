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
        private static void AutoCreate(){if(Object.FindFirstObjectByType<PrototypeBootstrap>()==null)new GameObject("AV_PrototypeBootstrap").AddComponent<PrototypeBootstrap>();}
        private void Start(){if(buildOnStart)BuildPrototype();}

        public void BuildPrototype()
        {
            if(GameObject.Find("AV_Player")!=null)return;
            BuildArena();
            GameObject player=GameObject.CreatePrimitive(PrimitiveType.Capsule); player.name="AV_Player"; player.transform.position=new Vector3(-4,1,-4); Destroy(player.GetComponent<CapsuleCollider>());
            CharacterController cc=player.AddComponent<CharacterController>();cc.height=2;cc.radius=.5f;
            Health ph=player.AddComponent<Health>();ph.Configure(140);player.AddComponent<HeroMotor>();player.AddComponent<HeroRespawn>();player.AddComponent<BasicAttack>();VeyrAbilities abilities=player.AddComponent<VeyrAbilities>();SetColor(player,new Color(.055f,.035f,.09f));player.AddComponent<CombatFeedback>();AddVeyrBlade(player.transform);
            GameObject enemy=GameObject.CreatePrimitive(PrimitiveType.Capsule);enemy.name="AV_DimensionZeroHunter";enemy.transform.position=new Vector3(5,1,4);Health eh=enemy.AddComponent<Health>();eh.Configure(180);PrototypeEnemyBot bot=enemy.AddComponent<PrototypeEnemyBot>();bot.SetTarget(ph);SetColor(enemy,new Color(.62f,.055f,.09f));enemy.AddComponent<CombatFeedback>();AddEnemyBlade(enemy.transform);
            UnityEngine.Camera cam=UnityEngine.Camera.main;if(cam==null){GameObject c=new("Main Camera");c.tag="MainCamera";cam=c.AddComponent<UnityEngine.Camera>();}cam.backgroundColor=new Color(.018f,.015f,.045f);MobaCamera mc=cam.GetComponent<MobaCamera>();if(mc==null)mc=cam.gameObject.AddComponent<MobaCamera>();mc.SetTarget(player.transform);
            GameObject hud=new("AV_HUD");hud.AddComponent<PrototypeHUD>().Initialize(ph,abilities,eh);
        }

        private static void BuildArena()
        {
            // Gameplay floor keeps its colliders. Everything added above it is visual-only unless explicitly intended as cover.
            CreateGround("AV_DimensionZeroGround",new Vector3(-7.5f,-.12f,0),new Vector3(15,.2f,30),new Color(.045f,.035f,.075f));
            CreateGround("AV_LumenGround",new Vector3(7.5f,-.12f,0),new Vector3(15,.2f,30),new Color(.065f,.15f,.17f));
            CreateBlock("AV_MainLane",new Vector3(0,.035f,0),new Vector3(27,.045f,1.15f),new Color(.16f,.105f,.22f));

            // Broken dimensional scar instead of one perfectly straight stripe.
            for(int i=-6;i<=6;i++)
            {
                float z=i*2.35f;
                float x=Mathf.Sin(i*1.73f)*.48f;
                CreateVisualBlock("AV_RealityScar",new Vector3(x,.055f,z),new Vector3(.34f,.045f,1.95f),new Color(.48f,.07f,.72f),new Vector3(0,Mathf.Sin(i)*13f,0));
                if(i%2==0) CreateShard(new Vector3(x+(i%4==0?1.15f:-1.15f),.22f,z+.55f),new Color(.55f,.10f,.82f),.42f);
            }

            for(int z=-12;z<=12;z+=6)
            {
                CreateObelisk(new Vector3(-10,1.25f,z),new Color(.13f,.035f,.22f),false);
                CreateObelisk(new Vector3(10,1.25f,z),new Color(.055f,.31f,.35f),true);
            }
            for(int i=-2;i<=2;i++)
            {
                CreateCrystal(new Vector3(-5.5f,.65f,i*5.2f),new Color(.42f,.07f,.68f));
                CreateCrystal(new Vector3(5.5f,.65f,i*5.2f),new Color(.06f,.62f,.70f));
            }

            // Central collision point: layered rings plus a floating animated core.
            CreateVisualCylinder("AV_RiftOuter",new Vector3(0,.075f,0),new Vector3(3.25f,.035f,3.25f),new Color(.20f,.045f,.29f));
            CreateVisualCylinder("AV_RiftInner",new Vector3(0,.095f,0),new Vector3(2.35f,.045f,2.35f),new Color(.55f,.065f,.80f));
            CreateVisualCylinder("AV_RiftEye",new Vector3(0,.115f,0),new Vector3(1.15f,.05f,1.15f),new Color(.08f,.025f,.12f));

            GameObject core=GameObject.CreatePrimitive(PrimitiveType.Cube);core.name="AV_RiftCore";core.transform.position=new Vector3(0,1.45f,0);core.transform.localScale=Vector3.one*.72f;core.transform.rotation=Quaternion.Euler(25,45,20);DisableCollider(core);SetColor(core,new Color(.72f,.18f,1f));core.AddComponent<RiftCoreVisual>();
            CreateShard(new Vector3(-1.15f,1.05f,.25f),new Color(.50f,.08f,.78f),.55f);
            CreateShard(new Vector3(1.05f,.85f,-.35f),new Color(.10f,.58f,.70f),.48f);
            CreateShard(new Vector3(.35f,1.2f,1.15f),new Color(.62f,.10f,.88f),.38f);
        }

        private static void CreateGround(string name,Vector3 p,Vector3 scale,Color color){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=p;g.transform.localScale=scale;SetColor(g,color);}
        private static void CreateObelisk(Vector3 p,Color color,bool future){GameObject o=GameObject.CreatePrimitive(future?PrimitiveType.Cube:PrimitiveType.Cylinder);o.name=future?"AV_LumenStructure":"AV_ZeroObelisk";o.transform.position=p;o.transform.localScale=future?new Vector3(.65f,2.3f,.65f):new Vector3(.8f,1.8f,.8f);o.transform.rotation=Quaternion.Euler(0,future?45:0,future?8:0);SetColor(o,color);}
        private static void CreateCrystal(Vector3 p,Color color){GameObject c=GameObject.CreatePrimitive(PrimitiveType.Cube);c.name="AV_RealityCrystal";c.transform.position=p;c.transform.localScale=new Vector3(.38f,1.25f,.38f);c.transform.rotation=Quaternion.Euler(18,45,18);DisableCollider(c);SetColor(c,color);}
        private static void CreateShard(Vector3 p,Color color,float size){GameObject s=GameObject.CreatePrimitive(PrimitiveType.Cube);s.name="AV_RiftShard";s.transform.position=p;s.transform.localScale=new Vector3(size*.35f,size,size*.35f);s.transform.rotation=Quaternion.Euler(20,45,25);DisableCollider(s);SetColor(s,color);}
        private static void CreateVisualCylinder(string name,Vector3 p,Vector3 scale,Color color){GameObject c=GameObject.CreatePrimitive(PrimitiveType.Cylinder);c.name=name;c.transform.position=p;c.transform.localScale=scale;DisableCollider(c);SetColor(c,color);}
        private static void CreateVisualBlock(string name,Vector3 p,Vector3 scale,Color color,Vector3 rotation){GameObject b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;b.transform.position=p;b.transform.localScale=scale;b.transform.rotation=Quaternion.Euler(rotation);DisableCollider(b);SetColor(b,color);}
        private static void CreateBlock(string name,Vector3 p,Vector3 scale,Color color){CreateVisualBlock(name,p,scale,color,Vector3.zero);}
        private static void AddVeyrBlade(Transform p)=>AddBlade(p,"Veyr_ShadowBlade",new Vector3(.55f,.15f,.15f),new Vector3(.12f,1.45f,.18f),new Color(.025f,.015f,.04f),-18);
        private static void AddEnemyBlade(Transform p)=>AddBlade(p,"Hunter_RiftBlade",new Vector3(-.55f,.1f,.15f),new Vector3(.12f,1.15f,.18f),new Color(.38f,.02f,.035f),18);
        private static void AddBlade(Transform p,string n,Vector3 pos,Vector3 scale,Color color,float z){GameObject b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=n;b.transform.SetParent(p,false);b.transform.localPosition=pos;b.transform.localRotation=Quaternion.Euler(8,0,z);b.transform.localScale=scale;DisableCollider(b);SetColor(b,color);}
        private static void DisableCollider(GameObject g){Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;}
        private static void SetColor(GameObject g,Color color){Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=color;}
    }
}
