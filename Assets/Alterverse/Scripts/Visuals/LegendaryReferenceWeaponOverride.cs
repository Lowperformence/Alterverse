using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Final reference-driven replacement for the approved 1100-shard LEGENDARY weapons.
    /// It runs after the normal signature/prestige passes, hides their renderers only on the
    /// affected weapon pivots, and builds the approved silhouette directly on the same gameplay
    /// pivots. Combat, hit timing, muzzle transforms, damage and animation bindings remain untouched.
    /// </summary>
    [DefaultExecutionOrder(2900)]
    internal sealed class LegendaryReferenceWeaponBootstrap : MonoBehaviour
    {
        private static LegendaryReferenceWeaponBootstrap instance;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_LegendaryReferenceWeaponBootstrap");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<LegendaryReferenceWeaponBootstrap>();
        }

        private void LateUpdate()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.18f;
            HeroIdentity[] heroes=FindObjectsByType<HeroIdentity>(FindObjectsSortMode.None);
            for(int i=0;i<heroes.Length;i++)Sync(heroes[i]);
        }

        private static bool Supported(HeroId hero)
        {
            return hero==HeroId.Veyr||hero==HeroId.Elyra||hero==HeroId.Mira||hero==HeroId.Traveler;
        }

        private static void Sync(HeroIdentity identity)
        {
            if(identity==null)return;
            SkinPreviewSelection preview=identity.GetComponent<SkinPreviewSelection>();
            int variant=preview!=null?preview.WeaponSkinVariant:
                identity.GetComponent<HeroRespawn>()!=null?WeaponSkinInventory.EquippedVariant(identity.Hero):-1;

            LegendaryReferenceWeaponPresentation pass=identity.GetComponent<LegendaryReferenceWeaponPresentation>();
            if(variant!=1||!Supported(identity.Hero))
            {
                if(pass!=null)pass.Remove();
                return;
            }
            if(pass==null)pass=identity.gameObject.AddComponent<LegendaryReferenceWeaponPresentation>();
            pass.Show(identity);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class LegendaryReferenceWeaponPresentation : MonoBehaviour
    {
        private sealed class RendererState
        {
            public Renderer Renderer;
            public bool Enabled;
        }

        private readonly List<RendererState> hidden=new();
        private readonly List<GameObject> roots=new();
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
            switch(identity.Hero)
            {
                case HeroId.Veyr: BuildAssassinReference();break;
                case HeroId.Elyra: BuildMageReference();break;
                case HeroId.Mira: BuildHealerReference();break;
                case HeroId.Traveler: BuildTravelerReference();break;
            }
        }

        // Approved reference: twin black jagged swords with aggressive angular guards,
        // violet crystal hearts and lightning-like emissive channels across the blades.
        private void BuildAssassinReference()
        {
            Material black=Mat("AssassinBlack",new Color(.010f,.012f,.020f),.96f,.90f,false,1f);
            Material steel=Mat("AssassinSteel",new Color(.16f,.18f,.24f),.94f,.86f,false,1f);
            Material violet=Mat("AssassinViolet",new Color(.48f,.04f,1f),.10f,.98f,true,7.5f);
            Material hot=Mat("AssassinHot",new Color(.86f,.46f,1f),.03f,1f,true,10.5f);

            for(int side=-1;side<=1;side+=2)
            {
                Transform pivot=PreparePivot(side<0?"WeaponL":"WeaponR");if(pivot==null)continue;
                Transform root=Root(pivot,"DF_ApprovedLegendary_Assassin_"+(side<0?"L":"R"));

                Block("Grip",root,new Vector3(0,.01f,-.08f),new Vector3(.105f,.31f,.105f),black);
                for(int band=0;band<4;band++)
                    Block("GripBand"+band,root,new Vector3(0,-.10f+band*.065f,-.075f),new Vector3(.125f,.018f,.125f),band%2==0?violet:steel,new Vector3(0,0,side*(band%2==0?8f:-8f)));

                // Wide multi-pronged guard from the approved sword reference.
                Beam("GuardOuterA",root,new Vector3(-.03f,-.10f,.10f),new Vector3(-.34f,.00f,.20f),.070f,black);
                Beam("GuardOuterB",root,new Vector3(.03f,-.10f,.10f),new Vector3(.34f,.00f,.20f),.070f,black);
                Beam("GuardHookA",root,new Vector3(-.17f,-.02f,.16f),new Vector3(-.27f,-.18f,.27f),.060f,steel);
                Beam("GuardHookB",root,new Vector3(.17f,-.02f,.16f),new Vector3(.27f,-.18f,.27f),.060f,steel);
                Beam("GuardRuneA",root,new Vector3(-.05f,-.08f,.12f),new Vector3(-.29f,-.02f,.20f),.020f,violet);
                Beam("GuardRuneB",root,new Vector3(.05f,-.08f,.12f),new Vector3(.29f,-.02f,.20f),.020f,violet);

                JaggedBlade("ObsidianBlade",root,side,1.34f,.058f,black);
                JaggedBlade("SteelFacet",root,side,1.28f,.032f,steel,.72f);

                // Lightning seam: an unmistakable purple zig-zag from hilt to tip.
                Vector3[] rune={
                    new Vector3(0,-.18f,.23f),
                    new Vector3(side*.075f,-.23f,.45f),
                    new Vector3(-side*.045f,-.28f,.65f),
                    new Vector3(side*.085f,-.33f,.86f),
                    new Vector3(-side*.030f,-.37f,1.08f),
                    new Vector3(0,-.41f,1.29f)};
                for(int i=0;i<rune.Length-1;i++)Beam("Lightning"+i,root,rune[i],rune[i+1],i==2?.034f:.026f,i==2?hot:violet);
                Beam("LightningBranchA",root,new Vector3(side*.075f,-.23f,.45f),new Vector3(side*.16f,-.22f,.57f),.020f,violet);
                Beam("LightningBranchB",root,new Vector3(side*.085f,-.33f,.86f),new Vector3(side*.17f,-.32f,.96f),.018f,hot);

                Crystal("GuardCrystal",root,new Vector3(0,-.11f,.17f),.105f,.22f,hot,true);
                Crystal("PommelCrystal",root,new Vector3(0,.22f,-.08f),.070f,.14f,violet,true);
                Ring("GuardAura",root,new Vector3(0,-.11f,.18f),.16f,.018f,violet,new Vector3(0,0,0),side*16f);
            }
        }

        // Approved mage reference: long ornate dark-bronze staff, huge faceted purple crystal,
        // forked crown and smaller embedded crystals. The crystal is the brightest legendary element.
        private void BuildMageReference()
        {
            Transform pivot=PreparePivot("WeaponR");if(pivot==null)return;
            Transform root=Root(pivot,"DF_ApprovedLegendary_MageCrystalStaff");

            Material bronze=Mat("MageBronze",new Color(.18f,.105f,.065f),.88f,.78f,false,1f);
            Material bronzeEdge=Mat("MageBronzeEdge",new Color(.42f,.25f,.13f),.94f,.90f,false,1f);
            Material violet=Mat("MageAmethyst",new Color(.46f,.04f,1f),.08f,.99f,true,9.0f);
            Material hot=Mat("MageAmethystHot",new Color(.82f,.38f,1f),.02f,1f,true,13.0f);

            Cylinder("Shaft",root,new Vector3(0,-.12f,0),new Vector3(.095f,.94f,.095f),bronze);
            Cylinder("LowerShaft",root,new Vector3(0,-.82f,0),new Vector3(.115f,.32f,.115f),bronzeEdge);
            for(int i=0;i<8;i++)
            {
                float y=-.77f+i*.19f;
                Block("OrnateBand"+i,root,new Vector3(0,y,.005f),new Vector3(.14f,.030f,.14f),i%3==0?bronzeEdge:bronze,new Vector3(0,0,i%2==0?14f:-14f));
            }

            // Crown forks rise around the crystal just like the approved reference.
            Beam("ForkL1",root,new Vector3(-.08f,.58f,0),new Vector3(-.31f,.86f,.01f),.075f,bronzeEdge);
            Beam("ForkL2",root,new Vector3(-.31f,.86f,.01f),new Vector3(-.22f,1.24f,.01f),.072f,bronzeEdge);
            Beam("ForkR1",root,new Vector3(.08f,.58f,0),new Vector3(.31f,.86f,.01f),.075f,bronzeEdge);
            Beam("ForkR2",root,new Vector3(.31f,.86f,.01f),new Vector3(.22f,1.24f,.01f),.072f,bronzeEdge);
            Beam("InnerForkL",root,new Vector3(-.05f,.67f,.02f),new Vector3(-.17f,1.02f,.03f),.035f,violet);
            Beam("InnerForkR",root,new Vector3(.05f,.67f,.02f),new Vector3(.17f,1.02f,.03f),.035f,violet);

            Crystal("CrownCrystal",root,new Vector3(0,1.12f,.035f),.20f,.72f,hot,true);
            Crystal("HeartCrystal",root,new Vector3(0,.64f,.08f),.090f,.20f,violet,true);
            Crystal("MidCrystal",root,new Vector3(0,-.23f,.07f),.070f,.18f,violet,true);
            Crystal("FootCrystal",root,new Vector3(0,-1.11f,.03f),.085f,.20f,hot,true);
            Ring("CrystalHalo",root,new Vector3(0,1.12f,.04f),.31f,.018f,violet,new Vector3(68,0,0),18f);
        }

        // Approved healer reference: crooked natural wood, pale wrap grip and a large blue crescent.
        private void BuildHealerReference()
        {
            Transform pivot=PreparePivot("WeaponR");if(pivot==null)return;
            Transform root=Root(pivot,"DF_ApprovedLegendary_HealerMoonroot");

            Material wood=Mat("HealerWood",new Color(.20f,.105f,.050f),.08f,.36f,false,1f);
            Material woodLight=Mat("HealerWoodLight",new Color(.34f,.19f,.09f),.10f,.42f,false,1f);
            Material wrap=Mat("HealerWrap",new Color(.72f,.64f,.49f),.02f,.35f,false,1f);
            Material moon=Mat("HealerMoon",new Color(.03f,.60f,1f),.05f,.96f,true,8.6f);
            Material moonHot=Mat("HealerMoonHot",new Color(.30f,.88f,1f),.02f,1f,true,12.0f);

            Vector3[] woodPath={
                new Vector3(.09f,-1.09f,0),new Vector3(-.02f,-.76f,.01f),new Vector3(-.10f,-.47f,.02f),
                new Vector3(.03f,-.18f,.025f),new Vector3(.10f,.10f,.02f),new Vector3(-.03f,.37f,.015f),
                new Vector3(-.11f,.62f,.01f),new Vector3(-.18f,.78f,.01f)};
            for(int i=0;i<woodPath.Length-1;i++)Beam("LivingWood"+i,root,woodPath[i],woodPath[i+1],i<2?.13f:.15f,i%2==0?wood:woodLight);

            // Wrapped hand section from the reference image.
            for(int i=0;i<9;i++)
            {
                float y=-.38f+i*.065f;
                Block("Wrap"+i,root,new Vector3(.02f,y,.03f),new Vector3(.18f,.050f,.17f),wrap,new Vector3(0,0,i%2==0?12f:-10f));
            }

            // Large open blue crescent, glowing strongly enough to read in gameplay.
            GameObject crescent=Arc("MoonCrescent",root,new Vector3(-.04f,.99f,.025f),.43f,.115f,moon,55f,305f,new Vector3(0,0,-8f));
            Animate(crescent,LegendaryWeaponMotion.Mode.Pulse,Vector3.one,1.1f,.035f,.2f);
            Arc("MoonInnerGlow",root,new Vector3(-.04f,.99f,.035f),.335f,.034f,moonHot,58f,302f,new Vector3(0,0,-8f));
            Beam("MoonBranchL",root,new Vector3(-.18f,.77f,.01f),new Vector3(-.31f,.84f,.02f),.070f,woodLight);
            Beam("MoonBranchR",root,new Vector3(-.15f,.77f,.01f),new Vector3(.05f,.82f,.02f),.065f,woodLight);
            Sphere("MoonSeed",root,new Vector3(-.04f,.99f,.07f),new Vector3(.105f,.105f,.065f),moonHot,true);
            for(int i=0;i<3;i++)Sphere("HealingRune"+i,root,new Vector3(.04f,-.22f+i*.18f,.11f),new Vector3(.035f,.035f,.022f),moon,i==1);
        }

        // Approved Traveler reference: a Saturn-like world as the unmistakable weapon focus.
        private void BuildTravelerReference()
        {
            Transform pivot=PreparePivot("WeaponR");if(pivot==null)return;
            Transform root=Root(pivot,"DF_ApprovedLegendary_TravelerSaturn");

            Material dark=Mat("TravelerDark",new Color(.055f,.045f,.035f),.88f,.83f,false,1f);
            Material gold=Mat("TravelerGold",new Color(.62f,.43f,.16f),.94f,.91f,true,2.5f);
            Material planet=Mat("TravelerPlanet",new Color(.88f,.61f,.27f),.28f,.86f,true,4.8f);
            Material cream=Mat("TravelerCream",new Color(1f,.87f,.60f),.12f,.94f,true,6.2f);
            Material white=Mat("TravelerRing",new Color(.90f,.94f,1f),.36f,.98f,true,8.4f);

            Cylinder("SaturnShaft",root,new Vector3(0,-.29f,0),new Vector3(.085f,.72f,.085f),dark);
            Block("SaturnGrip",root,new Vector3(0,-.62f,0),new Vector3(.15f,.42f,.15f),gold);
            Vector3 c=new Vector3(0,.73f,.07f);
            Sphere("SaturnWorld",root,c,new Vector3(.40f,.40f,.40f),planet,true);

            // Bright atmospheric banding over the planet.
            Ring("PlanetBandA",root,c+new Vector3(0,-.15f,0),.30f,.025f,cream,new Vector3(90,0,0),2f);
            Ring("PlanetBandB",root,c+new Vector3(0,-.06f,0),.36f,.030f,gold,new Vector3(90,0,0),-2f);
            Ring("PlanetBandC",root,c+new Vector3(0,.05f,0),.37f,.028f,cream,new Vector3(90,0,0),2.5f);
            Ring("PlanetBandD",root,c+new Vector3(0,.15f,0),.30f,.024f,gold,new Vector3(90,0,0),-2.5f);

            // Wide Saturn ring from the supplied reference, with a bright inner line.
            Ring("SaturnRing",root,c,.66f,.135f,white,new Vector3(67,0,7),5f);
            Ring("SaturnRingInner",root,c+new Vector3(0,0,.006f),.50f,.030f,gold,new Vector3(67,0,7),-7f);
            Ring("SaturnRingOuter",root,c+new Vector3(0,0,.012f),.76f,.020f,cream,new Vector3(67,0,7),8f);
            for(int i=0;i<4;i++)
            {
                float a=(45f+i*90f)*Mathf.Deg2Rad;
                GameObject star=Sphere("OrbitStar"+i,root,c+new Vector3(Mathf.Cos(a)*.78f,Mathf.Sin(a)*.18f,.04f),new Vector3(.045f,.045f,.045f),i%2==0?white:cream,true);
                Animate(star,LegendaryWeaponMotion.Mode.Float,Vector3.up,1.0f+i*.10f,.025f,i*.25f);
            }
        }

        private Transform PreparePivot(string name)
        {
            Transform pivot=FindRecursive(visualRoot,name);if(pivot==null)return null;
            Renderer[] renderers=pivot.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer r=renderers[i];
                hidden.Add(new RendererState{Renderer=r,Enabled=r.enabled});
                r.enabled=false;
            }
            return pivot;
        }

        private Transform Root(Transform pivot,string name)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(pivot,false);roots.Add(go);return go.transform;
        }

        private GameObject Block(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,Vector3 euler=default)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="DF_RefLegendary_"+name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);return go;
        }

        private GameObject Cylinder(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,Vector3 euler=default)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="DF_RefLegendary_"+name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);return go;
        }

        private GameObject Sphere(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,bool pulse=false)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="DF_RefLegendary_"+name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);
            if(pulse)Animate(go,LegendaryWeaponMotion.Mode.Pulse,Vector3.one,1.5f,.055f,.2f);return go;
        }

        private GameObject Beam(string name,Transform parent,Vector3 from,Vector3 to,float thickness,Material material)
        {
            Vector3 d=to-from;float length=d.magnitude;
            GameObject go=Block(name,parent,(from+to)*.5f,new Vector3(thickness,length,thickness),material);
            if(length>.0001f)go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,d/length);
            return go;
        }

        private GameObject Ring(string name,Transform parent,Vector3 pos,float radius,float width,Material material,Vector3 euler,float speed)
        {
            GameObject go=MeshObject("DF_RefLegendary_"+name,parent,RingMesh(radius,width,40),material);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);
            if(Mathf.Abs(speed)>.01f)Animate(go,LegendaryWeaponMotion.Mode.Rotate,Vector3.forward,speed,0,0);return go;
        }

        private GameObject Arc(string name,Transform parent,Vector3 pos,float radius,float width,Material material,float start,float end,Vector3 euler)
        {
            GameObject go=MeshObject("DF_RefLegendary_"+name,parent,ArcMesh(radius,width,start,end,34),material);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);return go;
        }

        private GameObject Crystal(string name,Transform parent,Vector3 pos,float radius,float height,Material material,bool pulse)
        {
            Mesh mesh=CrystalMesh(radius,height);GameObject go=MeshObject("DF_RefLegendary_"+name,parent,mesh,material);go.transform.localPosition=pos;
            if(pulse)Animate(go,LegendaryWeaponMotion.Mode.Pulse,Vector3.one,1.55f,.045f,.1f);return go;
        }

        private GameObject JaggedBlade(string name,Transform parent,int side,float length,float thickness,Material material,float widthScale=1f)
        {
            Mesh mesh=JaggedBladeMesh(side,length,thickness,widthScale);GameObject go=MeshObject("DF_RefLegendary_"+name,parent,mesh,material);return go;
        }

        private GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            meshes.Add(mesh);GameObject go=new GameObject(name);go.transform.SetParent(parent,false);MeshFilter f=go.AddComponent<MeshFilter>();f.sharedMesh=mesh;MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;return go;
        }

        private Mesh JaggedBladeMesh(int side,float length,float thickness,float widthScale)
        {
            float[] widths={.13f,.22f,.12f,.20f,.11f,.18f,.095f,.055f,.001f};int n=widths.Length;
            Vector3[] v=new Vector3[n*4];
            for(int i=0;i<n;i++)
            {
                float t=i/(float)(n-1);float z=.18f+t*length;float cy=-.17f-.25f*t;float cx=side*(.018f*Mathf.Sin(t*Mathf.PI*2f));float w=widths[i]*widthScale;
                v[i*4]=new Vector3(cx-w,cy-thickness*.5f,z);v[i*4+1]=new Vector3(cx+w,cy-thickness*.5f,z);
                v[i*4+2]=new Vector3(cx-w,cy+thickness*.5f,z);v[i*4+3]=new Vector3(cx+w,cy+thickness*.5f,z);
            }
            List<int> tri=new();
            for(int i=0;i<n-1;i++)
            {
                int a=i*4,b=(i+1)*4;
                AddQuad(tri,a,a+1,b+1,b);AddQuad(tri,a+2,b+2,b+3,a+3);
                AddQuad(tri,a,b,b+2,a+2);AddQuad(tri,a+1,a+3,b+3,b+1);
            }
            AddQuad(tri,0,2,3,1);int e=(n-1)*4;AddQuad(tri,e,e+1,e+3,e+2);
            Mesh mesh=new Mesh{name="DF_ApprovedJaggedBlade"};mesh.vertices=v;mesh.triangles=tri.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static void AddQuad(List<int> t,int a,int b,int c,int d)
        {
            t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d);
        }

        private Mesh RingMesh(float radius,float width,int segments)
        {
            float inner=Mathf.Max(.001f,radius-width*.5f),outer=radius+width*.5f;Vector3[] v=new Vector3[segments*2];int[] t=new int[segments*12];
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2f/segments,c=Mathf.Cos(a),s=Mathf.Sin(a);v[i*2]=new Vector3(c*inner,s*inner,0);v[i*2+1]=new Vector3(c*outer,s*outer,0);
                int n=(i+1)%segments,a0=i*2,a1=i*2+1,b0=n*2,b1=n*2+1,o=i*12;
                t[o]=a0;t[o+1]=a1;t[o+2]=b1;t[o+3]=a0;t[o+4]=b1;t[o+5]=b0;t[o+6]=a0;t[o+7]=b1;t[o+8]=a1;t[o+9]=a0;t[o+10]=b0;t[o+11]=b1;
            }
            Mesh mesh=new Mesh{name="DF_ApprovedRing"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private Mesh ArcMesh(float radius,float width,float startDeg,float endDeg,int segments)
        {
            float inner=Mathf.Max(.001f,radius-width*.5f),outer=radius+width*.5f;Vector3[] v=new Vector3[(segments+1)*2];int[] t=new int[segments*12];
            for(int i=0;i<=segments;i++)
            {
                float p=i/(float)segments;float a=Mathf.Lerp(startDeg,endDeg,p)*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);v[i*2]=new Vector3(c*inner,s*inner,0);v[i*2+1]=new Vector3(c*outer,s*outer,0);
                if(i==segments)continue;int a0=i*2,a1=i*2+1,b0=(i+1)*2,b1=(i+1)*2+1,o=i*12;
                t[o]=a0;t[o+1]=a1;t[o+2]=b1;t[o+3]=a0;t[o+4]=b1;t[o+5]=b0;t[o+6]=a0;t[o+7]=b1;t[o+8]=a1;t[o+9]=a0;t[o+10]=b0;t[o+11]=b1;
            }
            Mesh mesh=new Mesh{name="DF_ApprovedArc"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private Mesh CrystalMesh(float radius,float height)
        {
            Vector3 top=new Vector3(0,height*.5f,0),bottom=new Vector3(0,-height*.5f,0);
            Vector3[] v={top,new Vector3(radius,0,0),new Vector3(0,0,radius),new Vector3(-radius,0,0),new Vector3(0,0,-radius),bottom};
            int[] t={0,1,2,0,2,3,0,3,4,0,4,1,5,2,1,5,3,2,5,4,3,5,1,4};
            Mesh mesh=new Mesh{name="DF_ApprovedCrystal"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private Material Mat(string label,Color color,float metallic,float smooth,bool emissive,float emission)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new Material(shader){name="DF_ApprovedLegendary_"+label,color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);
            if(emissive){m.EnableKeyword("_EMISSION");if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",color*emission);}materials.Add(m);return m;
        }

        private void Animate(GameObject go,LegendaryWeaponMotion.Mode mode,Vector3 axis,float speed,float amount,float phase)
        {
            LegendaryWeaponMotion motion=go.AddComponent<LegendaryWeaponMotion>();motion.Configure(mode,axis,speed,amount,phase);
        }

        private void Clear()
        {
            for(int i=0;i<roots.Count;i++)if(roots[i]!=null)Destroy(roots[i]);roots.Clear();
            for(int i=0;i<hidden.Count;i++)if(hidden[i].Renderer!=null)hidden[i].Renderer.enabled=hidden[i].Enabled;hidden.Clear();
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
}
