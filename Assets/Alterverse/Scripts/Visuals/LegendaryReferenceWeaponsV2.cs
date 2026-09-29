using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Reference-inspired legendary weapon rebuild for the 1100-shard variant.
    /// Builds first, then hides only the older cosmetic layers. Gameplay weapon pivots,
    /// hit logic, damage, muzzle markers and animation bindings stay untouched.
    /// </summary>
    [DefaultExecutionOrder(3100)]
    internal sealed class LegendaryReferenceWeaponsV2Bootstrap : MonoBehaviour
    {
        private static LegendaryReferenceWeaponsV2Bootstrap instance;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_LegendaryReferenceWeaponsV2");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<LegendaryReferenceWeaponsV2Bootstrap>();
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

            LegendaryReferenceWeaponsV2Presentation pass=identity.GetComponent<LegendaryReferenceWeaponsV2Presentation>();
            if(variant!=1||!Supported(identity.Hero))
            {
                if(pass!=null)pass.Remove();
                return;
            }
            if(pass==null)pass=identity.gameObject.AddComponent<LegendaryReferenceWeaponsV2Presentation>();
            pass.Show(identity);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class LegendaryReferenceWeaponsV2Presentation : MonoBehaviour
    {
        private sealed class RendererState { public Renderer Renderer; public bool Enabled; }

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
            identity=hero;visualRoot=nextRoot;
            Build();
            if(roots.Count>0)
            {
                HideOlderCosmetics();
                built=true;
            }
        }

        public void Remove(){Clear();Destroy(this);}
        private void OnDestroy()=>Clear();

        private void Build()
        {
            switch(identity.Hero)
            {
                case HeroId.Veyr: BuildVeyrTwinBlades();break;
                case HeroId.Elyra: BuildElyraAmethystStaff();break;
                case HeroId.Mira: BuildMiraMoonStaff();break;
                case HeroId.Traveler: BuildTravelerSaturnStaff();break;
            }
        }

        // ASSASSIN reference: twin black fantasy swords, broad jagged silhouette,
        // purple lightning veins, crystal heart and aggressive branching guard.
        private void BuildVeyrTwinBlades()
        {
            Material obsidian=Mat("VeyrObsidian",new Color(.006f,.008f,.015f),.97f,.91f,false,1f);
            Material edge=Mat("VeyrEdge",new Color(.16f,.18f,.24f),.96f,.90f,false,1f);
            Material violet=Mat("VeyrViolet",new Color(.48f,.025f,1f),.04f,.99f,true,8.5f);
            Material hot=Mat("VeyrHot",new Color(.88f,.40f,1f),.01f,1f,true,14f);

            for(int side=-1;side<=1;side+=2)
            {
                Transform pivot=FindRecursive(visualRoot,side<0?"WeaponL":"WeaponR");
                if(pivot==null)continue;
                Transform r=Root(pivot,"DF_LegendaryV2_Veyr_"+(side<0?"L":"R"));

                Block("Grip",r,new Vector3(0,.02f,-.08f),new Vector3(.10f,.32f,.10f),obsidian);
                for(int i=0;i<5;i++)
                    Block("GripBand"+i,r,new Vector3(0,-.11f+i*.060f,-.075f),new Vector3(.125f,.016f,.125f),i%2==0?violet:edge,new Vector3(0,0,side*(i%2==0?10f:-10f)));

                Beam("GuardWingL",r,new Vector3(-.03f,-.10f,.10f),new Vector3(-.39f,.02f,.23f),.075f,obsidian);
                Beam("GuardWingR",r,new Vector3(.03f,-.10f,.10f),new Vector3(.39f,.02f,.23f),.075f,obsidian);
                Beam("GuardHookL",r,new Vector3(-.23f,.00f,.18f),new Vector3(-.34f,-.19f,.30f),.058f,edge);
                Beam("GuardHookR",r,new Vector3(.23f,.00f,.18f),new Vector3(.34f,-.19f,.30f),.058f,edge);
                Beam("GuardGlowL",r,new Vector3(-.06f,-.08f,.13f),new Vector3(-.34f,.01f,.22f),.020f,violet);
                Beam("GuardGlowR",r,new Vector3(.06f,-.08f,.13f),new Vector3(.34f,.01f,.22f),.020f,violet);

                MeshObject("Blade",r,JaggedSwordMesh(side,1.45f,.070f,1f),obsidian);
                GameObject facet=MeshObject("BladeFacet",r,JaggedSwordMesh(side,1.38f,.040f,.72f),edge);
                facet.transform.localPosition=new Vector3(0,0,.005f);

                Vector3[] lightning={
                    new(0,-.17f,.23f),new(side*.08f,-.22f,.43f),new(-side*.05f,-.26f,.62f),
                    new(side*.10f,-.30f,.83f),new(-side*.04f,-.34f,1.04f),new(side*.06f,-.37f,1.24f),new(0,-.40f,1.43f)};
                for(int i=0;i<lightning.Length-1;i++)Beam("Lightning"+i,r,lightning[i],lightning[i+1],i==2||i==4?.032f:.024f,i==2||i==4?hot:violet);
                Beam("BranchA",r,lightning[1],lightning[1]+new Vector3(side*.15f,.02f,.10f),.018f,violet);
                Beam("BranchB",r,lightning[3],lightning[3]+new Vector3(side*.16f,.02f,.11f),.017f,hot);
                Beam("BranchC",r,lightning[4],lightning[4]+new Vector3(-side*.13f,.015f,.09f),.016f,violet);

                Crystal("GuardCrystal",r,new Vector3(0,-.105f,.17f),.115f,.24f,hot,true);
                Crystal("PommelCrystal",r,new Vector3(0,.22f,-.08f),.070f,.15f,violet,true);
                Ring("GuardHalo",r,new Vector3(0,-.105f,.18f),.17f,.018f,violet,Vector3.zero,side*14f);
            }
        }

        // MAGE reference: ornate dark-bronze staff with a huge faceted purple crystal,
        // forked crown and several embedded glowing stones.
        private void BuildElyraAmethystStaff()
        {
            Transform pivot=FindRecursive(visualRoot,"WeaponR");if(pivot==null)return;
            Transform r=Root(pivot,"DF_LegendaryV2_Elyra_AmethystStaff");

            Material bronze=Mat("ElyraBronze",new Color(.14f,.075f,.045f),.88f,.78f,false,1f);
            Material bronzeHi=Mat("ElyraBronzeHi",new Color(.40f,.22f,.11f),.95f,.91f,false,1f);
            Material violet=Mat("ElyraViolet",new Color(.44f,.02f,1f),.04f,.99f,true,10f);
            Material hot=Mat("ElyraHot",new Color(.86f,.35f,1f),.01f,1f,true,15f);

            Cylinder("Shaft",r,new Vector3(0,-.18f,0),new Vector3(.090f,.98f,.090f),bronze);
            Cylinder("LowerShaft",r,new Vector3(0,-.92f,0),new Vector3(.115f,.26f,.115f),bronzeHi);
            for(int i=0;i<9;i++)
            {
                float y=-.86f+i*.17f;
                Block("Ornament"+i,r,new Vector3(0,y,.01f),new Vector3(.14f,.028f,.14f),i%3==0?bronzeHi:bronze,new Vector3(0,0,i%2==0?18f:-18f));
            }

            Beam("CrownL0",r,new Vector3(-.07f,.56f,0),new Vector3(-.30f,.84f,.02f),.075f,bronzeHi);
            Beam("CrownL1",r,new Vector3(-.30f,.84f,.02f),new Vector3(-.22f,1.30f,.02f),.072f,bronzeHi);
            Beam("CrownR0",r,new Vector3(.07f,.56f,0),new Vector3(.30f,.84f,.02f),.075f,bronzeHi);
            Beam("CrownR1",r,new Vector3(.30f,.84f,.02f),new Vector3(.22f,1.30f,.02f),.072f,bronzeHi);
            Beam("InnerL",r,new Vector3(-.05f,.67f,.03f),new Vector3(-.17f,1.06f,.04f),.034f,violet);
            Beam("InnerR",r,new Vector3(.05f,.67f,.03f),new Vector3(.17f,1.06f,.04f),.034f,violet);

            Crystal("MainCrystal",r,new Vector3(0,1.14f,.04f),.215f,.76f,hot,true);
            Crystal("HeartGem",r,new Vector3(0,.65f,.09f),.090f,.20f,violet,true);
            Crystal("ShaftGem",r,new Vector3(0,-.18f,.08f),.066f,.17f,violet,true);
            Crystal("FootGem",r,new Vector3(0,-1.19f,.03f),.085f,.21f,hot,true);
            Ring("CrystalAura",r,new Vector3(0,1.14f,.04f),.33f,.020f,violet,new Vector3(66,0,0),18f);
            Ring("CrystalAuraInner",r,new Vector3(0,1.14f,.05f),.26f,.014f,hot,new Vector3(22,52,-18),-25f);
        }

        // HEALER reference: crooked natural wood, wrapped grip and a large luminous blue crescent.
        private void BuildMiraMoonStaff()
        {
            Transform pivot=FindRecursive(visualRoot,"WeaponR");if(pivot==null)return;
            Transform r=Root(pivot,"DF_LegendaryV2_Mira_MoonStaff");

            Material wood=Mat("MiraWood",new Color(.18f,.085f,.038f),.05f,.32f,false,1f);
            Material woodHi=Mat("MiraWoodHi",new Color(.34f,.18f,.07f),.08f,.40f,false,1f);
            Material wrap=Mat("MiraWrap",new Color(.76f,.69f,.55f),.01f,.34f,false,1f);
            Material moon=Mat("MiraMoon",new Color(.015f,.55f,1f),.02f,.98f,true,10f);
            Material moonHot=Mat("MiraMoonHot",new Color(.28f,.90f,1f),.01f,1f,true,15f);

            Vector3[] path={new(.08f,-1.14f,0),new(-.03f,-.82f,.01f),new(-.11f,-.53f,.02f),new(.02f,-.23f,.025f),new(.11f,.08f,.02f),new(-.01f,.38f,.015f),new(-.12f,.64f,.01f),new(-.17f,.78f,.01f)};
            for(int i=0;i<path.Length-1;i++)Beam("Wood"+i,r,path[i],path[i+1],i<2?.125f:.145f,i%2==0?wood:woodHi);
            for(int i=0;i<10;i++)
            {
                float y=-.41f+i*.060f;
                Block("Wrap"+i,r,new Vector3(.015f,y,.035f),new Vector3(.18f,.046f,.17f),wrap,new Vector3(0,0,i%2==0?13f:-11f));
            }

            GameObject crescent=Arc("BlueCrescent",r,new Vector3(-.04f,1.00f,.03f),.45f,.125f,moon,52f,308f,new Vector3(0,0,-8f));
            Animate(crescent,LegendaryWeaponMotion.Mode.Pulse,Vector3.one,1.15f,.035f,.2f);
            Arc("InnerGlow",r,new Vector3(-.04f,1.00f,.04f),.345f,.034f,moonHot,56f,304f,new Vector3(0,0,-8f));
            Beam("BranchL",r,new Vector3(-.17f,.77f,.01f),new Vector3(-.32f,.86f,.02f),.068f,woodHi);
            Beam("BranchR",r,new Vector3(-.14f,.77f,.01f),new Vector3(.07f,.84f,.02f),.064f,woodHi);
            Sphere("MoonCore",r,new Vector3(-.04f,1.00f,.08f),new Vector3(.11f,.11f,.065f),moonHot,true);
            Crystal("GripRune",r,new Vector3(.03f,-.05f,.11f),.045f,.105f,moon,true);
        }

        // TRAVELER reference: Saturn itself is the weapon icon - warm planet, broad bright ring system,
        // subtle orbiting lights and a clean dark-gold staff body.
        private void BuildTravelerSaturnStaff()
        {
            Transform pivot=FindRecursive(visualRoot,"WeaponR");if(pivot==null)return;
            Transform r=Root(pivot,"DF_LegendaryV2_Traveler_SaturnStaff");

            Material dark=Mat("TravelerDark",new Color(.045f,.036f,.028f),.90f,.86f,false,1f);
            Material gold=Mat("TravelerGold",new Color(.64f,.43f,.14f),.94f,.93f,true,3.0f);
            Material planet=Mat("TravelerPlanet",new Color(.86f,.56f,.22f),.22f,.88f,true,5.2f);
            Material cream=Mat("TravelerCream",new Color(1f,.86f,.58f),.08f,.97f,true,7.0f);
            Material ring=Mat("TravelerRing",new Color(.88f,.94f,1f),.26f,.99f,true,10.5f);

            Cylinder("Shaft",r,new Vector3(0,-.34f,0),new Vector3(.086f,.76f,.086f),dark);
            Block("Grip",r,new Vector3(0,-.72f,0),new Vector3(.15f,.40f,.15f),gold);
            Block("Collar",r,new Vector3(0,.20f,.01f),new Vector3(.24f,.12f,.20f),gold);

            Vector3 c=new Vector3(0,.76f,.075f);
            Sphere("Saturn",r,c,new Vector3(.41f,.41f,.41f),planet,true);
            // Planet banding, inspired by Saturn's layered atmosphere.
            Ring("Band0",r,c+new Vector3(0,-.16f,.005f),.30f,.023f,cream,new Vector3(90,0,0),1.5f);
            Ring("Band1",r,c+new Vector3(0,-.07f,.006f),.36f,.028f,gold,new Vector3(90,0,0),-1.8f);
            Ring("Band2",r,c+new Vector3(0,.04f,.007f),.38f,.026f,cream,new Vector3(90,0,0),2.0f);
            Ring("Band3",r,c+new Vector3(0,.14f,.008f),.31f,.022f,gold,new Vector3(90,0,0),-2.2f);

            Ring("SaturnRing",r,c,.68f,.135f,ring,new Vector3(67,0,7),5f);
            Ring("SaturnRingInner",r,c+new Vector3(0,0,.010f),.51f,.030f,gold,new Vector3(67,0,7),-7f);
            Ring("SaturnRingOuter",r,c+new Vector3(0,0,.016f),.78f,.021f,cream,new Vector3(67,0,7),8f);
            for(int i=0;i<4;i++)
            {
                float a=(45f+i*90f)*Mathf.Deg2Rad;
                GameObject star=Sphere("OrbitLight"+i,r,c+new Vector3(Mathf.Cos(a)*.80f,Mathf.Sin(a)*.19f,.05f),new Vector3(.046f,.046f,.046f),i%2==0?ring:cream,true);
                Animate(star,LegendaryWeaponMotion.Mode.Float,Vector3.up,1.0f+i*.12f,.026f,i*.25f);
            }
        }

        // Older cosmetics are hidden only after the replacement exists. We never blanket-disable
        // every renderer on the weapon pivot; this prevents the disappearing-weapon regression.
        private void HideOlderCosmetics()
        {
            string prestigeRoot=identity.Hero switch
            {
                HeroId.Veyr=>"DF_Legendary_EclipseSeraph",
                HeroId.Elyra=>"DF_Legendary_SingularityStaff",
                HeroId.Mira=>"DF_Legendary_AureliaStaff",
                HeroId.Traveler=>"DF_Legendary_LastHour",
                _=>string.Empty
            };

            Renderer[] renderers=visualRoot.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer renderer=renderers[i];
                if(renderer==null||IsInsideNewRoot(renderer.transform))continue;
                bool oldSignature=renderer.gameObject.name.StartsWith("Signature")||renderer.gameObject.name.StartsWith("DF_Signature");
                bool oldPrestige=!string.IsNullOrEmpty(prestigeRoot)&&HasAncestor(renderer.transform,prestigeRoot);
                if(!oldSignature&&!oldPrestige)continue;
                hidden.Add(new RendererState{Renderer=renderer,Enabled=renderer.enabled});
                renderer.enabled=false;
            }
        }

        private bool IsInsideNewRoot(Transform t)
        {
            for(int i=0;i<roots.Count;i++)if(roots[i]!=null&&t.IsChildOf(roots[i].transform))return true;
            return false;
        }

        private static bool HasAncestor(Transform t,string name)
        {
            while(t!=null)
            {
                if(t.name==name)return true;
                t=t.parent;
            }
            return false;
        }

        private Transform Root(Transform pivot,string name)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(pivot,false);roots.Add(go);return go.transform;
        }

        private GameObject Block(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,Vector3 euler=default)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="DF_LegendaryV2_"+name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);return go;
        }

        private GameObject Cylinder(string name,Transform parent,Vector3 pos,Vector3 scale,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="DF_LegendaryV2_"+name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);return go;
        }

        private GameObject Sphere(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,bool pulse=false)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="DF_LegendaryV2_"+name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);
            if(pulse)Animate(go,LegendaryWeaponMotion.Mode.Pulse,Vector3.one,1.55f,.055f,.2f);return go;
        }

        private GameObject Beam(string name,Transform parent,Vector3 from,Vector3 to,float thickness,Material material)
        {
            Vector3 d=to-from;float length=d.magnitude;GameObject go=Block(name,parent,(from+to)*.5f,new Vector3(thickness,length,thickness),material);
            if(length>.0001f)go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,d/length);return go;
        }

        private GameObject Ring(string name,Transform parent,Vector3 pos,float radius,float width,Material material,Vector3 euler,float speed)
        {
            GameObject go=MeshObject(name,parent,RingMesh(radius,width,48),material);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);
            if(Mathf.Abs(speed)>.01f)Animate(go,LegendaryWeaponMotion.Mode.Rotate,Vector3.forward,speed,0,0);return go;
        }

        private GameObject Arc(string name,Transform parent,Vector3 pos,float radius,float width,Material material,float start,float end,Vector3 euler)
        {
            GameObject go=MeshObject(name,parent,ArcMesh(radius,width,start,end,42),material);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(euler);return go;
        }

        private GameObject Crystal(string name,Transform parent,Vector3 pos,float radius,float height,Material material,bool pulse)
        {
            GameObject go=MeshObject(name,parent,CrystalMesh(radius,height),material);go.transform.localPosition=pos;if(pulse)Animate(go,LegendaryWeaponMotion.Mode.Pulse,Vector3.one,1.50f,.045f,.1f);return go;
        }

        private GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            meshes.Add(mesh);GameObject go=new GameObject("DF_LegendaryV2_"+name);go.transform.SetParent(parent,false);MeshFilter f=go.AddComponent<MeshFilter>();f.sharedMesh=mesh;MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;return go;
        }

        private Mesh JaggedSwordMesh(int side,float length,float thickness,float widthScale)
        {
            float[] widths={.15f,.24f,.16f,.23f,.13f,.20f,.11f,.17f,.085f,.050f,.001f};int n=widths.Length;Vector3[] v=new Vector3[n*4];
            for(int i=0;i<n;i++)
            {
                float t=i/(float)(n-1),z=.18f+t*length,cy=-.17f-.23f*t,cx=side*(.025f*Mathf.Sin(t*Mathf.PI*3f)),w=widths[i]*widthScale;
                v[i*4]=new Vector3(cx-w,cy-thickness*.5f,z);v[i*4+1]=new Vector3(cx+w,cy-thickness*.5f,z);v[i*4+2]=new Vector3(cx-w,cy+thickness*.5f,z);v[i*4+3]=new Vector3(cx+w,cy+thickness*.5f,z);
            }
            List<int> tri=new();for(int i=0;i<n-1;i++){int a=i*4,b=(i+1)*4;Quad(tri,a,a+1,b+1,b);Quad(tri,a+2,b+2,b+3,a+3);Quad(tri,a,b,b+2,a+2);Quad(tri,a+1,a+3,b+3,b+1);}Quad(tri,0,2,3,1);int e=(n-1)*4;Quad(tri,e,e+1,e+3,e+2);
            Mesh mesh=new Mesh{name="DF_LegendaryV2JaggedSword"};mesh.vertices=v;mesh.triangles=tri.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static void Quad(List<int> t,int a,int b,int c,int d){t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d);}

        private Mesh RingMesh(float radius,float width,int segments)
        {
            float inner=Mathf.Max(.001f,radius-width*.5f),outer=radius+width*.5f;Vector3[] v=new Vector3[segments*2];int[] t=new int[segments*12];
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments,c=Mathf.Cos(a),s=Mathf.Sin(a);v[i*2]=new Vector3(c*inner,s*inner,0);v[i*2+1]=new Vector3(c*outer,s*outer,0);int n=(i+1)%segments,a0=i*2,a1=i*2+1,b0=n*2,b1=n*2+1,o=i*12;t[o]=a0;t[o+1]=a1;t[o+2]=b1;t[o+3]=a0;t[o+4]=b1;t[o+5]=b0;t[o+6]=a0;t[o+7]=b1;t[o+8]=a1;t[o+9]=a0;t[o+10]=b0;t[o+11]=b1;}
            Mesh mesh=new Mesh{name="DF_LegendaryV2Ring"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private Mesh ArcMesh(float radius,float width,float startDeg,float endDeg,int segments)
        {
            float inner=Mathf.Max(.001f,radius-width*.5f),outer=radius+width*.5f;Vector3[] v=new Vector3[(segments+1)*2];int[] t=new int[segments*12];
            for(int i=0;i<=segments;i++){float p=i/(float)segments,a=Mathf.Lerp(startDeg,endDeg,p)*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);v[i*2]=new Vector3(c*inner,s*inner,0);v[i*2+1]=new Vector3(c*outer,s*outer,0);if(i==segments)continue;int a0=i*2,a1=i*2+1,b0=(i+1)*2,b1=(i+1)*2+1,o=i*12;t[o]=a0;t[o+1]=a1;t[o+2]=b1;t[o+3]=a0;t[o+4]=b1;t[o+5]=b0;t[o+6]=a0;t[o+7]=b1;t[o+8]=a1;t[o+9]=a0;t[o+10]=b0;t[o+11]=b1;}
            Mesh mesh=new Mesh{name="DF_LegendaryV2Arc"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private Mesh CrystalMesh(float radius,float height)
        {
            Vector3 top=new(0,height*.5f,0),bottom=new(0,-height*.5f,0);Vector3[] v={top,new(radius,0,0),new(0,0,radius),new(-radius,0,0),new(0,0,-radius),bottom};int[] t={0,1,2,0,2,3,0,3,4,0,4,1,5,2,1,5,3,2,5,4,3,5,1,4};
            Mesh mesh=new Mesh{name="DF_LegendaryV2Crystal"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private Material Mat(string label,Color color,float metallic,float smooth,bool emissive,float emission)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new Material(shader){name="DF_LegendaryV2_"+label,color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);
            if(emissive){m.EnableKeyword("_EMISSION");if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",color*emission);}materials.Add(m);return m;
        }

        private void Animate(GameObject go,LegendaryWeaponMotion.Mode mode,Vector3 axis,float speed,float amount,float phase)
        {
            LegendaryWeaponMotion motion=go.AddComponent<LegendaryWeaponMotion>();motion.Configure(mode,axis,speed,amount,phase);
        }

        private void Clear()
        {
            for(int i=0;i<hidden.Count;i++)if(hidden[i].Renderer!=null)hidden[i].Renderer.enabled=hidden[i].Enabled;hidden.Clear();
            for(int i=0;i<roots.Count;i++)if(roots[i]!=null)Destroy(roots[i]);roots.Clear();
            for(int i=0;i<materials.Count;i++)if(materials[i]!=null)Destroy(materials[i]);materials.Clear();
            for(int i=0;i<meshes.Count;i++)if(meshes[i]!=null)Destroy(meshes[i]);meshes.Clear();
            built=false;identity=null;visualRoot=null;
        }

        private static Transform FindRecursive(Transform root,string name)
        {
            if(root==null)return null;if(root.name==name||root.name=="DF_"+name)return root;for(int i=0;i<root.childCount;i++){Transform found=FindRecursive(root.GetChild(i),name);if(found!=null)return found;}return null;
        }
    }
}
