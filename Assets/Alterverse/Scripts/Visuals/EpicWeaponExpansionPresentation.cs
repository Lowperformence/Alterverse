using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only layer for weapon catalog variants 2 and 3. It deliberately builds
    /// on the existing gameplay weapon pivots so hit logic, attack timing, muzzle markers,
    /// animation bindings and balance remain exactly the same as the original weapon.
    /// </summary>
    [DefaultExecutionOrder(950)]
    internal sealed class EpicWeaponExpansionBootstrap : MonoBehaviour
    {
        private static EpicWeaponExpansionBootstrap instance;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_EpicWeaponExpansion");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<EpicWeaponExpansionBootstrap>();
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
            EpicWeaponExpansionPresentation presentation=identity.GetComponent<EpicWeaponExpansionPresentation>();
            if(variant<2)
            {
                if(presentation!=null)presentation.RemoveAndRestore();
                return;
            }
            if(presentation==null)presentation=identity.gameObject.AddComponent<EpicWeaponExpansionPresentation>();
            presentation.Show(identity,variant);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class EpicWeaponExpansionPresentation : MonoBehaviour
    {
        private sealed class RendererState
        {
            public Renderer Renderer;
            public bool Enabled;
        }

        private readonly List<RendererState> hidden=new();
        private readonly List<Material> materials=new();
        private HeroIdentity identity;
        private Transform visualRoot;
        private GameObject generatedRoot;
        private int builtVariant=-1;

        public void Show(HeroIdentity hero,int variant)
        {
            identity=hero;
            Transform nextRoot=FindRecursive(transform,"DF_VisualRoot");
            if(nextRoot==null)return;
            if(builtVariant==variant&&visualRoot==nextRoot&&generatedRoot!=null)return;
            RestoreBase();
            DestroyGenerated();
            visualRoot=nextRoot;
            builtVariant=variant;
            Build();
        }

        public void RemoveAndRestore()
        {
            RestoreBase();
            DestroyGenerated();
            Destroy(this);
        }

        private void OnDestroy()
        {
            RestoreBase();
            DestroyGenerated();
        }

        private void Build()
        {
            if(identity==null||visualRoot==null||builtVariant<2)return;
            generatedRoot=new GameObject("DF_EpicWeaponVariant_"+builtVariant);
            generatedRoot.transform.SetParent(visualRoot,false);

            Color glow=WeaponSkinCatalog.For(identity.Hero,builtVariant).Glow;
            Material shell=MakeMaterial("Shell",Color.Lerp(new Color(.035f,.045f,.065f),glow,.16f),.82f,.72f,false);
            Material armor=MakeMaterial("Armor",Color.Lerp(new Color(.35f,.39f,.44f),glow,.22f),.91f,.80f,false);
            Material energy=MakeMaterial("Energy",glow,.18f,.92f,true);
            Material darkEnergy=MakeMaterial("DarkCore",Color.Lerp(Color.black,glow,.18f),.60f,.86f,true);

            switch(identity.Hero)
            {
                case HeroId.Veyr: BuildVeyr(shell,armor,energy);break;
                case HeroId.Kaelon: BuildKaelon(shell,armor,energy,darkEnergy);break;
                case HeroId.Nyra: BuildNyra(shell,armor,energy,darkEnergy);break;
                case HeroId.Elyra: BuildElyra(shell,armor,energy,darkEnergy);break;
                case HeroId.Mira: BuildMira(shell,armor,energy);break;
                case HeroId.Rook: BuildRook(shell,armor,energy,darkEnergy);break;
                case HeroId.Traveler: BuildTraveler(shell,armor,energy,darkEnergy);break;
            }
        }

        private void BuildVeyr(Material shell,Material armor,Material energy)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Transform pivot=WeaponPivot(side<0?"DF_WeaponL":"DF_WeaponR");
                if(pivot==null)continue;
                HideBase(pivot);
                string suffix=side<0?"L":"R";
                Transform anchor=Anchor("Veyr"+suffix,pivot);
                Block("Grip",anchor,new Vector3(0,.00f,-.03f),new Vector3(.085f,.28f,.085f),shell);
                if(builtVariant==2)
                {
                    // Voidfang Twins: deliberately asymmetric long/short silhouettes.
                    float length=side<0?.88f:1.24f;
                    Block("SplitGuardA",anchor,new Vector3(side*.10f,-.12f,.10f),new Vector3(.30f,.045f,.12f),armor,new Vector3(0,0,side*24f));
                    Block("SplitGuardB",anchor,new Vector3(-side*.07f,-.08f,.10f),new Vector3(.17f,.04f,.10f),energy,new Vector3(0,0,-side*36f));
                    BladeStack(anchor,side,length,shell,energy,"Voidfang");
                    Block("Pommel",anchor,new Vector3(0,.17f,-.08f),new Vector3(.11f,.10f,.11f),energy,new Vector3(0,0,45));
                }
                else
                {
                    // Eclipse Chakrams: open ring-sickles instead of conventional swords.
                    Arc("EclipseOuter",anchor,new Vector3(side*.02f,-.35f,.46f),.38f,.048f,armor,Quaternion.Euler(0,0,side*16f),42f,310f);
                    Arc("EclipseEdge",anchor,new Vector3(side*.02f,-.35f,.475f),.315f,.018f,energy,Quaternion.Euler(0,0,side*16f),44f,306f);
                    Block("OffsetGrip",anchor,new Vector3(-side*.14f,-.21f,.25f),new Vector3(.08f,.28f,.10f),shell,new Vector3(0,0,side*38f));
                    Block("CrescentTip",anchor,new Vector3(side*.28f,-.58f,.47f),new Vector3(.08f,.33f,.08f),armor,new Vector3(0,0,-side*28f));
                    Block("EclipseGem",anchor,new Vector3(side*.01f,-.35f,.50f),new Vector3(.10f,.10f,.06f),energy,new Vector3(0,0,45));
                }
            }
        }

        private void BladeStack(Transform anchor,int side,float length,Material shell,Material energy,string label)
        {
            int sections=5;
            for(int i=0;i<sections;i++)
            {
                float t=i/(float)(sections-1);
                float z=.18f+t*length;
                float x=side*(.03f+.18f*t*t);
                float width=Mathf.Lerp(.13f,.035f,t);
                Block(label+"Plate"+i,anchor,new Vector3(x,-.18f-.30f*t,z),new Vector3(width,.20f,length/sections*1.16f),shell,new Vector3(-5f,side*4f,side*7f));
                Block(label+"Seam"+i,anchor,new Vector3(x+side*width*.55f,-.18f-.30f*t,z+.015f),new Vector3(.016f,.13f,length/sections*.92f),energy,new Vector3(-5f,side*4f,side*7f));
            }
        }

        private void BuildKaelon(Material shell,Material armor,Material energy,Material darkEnergy)
        {
            Transform pivot=WeaponPivot("DF_WeaponR");if(pivot==null)return;HideBase(pivot);
            Transform a=Anchor("Kaelon",pivot);
            Block("Haft",a,new Vector3(0,-.03f,0),new Vector3(.12f,1.42f,.12f),shell);
            Block("GripLower",a,new Vector3(0,-.55f,0),new Vector3(.16f,.34f,.16f),armor);
            if(builtVariant==2)
            {
                // Siegebreaker: strictly melee, two-handed industrial hammer.
                Block("HydraulicCage",a,new Vector3(0,.63f,.02f),new Vector3(.82f,.42f,.44f),shell);
                for(int s=-1;s<=1;s+=2)
                {
                    Block("StrikeFace"+s,a,new Vector3(s*.47f,.63f,.02f),new Vector3(.24f,.52f,.54f),armor,new Vector3(0,0,s*-6f));
                    GameObject piston=Cylinder("Piston"+s,a,new Vector3(s*.25f,.63f,.27f),new Vector3(.065f,.24f,.065f),energy,new Vector3(90,0,0));
                    AddMotion(piston,WeaponPartMotion.Mode.Piston,new Vector3(0,0,1),1.7f,.035f,s>0?.3f:.8f);
                    Block("Rail"+s,a,new Vector3(s*.27f,.63f,-.25f),new Vector3(.07f,.34f,.07f),energy);
                }
                Block("PressureCore",a,new Vector3(0,.64f,.27f),new Vector3(.22f,.22f,.10f),energy,new Vector3(0,0,45));
                Block("Crown",a,new Vector3(0,.91f,.02f),new Vector3(.42f,.12f,.34f),armor);
            }
            else
            {
                // Aegis Crusher: reactor/shield ring built into a broad crushing maul head.
                Block("CrusherCore",a,new Vector3(0,.62f,.02f),new Vector3(.70f,.46f,.40f),shell);
                Block("FaceL",a,new Vector3(-.42f,.62f,.03f),new Vector3(.24f,.56f,.50f),armor);
                Block("FaceR",a,new Vector3(.42f,.62f,.03f),new Vector3(.24f,.56f,.50f),armor);
                GameObject ring=Arc("AegisRing",a,new Vector3(0,.63f,.25f),.37f,.045f,energy,Quaternion.Euler(0,0,0),0,360);
                AddMotion(ring,WeaponPartMotion.Mode.Rotate,new Vector3(0,0,1),24f,0,0);
                Block("ShieldSpine",a,new Vector3(0,.63f,.28f),new Vector3(.10f,.64f,.07f),armor);
                Block("Reactor",a,new Vector3(0,.63f,.34f),new Vector3(.19f,.19f,.08f),darkEnergy,new Vector3(0,0,45));
                Block("AegisCap",a,new Vector3(0,.91f,.02f),new Vector3(.48f,.10f,.30f),energy);
            }
        }

        private void BuildNyra(Material shell,Material armor,Material energy,Material darkEnergy)
        {
            Transform pivot=WeaponPivot("DF_WeaponR");if(pivot==null)return;HideBase(pivot);
            Transform a=Anchor("Nyra",pivot);
            if(builtVariant==2)
            {
                Block("StormReceiver",a,new Vector3(0,0,.05f),new Vector3(.26f,.25f,.70f),shell);
                Block("StormStock",a,new Vector3(0,-.08f,-.45f),new Vector3(.20f,.22f,.40f),armor);
                Block("StormBarrel",a,new Vector3(0,.01f,.78f),new Vector3(.09f,.09f,1.30f),armor);
                for(int s=-1;s<=1;s+=2)Block("CoilRail"+s,a,new Vector3(s*.15f,.01f,.72f),new Vector3(.04f,.07f,1.10f),energy);
                for(int i=0;i<4;i++)
                {
                    GameObject coil=Arc("ChargeCoil"+i,a,new Vector3(0,.01f,.34f+i*.28f),.19f,.025f,i%2==0?energy:armor,Quaternion.identity,0,360);
                    AddMotion(coil,WeaponPartMotion.Mode.Rotate,new Vector3(0,0,1),i%2==0?42f:-34f,0,i*.2f);
                }
                Block("StormCell",a,new Vector3(0,-.22f,.05f),new Vector3(.18f,.26f,.20f),energy);
                Block("StormOptic",a,new Vector3(0,.24f,.03f),new Vector3(.14f,.13f,.38f),darkEnergy);
            }
            else
            {
                // Segmented spine and jaw muzzle make the rifle unmistakably biomechanical.
                Block("PredatorStock",a,new Vector3(0,-.05f,-.43f),new Vector3(.22f,.25f,.42f),shell);
                for(int i=0;i<7;i++)
                {
                    float z=-.06f+i*.24f;
                    Block("Vertebra"+i,a,new Vector3(0,.02f,z),new Vector3(.22f-i*.008f,.18f,.18f),i%2==0?armor:shell,new Vector3(0,0,i%2==0?7f:-7f));
                    for(int s=-1;s<=1;s+=2)Block("Rib"+i+"_"+s,a,new Vector3(s*.13f,.02f,z),new Vector3(.13f,.045f,.13f),shell,new Vector3(0,0,s*28f));
                }
                Block("SpineChannel",a,new Vector3(0,.02f,.68f),new Vector3(.055f,.055f,1.25f),energy);
                Block("JawL",a,new Vector3(-.12f,-.04f,1.38f),new Vector3(.11f,.22f,.34f),armor,new Vector3(0,-12,-8));
                Block("JawR",a,new Vector3(.12f,-.04f,1.38f),new Vector3(.11f,.22f,.34f),armor,new Vector3(0,12,8));
                Block("PredatorEye",a,new Vector3(0,.19f,.16f),new Vector3(.10f,.08f,.05f),energy);
            }
        }

        private void BuildElyra(Material shell,Material armor,Material energy,Material darkEnergy)
        {
            Transform pivot=WeaponPivot("DF_WeaponR");if(pivot==null)return;HideBase(pivot);
            Transform a=Anchor("ElyraStaff",pivot);
            // Both new Elyra cosmetics are full-length staffs, preserving the mage's authored weapon language.
            Cylinder("StaffShaft",a,new Vector3(0,-.13f,0),new Vector3(.055f,.82f,.055f),shell);
            Block("StaffGrip",a,new Vector3(0,-.42f,0),new Vector3(.12f,.32f,.12f),armor);
            Block("StaffFoot",a,new Vector3(0,-.93f,0),new Vector3(.13f,.18f,.13f),energy,new Vector3(0,0,45));
            if(builtVariant==2)
            {
                Block("OrreryNeck",a,new Vector3(0,.61f,0),new Vector3(.13f,.28f,.13f),armor);
                GameObject r1=Arc("OrbitA",a,new Vector3(0,.83f,.03f),.34f,.025f,energy,Quaternion.Euler(64,0,25),0,360);
                GameObject r2=Arc("OrbitB",a,new Vector3(0,.83f,.03f),.27f,.025f,armor,Quaternion.Euler(18,58,-17),0,360);
                GameObject r3=Arc("OrbitC",a,new Vector3(0,.83f,.03f),.20f,.018f,energy,Quaternion.Euler(0,78,45),0,360);
                AddMotion(r1,WeaponPartMotion.Mode.Rotate,new Vector3(0,1,1),18f,0,0);
                AddMotion(r2,WeaponPartMotion.Mode.Rotate,new Vector3(1,1,0),-24f,0,.2f);
                AddMotion(r3,WeaponPartMotion.Mode.Rotate,new Vector3(1,0,1),31f,0,.5f);
                Sphere("AstralHeart",a,new Vector3(0,.83f,.08f),new Vector3(.18f,.18f,.18f),energy);
                Sphere("PlanetA",a,new Vector3(.27f,.90f,.06f),new Vector3(.065f,.065f,.065f),armor);
                Sphere("PlanetB",a,new Vector3(-.17f,.67f,.12f),new Vector3(.045f,.045f,.045f),energy);
            }
            else
            {
                // Rift Lantern Staff: caged core, split crown and angular lantern silhouette.
                Block("LanternBase",a,new Vector3(0,.58f,.02f),new Vector3(.28f,.12f,.24f),armor,new Vector3(0,0,45));
                for(int s=-1;s<=1;s+=2)
                {
                    Block("CageSide"+s,a,new Vector3(s*.19f,.82f,.02f),new Vector3(.055f,.50f,.055f),shell,new Vector3(0,0,s*-18f));
                    Block("CrownProng"+s,a,new Vector3(s*.13f,1.06f,.02f),new Vector3(.08f,.34f,.08f),armor,new Vector3(0,0,s*-28f));
                }
                Sphere("RiftCore",a,new Vector3(0,.82f,.07f),new Vector3(.22f,.22f,.22f),darkEnergy);
                GameObject halo=Arc("LanternHalo",a,new Vector3(0,.82f,.09f),.25f,.022f,energy,Quaternion.Euler(70,0,0),0,360);
                AddMotion(halo,WeaponPartMotion.Mode.Rotate,new Vector3(0,0,1),35f,0,0);
                GameObject pulse=Block("CorePulse",a,new Vector3(0,.82f,.18f),new Vector3(.10f,.10f,.04f),energy,new Vector3(0,0,45));
                AddMotion(pulse,WeaponPartMotion.Mode.Pulse,Vector3.one,1.8f,.16f,.3f);
            }
        }

        private void BuildMira(Material shell,Material armor,Material energy)
        {
            Transform pivot=WeaponPivot("DF_WeaponR");if(pivot==null)return;HideBase(pivot);
            Transform a=Anchor("Mira",pivot);
            Cylinder("SupportShaft",a,new Vector3(0,.04f,0),new Vector3(.052f,.86f,.052f),shell);
            Block("SupportGrip",a,new Vector3(0,-.42f,0),new Vector3(.12f,.30f,.12f),armor);
            if(builtVariant==2)
            {
                GameObject halo=Arc("SeraphHalo",a,new Vector3(0,.91f,.04f),.36f,.035f,energy,Quaternion.Euler(0,0,0),0,360);
                AddMotion(halo,WeaponPartMotion.Mode.Rotate,new Vector3(0,0,1),20f,0,0);
                for(int i=0;i<6;i++)
                {
                    float angle=i*60f*Mathf.Deg2Rad;
                    Vector3 p=new Vector3(Mathf.Cos(angle)*.31f,.91f+Mathf.Sin(angle)*.31f,.05f);
                    Block("HaloVane"+i,a,p,new Vector3(.055f,.18f,.055f),armor,new Vector3(0,0,-i*60f));
                }
                Sphere("HealingCore",a,new Vector3(0,.91f,.09f),new Vector3(.17f,.17f,.17f),energy);
            }
            else
            {
                // Layered petals create a very different living/mechanical silhouette.
                Sphere("LifeSeed",a,new Vector3(0,.89f,.08f),new Vector3(.18f,.18f,.18f),energy);
                for(int i=0;i<8;i++)
                {
                    float a0=i*45f*Mathf.Deg2Rad;
                    Vector3 p=new Vector3(Mathf.Cos(a0)*.23f,.89f+Mathf.Sin(a0)*.23f,.04f);
                    GameObject petal=Block("Petal"+i,a,p,new Vector3(.11f,.28f,.06f),i%2==0?armor:shell,new Vector3(0,0,-i*45f));
                    AddMotion(petal,WeaponPartMotion.Mode.Pulse,Vector3.one,1.25f,.055f,i*.08f);
                }
                for(int s=-1;s<=1;s+=2)Block("LifeBranch"+s,a,new Vector3(s*.12f,.61f,.02f),new Vector3(.045f,.35f,.045f),energy,new Vector3(0,0,s*-22f));
            }
        }

        private void BuildRook(Material shell,Material armor,Material energy,Material darkEnergy)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Transform pivot=WeaponPivot(side<0?"DF_GauntletL":"DF_GauntletR");if(pivot==null)continue;HideBase(pivot);
                Transform a=Anchor("Rook"+(side<0?"L":"R"),pivot);
                if(builtVariant==2)
                {
                    Block("TitanPalm",a,new Vector3(0,-.02f,.12f),new Vector3(.42f,.42f,.44f),shell);
                    Block("TitanFace",a,new Vector3(0,.00f,.38f),new Vector3(.40f,.30f,.18f),armor);
                    for(int finger=-1;finger<=1;finger++)
                    {
                        float x=finger*.13f;
                        Block("Knuckle"+finger,a,new Vector3(x,.15f,.50f),new Vector3(.11f,.14f,.22f),armor);
                        GameObject piston=Cylinder("Piston"+finger,a,new Vector3(x,-.03f,.43f),new Vector3(.04f,.14f,.04f),energy,new Vector3(90,0,0));
                        AddMotion(piston,WeaponPartMotion.Mode.Piston,new Vector3(0,0,1),2f,.025f,.2f+finger*.15f);
                    }
                    Block("PressureRail",a,new Vector3(0,-.20f,.31f),new Vector3(.23f,.055f,.35f),energy);
                }
                else
                {
                    Block("MagmaCore",a,new Vector3(0,-.02f,.13f),new Vector3(.43f,.42f,.43f),darkEnergy);
                    Block("ObsidianTop",a,new Vector3(0,.17f,.28f),new Vector3(.38f,.18f,.31f),shell,new Vector3(5,0,side*8));
                    Block("ObsidianLeft",a,new Vector3(-.16f,-.03f,.34f),new Vector3(.17f,.30f,.27f),armor,new Vector3(0,0,-11));
                    Block("ObsidianRight",a,new Vector3(.16f,-.03f,.34f),new Vector3(.17f,.30f,.27f),armor,new Vector3(0,0,11));
                    Block("MagmaFissureA",a,new Vector3(-.08f,.04f,.49f),new Vector3(.035f,.27f,.035f),energy,new Vector3(0,0,-22));
                    Block("MagmaFissureB",a,new Vector3(.10f,-.03f,.50f),new Vector3(.035f,.22f,.035f),energy,new Vector3(0,0,31));
                    Block("StrikeFace",a,new Vector3(0,.01f,.55f),new Vector3(.42f,.26f,.12f),shell);
                }
            }
        }

        private void BuildTraveler(Material shell,Material armor,Material energy,Material darkEnergy)
        {
            Transform pivot=WeaponPivot("DF_WeaponR");if(pivot==null)return;HideBase(pivot);
            Transform a=Anchor("Traveler",pivot);
            Vector3 center=new Vector3(0,-.33f,.30f);
            if(builtVariant==2)
            {
                Sphere("ParadoxCore",a,center,new Vector3(.19f,.19f,.19f),energy);
                GameObject outer=Arc("ParadoxOrbitA",a,center,.38f,.028f,energy,Quaternion.Euler(66,20,23),0,360);
                GameObject inner=Arc("ParadoxOrbitB",a,center,.27f,.025f,armor,Quaternion.Euler(24,68,-18),0,360);
                AddMotion(outer,WeaponPartMotion.Mode.Rotate,new Vector3(1,1,0),24f,0,0);
                AddMotion(inner,WeaponPartMotion.Mode.Rotate,new Vector3(0,1,1),-31f,0,.3f);
                for(int i=0;i<4;i++)
                {
                    float angle=i*90f*Mathf.Deg2Rad;
                    Vector3 p=center+new Vector3(Mathf.Cos(angle)*.32f,Mathf.Sin(angle)*.32f,0);
                    GameObject seg=Block("EngineSegment"+i,a,p,new Vector3(.11f,.22f,.10f),i%2==0?shell:armor,new Vector3(0,0,-i*90));
                    AddMotion(seg,WeaponPartMotion.Mode.Pulse,Vector3.one,1.15f,.045f,i*.14f);
                }
                Block("TemporalNeedle",a,new Vector3(0,-.72f,.31f),new Vector3(.08f,.58f,.08f),shell,new Vector3(0,0,8));
            }
            else
            {
                // Event Horizon: dark singularity with bright lens rings and asymmetric gravity fins.
                Sphere("Singularity",a,center,new Vector3(.24f,.24f,.24f),darkEnergy);
                GameObject lensA=Arc("LensA",a,center,.36f,.026f,energy,Quaternion.Euler(72,0,0),0,360);
                GameObject lensB=Arc("LensB",a,center,.29f,.018f,energy,Quaternion.Euler(15,70,32),0,360);
                AddMotion(lensA,WeaponPartMotion.Mode.Rotate,new Vector3(0,0,1),38f,0,0);
                AddMotion(lensB,WeaponPartMotion.Mode.Rotate,new Vector3(1,0,1),-28f,0,.25f);
                Block("GravityFinL",a,center+new Vector3(-.34f,.02f,0),new Vector3(.30f,.09f,.12f),armor,new Vector3(0,0,-32));
                Block("GravityFinR",a,center+new Vector3(.27f,-.10f,0),new Vector3(.22f,.075f,.12f),shell,new Vector3(0,0,20));
                Block("HorizonGrip",a,new Vector3(0,-.66f,.29f),new Vector3(.10f,.48f,.10f),shell);
            }
        }

        private Transform WeaponPivot(string name)
        {
            return FindRecursive(visualRoot,name);
        }

        private void HideBase(Transform pivot)
        {
            Renderer[] renderers=pivot.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer r=renderers[i];
                if(r==null||r.transform.IsChildOf(generatedRoot.transform))continue;
                hidden.Add(new RendererState{Renderer=r,Enabled=r.enabled});
                r.enabled=false;
            }
        }

        private void RestoreBase()
        {
            for(int i=0;i<hidden.Count;i++)
                if(hidden[i].Renderer!=null)hidden[i].Renderer.enabled=hidden[i].Enabled;
            hidden.Clear();
        }

        private void DestroyGenerated()
        {
            if(generatedRoot!=null)Destroy(generatedRoot);
            generatedRoot=null;
            for(int i=0;i<materials.Count;i++)if(materials[i]!=null)Destroy(materials[i]);
            materials.Clear();
            builtVariant=-1;
        }

        private Transform Anchor(string name,Transform pivot)
        {
            GameObject go=new GameObject("DF_Epic_"+name);
            go.transform.SetParent(generatedRoot.transform,false);
            go.transform.position=pivot.position;
            go.transform.rotation=pivot.rotation;
            go.transform.localScale=pivot.lossyScale;
            return go.transform;
        }

        private Material MakeMaterial(string label,Color color,float metallic,float smoothness,bool emissive)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")??Shader.Find("Sprites/Default");
            Material m=new Material(shader){name="DF_EpicWeapon_"+label+"_"+identity.Hero+"_"+builtVariant,color=color};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);
            if(emissive)
            {
                Color emission=color*2.2f;
                if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",emission);
                m.EnableKeyword("_EMISSION");
            }
            materials.Add(m);
            return m;
        }

        private GameObject Block(string name,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3 rotation=default)
        {
            return Primitive(name,PrimitiveType.Cube,parent,position,scale,material,rotation);
        }

        private GameObject Sphere(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            return Primitive(name,PrimitiveType.Sphere,parent,position,scale,material,Vector3.zero);
        }

        private GameObject Cylinder(string name,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3 rotation=default)
        {
            return Primitive(name,PrimitiveType.Cylinder,parent,position,scale,material,rotation);
        }

        private GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3 rotation)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name="DF_Epic_"+name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(rotation);
            go.transform.localScale=scale;
            Renderer r=go.GetComponent<Renderer>();if(r!=null)r.sharedMaterial=material;
            Collider c=go.GetComponent<Collider>();if(c!=null)Destroy(c);
            return go;
        }

        private GameObject Arc(string name,Transform parent,Vector3 position,float radius,float tube,Material material,Quaternion rotation,float startDeg,float arcDeg)
        {
            GameObject go=new GameObject("DF_Epic_"+name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localRotation=rotation;
            MeshFilter f=go.AddComponent<MeshFilter>();
            f.sharedMesh=BuildArcMesh(name,radius,tube,startDeg,arcDeg);
            MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            return go;
        }

        private static Mesh BuildArcMesh(string name,float radius,float tube,float startDeg,float arcDeg)
        {
            const int arcSegments=36,tubeSegments=6;
            Vector3[] vertices=new Vector3[(arcSegments+1)*tubeSegments];
            int[] triangles=new int[arcSegments*tubeSegments*6];
            for(int i=0;i<=arcSegments;i++)
            {
                float u=i/(float)arcSegments;
                float angle=(startDeg+arcDeg*u)*Mathf.Deg2Rad;
                Vector3 radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                Vector3 center=radial*radius;
                for(int j=0;j<tubeSegments;j++)
                {
                    float v=j/(float)tubeSegments*Mathf.PI*2f;
                    vertices[i*tubeSegments+j]=center+radial*(Mathf.Cos(v)*tube)+Vector3.forward*(Mathf.Sin(v)*tube);
                }
            }
            int t=0;
            for(int i=0;i<arcSegments;i++)
            for(int j=0;j<tubeSegments;j++)
            {
                int a=i*tubeSegments+j,b=i*tubeSegments+(j+1)%tubeSegments;
                int c=(i+1)*tubeSegments+j,d=(i+1)*tubeSegments+(j+1)%tubeSegments;
                triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;
                triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;
            }
            Mesh mesh=new Mesh{name="DF_EpicArc_"+name};
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        private static Transform FindRecursive(Transform root,string name)
        {
            if(root==null)return null;
            if(root.name==name)return root;
            for(int i=0;i<root.childCount;i++)
            {
                Transform found=FindRecursive(root.GetChild(i),name);
                if(found!=null)return found;
            }
            return null;
        }

        private static void AddMotion(GameObject go,WeaponPartMotion.Mode mode,Vector3 axis,float speed,float amplitude,float phase)
        {
            WeaponPartMotion m=go.AddComponent<WeaponPartMotion>();
            m.MotionMode=mode;m.Axis=axis;m.Speed=speed;m.Amplitude=amplitude;m.Phase=phase;
        }
    }

    internal sealed class WeaponPartMotion : MonoBehaviour
    {
        internal enum Mode { Rotate, Pulse, Piston }
        internal Mode MotionMode;
        internal Vector3 Axis=Vector3.up;
        internal float Speed=20f;
        internal float Amplitude=.05f;
        internal float Phase;
        private Vector3 basePosition,baseScale;

        private void Awake(){basePosition=transform.localPosition;baseScale=transform.localScale;}
        private void Update()
        {
            float t=Time.unscaledTime*Speed+Phase;
            switch(MotionMode)
            {
                case Mode.Rotate:
                    transform.Rotate(Axis.normalized,Speed*Time.unscaledDeltaTime,Space.Self);
                    break;
                case Mode.Pulse:
                    transform.localScale=baseScale*(1f+Mathf.Sin(t)*Amplitude);
                    break;
                case Mode.Piston:
                    transform.localPosition=basePosition+Axis.normalized*(Mathf.Sin(t)*Amplitude);
                    break;
            }
        }
    }
}
