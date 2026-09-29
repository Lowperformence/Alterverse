using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only prestige pass for the 1100-shard LEGENDARY weapon collection (variant 1).
    /// It adds large silhouette pieces, animated reactor rings and floating energy hardware below
    /// the existing gameplay weapon pivots. Combat timing, hit logic, damage and muzzle markers are untouched.
    /// </summary>
    [DefaultExecutionOrder(2150)]
    internal sealed class LegendaryWeaponPrestigeBootstrap : MonoBehaviour
    {
        private static LegendaryWeaponPrestigeBootstrap instance;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_LegendaryWeaponPrestige");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<LegendaryWeaponPrestigeBootstrap>();
        }

        private void LateUpdate()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.18f;
            HeroIdentity[] heroes=FindObjectsByType<HeroIdentity>(FindObjectsSortMode.None);
            for(int i=0;i<heroes.Length;i++)Sync(heroes[i]);
        }

        private static void Sync(HeroIdentity identity)
        {
            if(identity==null)return;
            SkinPreviewSelection preview=identity.GetComponent<SkinPreviewSelection>();
            int variant=preview!=null?preview.WeaponSkinVariant:
                identity.GetComponent<HeroRespawn>()!=null?WeaponSkinInventory.EquippedVariant(identity.Hero):-1;

            LegendaryWeaponPrestigePresentation pass=identity.GetComponent<LegendaryWeaponPrestigePresentation>();
            if(variant!=1)
            {
                if(pass!=null)pass.Remove();
                return;
            }
            if(pass==null)pass=identity.gameObject.AddComponent<LegendaryWeaponPrestigePresentation>();
            pass.Show(identity);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class LegendaryWeaponPrestigePresentation : MonoBehaviour
    {
        private readonly List<GameObject> anchors=new();
        private readonly List<Material> materials=new();
        private readonly List<Mesh> meshes=new();
        private HeroIdentity identity;
        private Transform visualRoot;
        private bool built;

        public void Show(HeroIdentity hero)
        {
            Transform nextRoot=FindRecursive(transform,"DF_VisualRoot");
            if(hero==null||nextRoot==null)return;
            if(built&&identity==hero&&visualRoot==nextRoot)return;
            Clear();
            identity=hero;visualRoot=nextRoot;built=true;
            Build();
        }

        public void Remove()
        {
            Clear();
            Destroy(this);
        }

        private void OnDestroy()=>Clear();

        private void Build()
        {
            WeaponSkinItem item=WeaponSkinCatalog.For(identity.Hero,1);
            Color glow=item.Glow;
            Material voidMetal=Mat("VoidMetal",Color.Lerp(new Color(.015f,.020f,.035f),glow,.10f),.93f,.91f,false,1f);
            Material prestigeMetal=Mat("PrestigeMetal",Color.Lerp(new Color(.62f,.66f,.73f),glow,.20f),.98f,.96f,false,1f);
            Material energy=Mat("Energy",Color.Lerp(glow,Color.white,.12f),.05f,.98f,true,5.4f);
            Material hot=Mat("HotCore",Color.Lerp(glow,Color.white,.58f),.02f,1f,true,8.0f);
            Material crown=Mat("Crown",new Color(1f,.73f,.24f),.92f,.95f,true,2.6f);

            switch(identity.Hero)
            {
                case HeroId.Veyr: BuildVeyr(voidMetal,prestigeMetal,energy,hot,crown);break;
                case HeroId.Kaelon: BuildKaelon(voidMetal,prestigeMetal,energy,hot,crown);break;
                case HeroId.Nyra: BuildNyra(voidMetal,prestigeMetal,energy,hot,crown);break;
                case HeroId.Elyra: BuildElyra(voidMetal,prestigeMetal,energy,hot,crown);break;
                case HeroId.Mira: BuildMira(voidMetal,prestigeMetal,energy,hot,crown);break;
                case HeroId.Rook: BuildRook(voidMetal,prestigeMetal,energy,hot,crown);break;
                case HeroId.Traveler: BuildTraveler(voidMetal,prestigeMetal,energy,hot,crown);break;
            }
        }

        // Veyr: twin eclipse blades with a broken celestial halo and floating phase shards.
        private void BuildVeyr(Material dark,Material metal,Material energy,Material hot,Material crown)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Transform weapon=FindRecursive(visualRoot,side<0?"WeaponL":"WeaponR");if(weapon==null)continue;
                Transform a=Anchor(weapon,"DF_Legendary_EclipseSeraph");
                Ring("SeraphHalo",a,new Vector3(side*.02f,-.39f,.49f),.36f,.032f,side<0?energy:crown,new Vector3(0,0,side*17f),side*22f);
                Ring("InnerPhaseHalo",a,new Vector3(side*.02f,-.39f,.505f),.27f,.018f,hot,new Vector3(0,0,-side*24f),-side*34f);
                Block("CrownGuard",a,new Vector3(side*.14f,-.10f,.11f),new Vector3(.38f,.050f,.13f),metal,new Vector3(0,0,side*30f));
                Block("BladeSpine",a,new Vector3(side*.17f,-.43f,.71f),new Vector3(.10f,.15f,.72f),dark,new Vector3(-7f,side*4f,side*10f));
                Block("PhaseEdge",a,new Vector3(side*.245f,-.46f,.72f),new Vector3(.027f,.085f,.78f),energy,new Vector3(-7f,side*4f,side*11f));
                Sphere("Heart",a,new Vector3(0,-.14f,.16f),new Vector3(.16f,.16f,.12f),hot,true);
                for(int i=0;i<3;i++)
                {
                    GameObject shard=Block("OrbitShard"+i,a,new Vector3(side*(.30f+.035f*i),-.22f-.15f*i,.42f+.29f*i),new Vector3(.055f,.16f,.040f),i==1?crown:energy,new Vector3(18f,side*(24f+i*11f),side*(38f+i*9f)));
                    Motion(shard,LegendaryWeaponMotion.Mode.Float,Vector3.up,1.4f+i*.18f,.045f,i*.72f);
                }
            }
        }

        // Kaelon: oversized melee world-crusher; no projectile hardware is introduced.
        private void BuildKaelon(Material dark,Material metal,Material energy,Material hot,Material crown)
        {
            Transform weapon=FindRecursive(visualRoot,"WeaponR");if(weapon==null)return;
            Transform a=Anchor(weapon,"DF_Legendary_Worldcrusher");
            Block("MassCore",a,new Vector3(0,.59f,.03f),new Vector3(1.02f,.60f,.63f),dark);
            for(int side=-1;side<=1;side+=2)
            {
                Block("StrikeFace"+side,a,new Vector3(side*.59f,.59f,.04f),new Vector3(.30f,.70f,.70f),metal,new Vector3(0,0,side*-8f));
                Block("ImpactRune"+side,a,new Vector3(side*.75f,.59f,.40f),new Vector3(.075f,.43f,.075f),hot,new Vector3(0,0,side*45f));
                Block("CrownFin"+side,a,new Vector3(side*.42f,.92f,.02f),new Vector3(.15f,.40f,.30f),crown,new Vector3(0,0,side*15f));
            }
            Ring("GravityCageA",a,new Vector3(0,.60f,.39f),.43f,.045f,energy,new Vector3(0,0,0),24f);
            Ring("GravityCageB",a,new Vector3(0,.60f,.42f),.31f,.026f,crown,new Vector3(0,0,0),-38f);
            Sphere("GravityHeart",a,new Vector3(0,.60f,.46f),new Vector3(.29f,.29f,.16f),hot,true);
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*.24f;
                GameObject shard=Block("GravityShard"+i,a,new Vector3(x,1.08f+.03f*(i%2),.04f),new Vector3(.10f,.24f,.16f),i%2==0?energy:crown,new Vector3(0,0,(i-1.5f)*15f));
                Motion(shard,LegendaryWeaponMotion.Mode.Float,Vector3.up,1.1f,.035f,i*.55f);
            }
        }

        // Nyra: star-piercer rail rifle with visibly separated energized rails and charge collars.
        private void BuildNyra(Material dark,Material metal,Material energy,Material hot,Material crown)
        {
            Transform weapon=FindRecursive(visualRoot,"WeaponR");if(weapon==null)return;
            Transform a=Anchor(weapon,"DF_Legendary_Starpiercer");
            Block("ReceiverArmor",a,new Vector3(0,.02f,.20f),new Vector3(.38f,.34f,.92f),dark);
            for(int side=-1;side<=1;side+=2)
            {
                Block("Rail"+side,a,new Vector3(side*.19f,.02f,.92f),new Vector3(.050f,.075f,1.58f),side<0?energy:crown);
                Block("RailFin"+side,a,new Vector3(side*.24f,.14f,.55f),new Vector3(.12f,.055f,.76f),metal,new Vector3(0,0,side*11f));
            }
            for(int i=0;i<4;i++)
            {
                float z=.42f+i*.30f;
                Ring("ChargeCollar"+i,a,new Vector3(0,.02f,z),.245f,.027f,i%2==0?energy:crown,Vector3.zero,i%2==0?42f:-34f);
            }
            Ring("MuzzleHalo",a,new Vector3(0,.02f,1.67f),.30f,.040f,hot,Vector3.zero,-48f);
            Sphere("Capacitor",a,new Vector3(0,-.22f,.15f),new Vector3(.22f,.25f,.20f),hot,true);
            Block("CrownOptic",a,new Vector3(0,.25f,.04f),new Vector3(.18f,.15f,.46f),metal);
            Sphere("OpticLens",a,new Vector3(0,.25f,.29f),new Vector3(.12f,.09f,.055f),energy,true);
        }

        // Elyra remains a staff: a long singularity staff crowned by three counter-rotating orbits.
        private void BuildElyra(Material dark,Material metal,Material energy,Material hot,Material crown)
        {
            Transform weapon=FindRecursive(visualRoot,"WeaponR");if(weapon==null)return;
            Transform a=Anchor(weapon,"DF_Legendary_SingularityStaff");
            Block("RoyalShaft",a,new Vector3(0,-.17f,0),new Vector3(.075f,1.95f,.075f),dark);
            Block("ShaftInlay",a,new Vector3(.055f,-.12f,.015f),new Vector3(.018f,1.60f,.018f),energy);
            Vector3 c=new Vector3(0,.48f,.10f);
            Ring("OrbitA",a,c,.49f,.036f,crown,new Vector3(67,0,24),22f);
            Ring("OrbitB",a,c,.37f,.026f,energy,new Vector3(20,61,-19),-31f);
            Ring("OrbitC",a,c,.25f,.020f,hot,new Vector3(44,-46,57),43f);
            Sphere("Singularity",a,c,new Vector3(.27f,.27f,.27f),dark,true);
            Sphere("EventGlow",a,c+new Vector3(0,0,.035f),new Vector3(.17f,.17f,.11f),hot,true);
            for(int side=-1;side<=1;side+=2)
            {
                Block("CrownProng"+side,a,new Vector3(side*.31f,.78f,.02f),new Vector3(.075f,.52f,.08f),metal,new Vector3(0,0,side*28f));
                Block("RuneTip"+side,a,new Vector3(side*.43f,1.00f,.02f),new Vector3(.10f,.19f,.09f),crown,new Vector3(0,0,side*38f));
            }
        }

        // Mira: ceremonial healing staff with a large radiant halo crown and floating light petals.
        private void BuildMira(Material dark,Material metal,Material energy,Material hot,Material crown)
        {
            Transform weapon=FindRecursive(visualRoot,"WeaponR");if(weapon==null)return;
            Transform a=Anchor(weapon,"DF_Legendary_AureliaStaff");
            Block("PearlShaft",a,new Vector3(0,.01f,0),new Vector3(.075f,1.92f,.075f),metal);
            Ring("SeraphHaloOuter",a,new Vector3(0,.94f,.06f),.44f,.040f,crown,new Vector3(78,0,0),18f);
            Ring("SeraphHaloInner",a,new Vector3(0,.94f,.08f),.31f,.023f,energy,new Vector3(78,0,0),-30f);
            Sphere("HealingSun",a,new Vector3(0,.94f,.13f),new Vector3(.24f,.24f,.14f),hot,true);
            for(int i=0;i<6;i++)
            {
                float ang=i*60f*Mathf.Deg2Rad;
                Vector3 p=new Vector3(Mathf.Cos(ang)*.47f,.94f+Mathf.Sin(ang)*.47f,.06f);
                GameObject petal=Block("LightPetal"+i,a,p,new Vector3(.09f,.25f,.055f),i%2==0?energy:crown,new Vector3(0,0,-i*60f));
                Motion(petal,LegendaryWeaponMotion.Mode.Pulse,Vector3.one,1.2f,.08f,i*.42f);
            }
            Block("HaloCollar",a,new Vector3(0,.53f,.02f),new Vector3(.34f,.16f,.18f),dark);
        }

        // Rook: powered melee gauntlets with reactor knuckles and floating pressure crowns.
        private void BuildRook(Material dark,Material metal,Material energy,Material hot,Material crown)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Transform weapon=FindRecursive(visualRoot,side<0?"GauntletL":"GauntletR");if(weapon==null)continue;
                Transform a=Anchor(weapon,"DF_Legendary_Titanforge");
                Block("FistArmor",a,new Vector3(0,-.02f,.14f),new Vector3(.47f,.48f,.48f),dark);
                Block("KnucklePlate",a,new Vector3(0,-.13f,.42f),new Vector3(.44f,.22f,.16f),metal);
                Ring("ReactorRing",a,new Vector3(0,.10f,.38f),.24f,.030f,side<0?energy:crown,Vector3.zero,side*38f);
                Sphere("Reactor",a,new Vector3(0,.10f,.40f),new Vector3(.18f,.15f,.10f),hot,true);
                for(int finger=0;finger<4;finger++)
                {
                    float x=(finger-1.5f)*.105f;
                    Block("PowerKnuckle"+finger,a,new Vector3(x,-.18f,.51f),new Vector3(.085f,.17f,.13f),finger%2==0?crown:energy);
                }
                for(int rail=-1;rail<=1;rail+=2)
                    Block("PressureRail"+rail,a,new Vector3(rail*.18f,.02f,.30f),new Vector3(.035f,.23f,.035f),energy,new Vector3(0,0,rail*8f));
                GameObject crownShard=Block("FloatingCrown",a,new Vector3(0,.34f,.30f),new Vector3(.22f,.08f,.12f),crown,new Vector3(0,0,side*12f));
                Motion(crownShard,LegendaryWeaponMotion.Mode.Float,Vector3.up,1.4f,.040f,side<0?0f:.8f);
            }
        }

        // Traveler: a royal chronal staff built around an hourglass core and opposing clock rings.
        private void BuildTraveler(Material dark,Material metal,Material energy,Material hot,Material crown)
        {
            Transform weapon=FindRecursive(visualRoot,"WeaponR");if(weapon==null)return;
            Transform a=Anchor(weapon,"DF_Legendary_LastHour");
            Block("ChronalShaft",a,new Vector3(0,-.05f,0),new Vector3(.075f,1.90f,.075f),dark);
            Vector3 c=new Vector3(0,.63f,.11f);
            Ring("ClockOuter",a,c,.48f,.040f,crown,new Vector3(68,0,23),20f);
            Ring("ClockMiddle",a,c,.35f,.027f,energy,new Vector3(22,64,-18),-29f);
            Ring("ClockInner",a,c,.23f,.018f,hot,new Vector3(44,-40,54),41f);
            Sphere("ChronalCore",a,c,new Vector3(.18f,.24f,.14f),hot,true);
            Block("HourglassUpper",a,c+new Vector3(0,.19f,0),new Vector3(.17f,.24f,.11f),metal,new Vector3(0,0,45f));
            Block("HourglassLower",a,c+new Vector3(0,-.19f,0),new Vector3(.17f,.24f,.11f),metal,new Vector3(0,0,45f));
            for(int side=-1;side<=1;side+=2)
            {
                GameObject satellite=Block("TimeShard"+side,a,c+new Vector3(side*.49f,side*.04f,.02f),new Vector3(.09f,.25f,.08f),side<0?energy:crown,new Vector3(0,0,side*32f));
                Motion(satellite,LegendaryWeaponMotion.Mode.Float,Vector3.up,1.0f,.045f,side<0?.2f:.9f);
            }
            Block("RoyalFoot",a,new Vector3(0,-.96f,.02f),new Vector3(.24f,.15f,.20f),crown);
        }

        private Transform Anchor(Transform parent,string name)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(parent,false);anchors.Add(go);return go.transform;
        }

        private GameObject Block(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,Vector3 euler=default)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="DF_Legendary_"+name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);go.transform.localScale=scale;
            MeshRenderer r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);return go;
        }

        private GameObject Sphere(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,bool pulse=false)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="DF_Legendary_"+name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            MeshRenderer r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);
            if(pulse)Motion(go,LegendaryWeaponMotion.Mode.Pulse,Vector3.one,1.7f,.10f,.2f);return go;
        }

        private GameObject Ring(string name,Transform parent,Vector3 pos,float radius,float width,Material material,Vector3 euler,float speed)
        {
            GameObject go=new GameObject("DF_Legendary_"+name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);
            Mesh mesh=RingMesh(radius,width,32);meshes.Add(mesh);MeshFilter f=go.AddComponent<MeshFilter>();f.sharedMesh=mesh;MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            Motion(go,LegendaryWeaponMotion.Mode.Rotate,Vector3.forward,speed,0,0);return go;
        }

        private Mesh RingMesh(float radius,float width,int segments)
        {
            float inner=Mathf.Max(.001f,radius-width*.5f),outer=radius+width*.5f;
            Vector3[] v=new Vector3[segments*2];int[] t=new int[segments*12];
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2f/segments,c=Mathf.Cos(a),s=Mathf.Sin(a);v[i*2]=new Vector3(c*inner,s*inner,0);v[i*2+1]=new Vector3(c*outer,s*outer,0);
                int n=(i+1)%segments,a0=i*2,a1=i*2+1,b0=n*2,b1=n*2+1,o=i*12;
                t[o]=a0;t[o+1]=a1;t[o+2]=b1;t[o+3]=a0;t[o+4]=b1;t[o+5]=b0;
                t[o+6]=a0;t[o+7]=b1;t[o+8]=a1;t[o+9]=a0;t[o+10]=b0;t[o+11]=b1;
            }
            Mesh mesh=new Mesh{name="DF_LegendaryPrestigeRing"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private Material Mat(string label,Color color,float metallic,float smooth,bool emissive,float emission)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new Material(shader){name="DF_Legendary_"+label,color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);
            if(emissive){m.EnableKeyword("_EMISSION");if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",color*emission);}materials.Add(m);return m;
        }

        private void Motion(GameObject go,LegendaryWeaponMotion.Mode mode,Vector3 axis,float speed,float amount,float phase)
        {
            LegendaryWeaponMotion motion=go.AddComponent<LegendaryWeaponMotion>();motion.Configure(mode,axis,speed,amount,phase);
        }

        private void Clear()
        {
            for(int i=0;i<anchors.Count;i++)if(anchors[i]!=null)Destroy(anchors[i]);anchors.Clear();
            for(int i=0;i<materials.Count;i++)if(materials[i]!=null)Destroy(materials[i]);materials.Clear();
            for(int i=0;i<meshes.Count;i++)if(meshes[i]!=null)Destroy(meshes[i]);meshes.Clear();
            built=false;identity=null;visualRoot=null;
        }

        private static Transform FindRecursive(Transform root,string name)
        {
            if(root==null)return null;if(root.name==name||root.name=="DF_"+name)return root;
            for(int i=0;i<root.childCount;i++){Transform found=FindRecursive(root.GetChild(i),name);if(found!=null)return found;}return null;
        }
    }

    internal sealed class LegendaryWeaponMotion : MonoBehaviour
    {
        internal enum Mode { Rotate, Float, Pulse }
        private Mode mode;private Vector3 axis;private float speed,amount,phase;private Vector3 basePos,baseScale;private Quaternion baseRot;private bool ready;

        internal void Configure(Mode next,Vector3 nextAxis,float nextSpeed,float nextAmount,float nextPhase)
        {
            mode=next;axis=nextAxis.sqrMagnitude>.001f?nextAxis.normalized:Vector3.up;speed=nextSpeed;amount=nextAmount;phase=nextPhase;
            basePos=transform.localPosition;baseScale=transform.localScale;baseRot=transform.localRotation;ready=true;
        }

        private void LateUpdate()
        {
            if(!ready)return;float t=Time.unscaledTime;
            if(mode==Mode.Rotate)transform.localRotation=baseRot*Quaternion.AngleAxis(t*speed+phase*90f,axis);
            else if(mode==Mode.Float)transform.localPosition=basePos+axis*(Mathf.Sin(t*speed+phase*Mathf.PI*2f)*amount);
            else if(mode==Mode.Pulse){float p=1f+Mathf.Sin(t*speed+phase*Mathf.PI*2f)*amount;transform.localScale=baseScale*p;}
        }
    }
}
