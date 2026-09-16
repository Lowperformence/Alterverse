using Alterverse.AI;
using Alterverse.CameraSystem;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.UI;
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
            UnityEngine.Camera cam=UnityEngine.Camera.main;if(cam==null){GameObject c=new("Main Camera");c.tag="MainCamera";cam=c.AddComponent<UnityEngine.Camera>();}cam.backgroundColor=new Color(.025f,.02f,.055f);MobaCamera mc=cam.GetComponent<MobaCamera>();if(mc==null)mc=cam.gameObject.AddComponent<MobaCamera>();mc.SetTarget(player.transform);
            GameObject hud=new("AV_HUD");hud.AddComponent<PrototypeHUD>().Initialize(ph,abilities,eh);
        }

        private static void BuildArena()
        {
            CreateGround("AV_DimensionZeroGround",new Vector3(-7.5f,-.12f,0),new Vector3(15,.2f,30),new Color(.055f,.045f,.085f));
            CreateGround("AV_LumenGround",new Vector3(7.5f,-.12f,0),new Vector3(15,.2f,30),new Color(.075f,.16f,.18f));
            CreateBlock("AV_RealityScar",new Vector3(0,.02f,0),new Vector3(.55f,.06f,30),new Color(.48f,.08f,.72f));
            CreateBlock("AV_MainLane",new Vector3(0,.035f,0),new Vector3(27,.045f,1.15f),new Color(.18f,.12f,.24f));
            for(int z=-12;z<=12;z+=6){CreateObelisk(new Vector3(-10,1.25f,z),new Color(.16f,.045f,.24f),false);CreateObelisk(new Vector3(10,1.25f,z),new Color(.08f,.34f,.38f),true);}
            for(int i=-2;i<=2;i++){CreateCrystal(new Vector3(-5.5f,.65f,i*5.2f),new Color(.42f,.08f,.68f));CreateCrystal(new Vector3(5.5f,.65f,i*5.2f),new Color(.08f,.65f,.72f));}
            GameObject rift=GameObject.CreatePrimitive(PrimitiveType.Cylinder);rift.name="AV_CentralRealityRift";rift.transform.position=new Vector3(0,.09f,0);rift.transform.localScale=new Vector3(2.4f,.07f,2.4f);DisableCollider(rift);SetColor(rift,new Color(.58f,.08f,.82f));
            GameObject core=GameObject.CreatePrimitive(PrimitiveType.Sphere);core.name="AV_RiftCore";core.transform.position=new Vector3(0,1.2f,0);core.transform.localScale=Vector3.one*.65f;DisableCollider(core);SetColor(core,new Color(.72f,.22f,1f));
        }

        private static void CreateGround(string name,Vector3 p,Vector3 scale,Color color){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=p;g.transform.localScale=scale;SetColor(g,color);}
        private static void CreateObelisk(Vector3 p,Color color,bool future){GameObject o=GameObject.CreatePrimitive(future?PrimitiveType.Cube:PrimitiveType.Cylinder);o.name=future?"AV_LumenStructure":"AV_ZeroObelisk";o.transform.position=p;o.transform.localScale=future?new Vector3(.65f,2.3f,.65f):new Vector3(.8f,1.8f,.8f);o.transform.rotation=Quaternion.Euler(0,future?45:0,future?8:0);SetColor(o,color);}
        private static void CreateCrystal(Vector3 p,Color color){GameObject c=GameObject.CreatePrimitive(PrimitiveType.Cube);c.name="AV_RealityCrystal";c.transform.position=p;c.transform.localScale=new Vector3(.38f,1.25f,.38f);c.transform.rotation=Quaternion.Euler(18,45,18);DisableCollider(c);SetColor(c,color);}
        private static void CreateBlock(string name,Vector3 p,Vector3 scale,Color color){GameObject b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;b.transform.position=p;b.transform.localScale=scale;DisableCollider(b);SetColor(b,color);}
        private static void AddVeyrBlade(Transform p)=>AddBlade(p,"Veyr_ShadowBlade",new Vector3(.55f,.15f,.15f),new Vector3(.12f,1.45f,.18f),new Color(.025f,.015f,.04f),-18);
        private static void AddEnemyBlade(Transform p)=>AddBlade(p,"Hunter_RiftBlade",new Vector3(-.55f,.1f,.15f),new Vector3(.12f,1.15f,.18f),new Color(.38f,.02f,.035f),18);
        private static void AddBlade(Transform p,string n,Vector3 pos,Vector3 scale,Color color,float z){GameObject b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=n;b.transform.SetParent(p,false);b.transform.localPosition=pos;b.transform.localRotation=Quaternion.Euler(8,0,z);b.transform.localScale=scale;DisableCollider(b);SetColor(b,color);}
        private static void DisableCollider(GameObject g){Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;}
        private static void SetColor(GameObject g,Color color){Renderer r=g.GetComponent<Renderer>();if(r!=null)r.material.color=color;}
    }
}
