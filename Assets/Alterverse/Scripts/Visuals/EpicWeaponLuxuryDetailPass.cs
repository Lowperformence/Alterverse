using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Premium presentation pass for EPIC weapon variants 2 and 3.
    /// It only adds presentation geometry below the generated cosmetic anchors, so combat pivots,
    /// hit logic, animation timing and gameplay balance remain untouched.
    /// </summary>
    [DefaultExecutionOrder(2250)]
    internal sealed class EpicWeaponLuxuryDetailPass : MonoBehaviour
    {
        private static EpicWeaponLuxuryDetailPass instance;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_EpicWeaponLuxuryDetailPass");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<EpicWeaponLuxuryDetailPass>();
        }

        private void LateUpdate()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.20f;
            HeroIdentity[] heroes=FindObjectsByType<HeroIdentity>(FindObjectsSortMode.None);
            for(int i=0;i<heroes.Length;i++)Enhance(heroes[i]);
        }

        private static void Enhance(HeroIdentity identity)
        {
            if(identity==null)return;
            Transform visual=FindRecursive(identity.transform,"DF_VisualRoot");
            if(visual==null)return;

            Transform generated=FindDirect(visual,"DF_EpicWeaponVariant_2");
            int variant=2;
            if(generated==null)
            {
                generated=FindDirect(visual,"DF_EpicWeaponVariant_3");
                variant=3;
            }
            if(generated==null||FindDirect(generated,"DF_EpicLuxuryDetailPass")!=null)return;

            GameObject rootObject=new GameObject("DF_EpicLuxuryDetailPass");
            rootObject.transform.SetParent(generated,false);
            Transform root=rootObject.transform;

            Color glow=WeaponSkinCatalog.For(identity.Hero,variant).Glow;
            Material dark=Material("LuxuryDark",Color.Lerp(new Color(.012f,.016f,.028f),glow,.10f),.88f,.88f,false,1f);
            Material metal=Material("LuxuryMetal",Color.Lerp(new Color(.48f,.53f,.62f),glow,.20f),.96f,.93f,false,1f);
            Material energy=Material("LuxuryEnergy",Color.Lerp(glow,Color.white,.12f),.05f,.98f,true,5.2f);
            Material hot=Material("LuxuryHot",Color.Lerp(glow,Color.white,.42f),.02f,1f,true,7.0f);

            switch(identity.Hero)
            {
                case HeroId.Veyr: EnhanceVeyr(generated,variant,dark,metal,energy,hot);break;
                case HeroId.Kaelon: EnhanceKaelon(generated,variant,dark,metal,energy,hot);break;
                case HeroId.Nyra: EnhanceNyra(generated,variant,dark,metal,energy,hot);break;
                case HeroId.Elyra: EnhanceElyra(generated,variant,dark,metal,energy,hot);break;
                case HeroId.Mira: EnhanceMira(generated,variant,dark,metal,energy,hot);break;
                case HeroId.Rook: EnhanceRook(generated,variant,dark,metal,energy,hot);break;
                case HeroId.Traveler: EnhanceTraveler(generated,variant,dark,metal,energy,hot);break;
            }
        }

        private static void EnhanceVeyr(Transform generated,int variant,Material dark,Material metal,Material energy,Material hot)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Transform a=FindDirect(generated,side<0?"DF_Epic_VeyrL":"DF_Epic_VeyrR");
                if(a==null)continue;
                if(variant==2)
                {
                    float length=side<0?1.18f:1.58f;
                    Block("LuxuryBackbone",a,new Vector3(side*.035f,-.34f,.42f+length*.44f),new Vector3(.10f,.16f,length),dark,new Vector3(-6f,side*4f,side*7f));
                    Block("LuxuryEdge",a,new Vector3(side*.14f,-.38f,.44f+length*.45f),new Vector3(.032f,.105f,length*1.05f),energy,new Vector3(-6f,side*4f,side*8f));
                    Block("LuxuryInnerEdge",a,new Vector3(-side*.055f,-.30f,.48f+length*.42f),new Vector3(.018f,.07f,length*.82f),hot,new Vector3(-6f,side*4f,side*4f));
                    Block("LuxuryGuardWingA",a,new Vector3(side*.25f,-.10f,.13f),new Vector3(.38f,.055f,.14f),metal,new Vector3(0,0,side*28f));
                    Block("LuxuryGuardWingB",a,new Vector3(-side*.18f,-.08f,.14f),new Vector3(.27f,.045f,.12f),energy,new Vector3(0,0,-side*38f));
                    for(int i=0;i<3;i++)
                    {
                        GameObject shard=Block("PhaseShard"+i,a,new Vector3(side*(.22f+.03f*i),-.22f-.11f*i,.48f+.34f*i),new Vector3(.055f,.14f,.035f),i==1?hot:energy,new Vector3(16f,side*(18f+i*9f),side*(28f+i*8f)));
                        AddMotion(shard,PremiumWeaponMotion.Mode.Float,new Vector3(0,1,0),1.3f+i*.15f,.035f,i*.8f);
                    }
                    Sphere("VoidGem",a,new Vector3(0,-.10f,.16f),new Vector3(.16f,.16f,.16f),hot);
                }
                else
                {
                    GameObject outer=Arc("LuxuryChakramOuter",a,new Vector3(side*.02f,-.36f,.49f),.53f,.060f,metal,Quaternion.Euler(0,0,side*15f),36f,318f);
                    GameObject inner=Arc("LuxuryChakramEnergy",a,new Vector3(side*.02f,-.36f,.505f),.43f,.027f,energy,Quaternion.Euler(0,0,side*15f),38f,314f);
                    AddMotion(inner,PremiumWeaponMotion.Mode.Rotate,new Vector3(0,0,1),side*18f,0,0);
                    AddMotion(outer,PremiumWeaponMotion.Mode.Breathe,Vector3.one,1.0f,.018f,.5f);
                    for(int i=0;i<4;i++)
                    {
                        float ang=(48f+i*78f)*Mathf.Deg2Rad;
                        Vector3 p=new Vector3(side*.02f+Mathf.Cos(ang)*.49f,-.36f+Mathf.Sin(ang)*.49f,.53f);
                        Block("EclipseTooth"+i,a,p,new Vector3(.07f,.21f,.05f),i%2==0?hot:energy,new Vector3(0,0,-(48f+i*78f)));
                    }
                    Sphere("EclipseCore",a,new Vector3(side*.02f,-.36f,.54f),new Vector3(.18f,.18f,.10f),hot);
                }
            }
        }

        private static void EnhanceKaelon(Transform generated,int variant,Material dark,Material metal,Material energy,Material hot)
        {
            Transform a=FindDirect(generated,"DF_Epic_Kaelon");if(a==null)return;
            if(variant==2)
            {
                Block("LuxuryHammerMass",a,new Vector3(0,.66f,.02f),new Vector3(1.28f,.62f,.68f),dark);
                for(int s=-1;s<=1;s+=2)
                {
                    Block("LuxuryStrikePlate"+s,a,new Vector3(s*.70f,.66f,.02f),new Vector3(.29f,.68f,.74f),metal,new Vector3(0,0,s*-7f));
                    Block("ImpactCross"+s,a,new Vector3(s*.86f,.66f,.03f),new Vector3(.08f,.42f,.52f),hot);
                    for(int i=-1;i<=1;i++)
                    {
                        GameObject piston=Cylinder("HeavyPiston"+s+"_"+i,a,new Vector3(s*(.31f+.08f*i),.66f,.31f+i*.10f),new Vector3(.075f,.31f,.075f),energy,new Vector3(90,0,0));
                        AddMotion(piston,PremiumWeaponMotion.Mode.Piston,new Vector3(0,0,1),1.45f,.055f,(s>0?.4f:1f)+i*.3f);
                    }
                }
                Sphere("PressureHeart",a,new Vector3(0,.66f,.39f),new Vector3(.30f,.30f,.16f),hot);
                Arc("PressureHalo",a,new Vector3(0,.66f,.40f),.32f,.032f,energy,Quaternion.identity,0,360);
                for(int i=0;i<4;i++)Block("CrownVent"+i,a,new Vector3(-.33f+i*.22f,1.02f,.02f),new Vector3(.09f,.22f,.24f),i%2==0?metal:energy,new Vector3(0,0,(i-2)*8f));
            }
            else
            {
                Block("LuxuryCrusherBody",a,new Vector3(0,.64f,.02f),new Vector3(1.18f,.64f,.66f),dark);
                Block("CrusherFace",a,new Vector3(0,.64f,.38f),new Vector3(.92f,.52f,.22f),metal);
                GameObject ringA=Arc("LuxuryAegisOuter",a,new Vector3(0,.64f,.43f),.53f,.052f,energy,Quaternion.identity,0,360);
                GameObject ringB=Arc("LuxuryAegisInner",a,new Vector3(0,.64f,.46f),.38f,.027f,hot,Quaternion.identity,0,360);
                AddMotion(ringA,PremiumWeaponMotion.Mode.Rotate,new Vector3(0,0,1),22f,0,0);
                AddMotion(ringB,PremiumWeaponMotion.Mode.Rotate,new Vector3(0,0,1),-34f,0,.3f);
                Sphere("AegisReactor",a,new Vector3(0,.64f,.49f),new Vector3(.28f,.28f,.14f),hot);
                for(int i=0;i<6;i++)
                {
                    float ang=i*60f*Mathf.Deg2Rad;
                    Vector3 p=new Vector3(Mathf.Cos(ang)*.47f,.64f+Mathf.Sin(ang)*.47f,.45f);
                    Block("ShieldNode"+i,a,p,new Vector3(.09f,.20f,.08f),i%2==0?metal:energy,new Vector3(0,0,-i*60f));
                }
            }
        }

        private static void EnhanceNyra(Transform generated,int variant,Material dark,Material metal,Material energy,Material hot)
        {
            Transform a=FindDirect(generated,"DF_Epic_Nyra");if(a==null)return;
            if(variant==2)
            {
                Block("LuxuryReceiver",a,new Vector3(0,.02f,.26f),new Vector3(.38f,.34f,1.06f),dark);
                Block("LuxuryBarrelCore",a,new Vector3(0,.02f,1.02f),new Vector3(.10f,.10f,1.65f),hot);
                for(int s=-1;s<=1;s+=2)
                {
                    Block("StormRail"+s,a,new Vector3(s*.22f,.02f,.95f),new Vector3(.055f,.09f,1.62f),energy);
                    Block("StormFin"+s,a,new Vector3(s*.26f,.18f,.45f),new Vector3(.16f,.06f,.72f),metal,new Vector3(0,0,s*10f));
                }
                for(int i=0;i<6;i++)
                {
                    GameObject coil=Arc("LuxuryCoil"+i,a,new Vector3(0,.02f,.38f+i*.26f),.25f,.032f,i%2==0?energy:hot,Quaternion.identity,0,360);
                    AddMotion(coil,PremiumWeaponMotion.Mode.Rotate,new Vector3(0,0,1),i%2==0?48f:-41f,0,i*.25f);
                }
                Sphere("StormCapacitor",a,new Vector3(0,-.25f,.18f),new Vector3(.25f,.25f,.25f),hot);
            }
            else
            {
                Block("PredatorSpineGlow",a,new Vector3(0,.02f,.70f),new Vector3(.075f,.075f,1.55f),energy);
                for(int i=0;i<8;i++)
                {
                    float z=-.02f+i*.25f;
                    for(int s=-1;s<=1;s+=2)
                        Block("PredatorFin"+i+"_"+s,a,new Vector3(s*(.21f+.012f*i),.05f,z),new Vector3(.19f,.055f,.18f),i%3==0?energy:metal,new Vector3(0,0,s*(28f+i*2f)));
                }
                Block("LuxuryJawL",a,new Vector3(-.18f,-.03f,1.62f),new Vector3(.17f,.30f,.45f),dark,new Vector3(0,-14,-10));
                Block("LuxuryJawR",a,new Vector3(.18f,-.03f,1.62f),new Vector3(.17f,.30f,.45f),dark,new Vector3(0,14,10));
                for(int s=-1;s<=1;s+=2)
                for(int i=0;i<3;i++)
                    Block("Fang"+s+"_"+i,a,new Vector3(s*(.08f+i*.045f),-.08f+i*.055f,1.78f),new Vector3(.035f,.11f,.08f),hot,new Vector3(20,s*8,0));
                Sphere("PredatorEyeLuxury",a,new Vector3(0,.22f,.30f),new Vector3(.18f,.12f,.09f),hot);
                GameObject eyePulse=Sphere("PredatorEyePulse",a,new Vector3(0,.22f,.35f),new Vector3(.09f,.07f,.04f),energy);
                AddMotion(eyePulse,PremiumWeaponMotion.Mode.Breathe,Vector3.one,2.2f,.28f,.2f);
            }
        }

        private static void EnhanceElyra(Transform generated,int variant,Material dark,Material metal,Material energy,Material hot)
        {
            Transform a=FindDirect(generated,"DF_Epic_ElyraStaff");if(a==null)return;
            Block("LuxuryStaffSpine",a,new Vector3(0,-.08f,0),new Vector3(.085f,1.98f,.085f),dark);
            Block("LuxuryStaffInlay",a,new Vector3(.055f,-.06f,.012f),new Vector3(.018f,1.55f,.018f),energy);
            if(variant==2)
            {
                Vector3 c=new Vector3(0,.88f,.05f);
                float[] radii={.50f,.39f,.28f};
                Vector3[] rotations={new Vector3(68,0,22),new Vector3(22,62,-18),new Vector3(42,-48,58)};
                for(int i=0;i<3;i++)
                {
                    GameObject ring=Arc("LuxuryOrbit"+i,a,c,radii[i],i==0?.038f:.027f,i==1?metal:energy,Quaternion.Euler(rotations[i]),0,360);
                    AddMotion(ring,PremiumWeaponMotion.Mode.Rotate,new Vector3(i==0?1:0,i==1?1:0,1),i%2==0?24f:-31f,0,i*.4f);
                }
                Sphere("LuxuryAstralHeart",a,c,new Vector3(.29f,.29f,.29f),hot);
                for(int i=0;i<5;i++)
                {
                    float ang=i*72f*Mathf.Deg2Rad;
                    GameObject planet=Sphere("AstralPlanet"+i,a,c+new Vector3(Mathf.Cos(ang)*(.36f+.03f*(i%2)),Mathf.Sin(ang)*(.36f+.03f*(i%2)),.06f),new Vector3(.065f+i*.008f,.065f+i*.008f,.065f+i*.008f),i%2==0?energy:metal);
                    AddMotion(planet,PremiumWeaponMotion.Mode.Breathe,Vector3.one,1.1f,.16f,i*.7f);
                }
                for(int s=-1;s<=1;s+=2)Block("OrreryCrown"+s,a,new Vector3(s*.30f,1.18f,.02f),new Vector3(.11f,.42f,.09f),metal,new Vector3(0,0,s*-32f));
            }
            else
            {
                Vector3 c=new Vector3(0,.86f,.06f);
                for(int s=-1;s<=1;s+=2)
                {
                    Block("LuxuryCage"+s,a,new Vector3(s*.27f,.87f,.03f),new Vector3(.075f,.72f,.075f),metal,new Vector3(0,0,s*-16f));
                    Block("LuxuryCrown"+s,a,new Vector3(s*.20f,1.25f,.02f),new Vector3(.10f,.46f,.09f),energy,new Vector3(0,0,s*-29f));
                }
                Sphere("LuxuryRiftCore",a,c,new Vector3(.32f,.32f,.32f),hot);
                float[] radii={.42f,.33f,.25f};
                for(int i=0;i<3;i++)
                {
                    GameObject halo=Arc("RiftHalo"+i,a,c,radii[i],.027f,i==1?hot:energy,Quaternion.Euler(62-i*21,20+i*24,15-i*18),0,360);
                    AddMotion(halo,PremiumWeaponMotion.Mode.Rotate,new Vector3(i%2,1,(i+1)%2),i%2==0?32f:-39f,0,i*.25f);
                }
                Block("LanternTail",a,new Vector3(0,.48f,.05f),new Vector3(.09f,.42f,.09f),dark,new Vector3(0,0,45));
            }
        }

        private static void EnhanceMira(Transform generated,int variant,Material dark,Material metal,Material energy,Material hot)
        {
            Transform a=FindDirect(generated,"DF_Epic_Mira");if(a==null)return;
            Block("LuxurySupportSpine",a,new Vector3(0,.02f,0),new Vector3(.080f,1.90f,.080f),dark);
            if(variant==2)
            {
                Vector3 c=new Vector3(0,.96f,.06f);
                GameObject outer=Arc("SeraphOuter",a,c,.52f,.045f,energy,Quaternion.identity,0,360);
                GameObject inner=Arc("SeraphInner",a,c,.39f,.024f,hot,Quaternion.identity,0,360);
                AddMotion(outer,PremiumWeaponMotion.Mode.Rotate,new Vector3(0,0,1),15f,0,0);
                AddMotion(inner,PremiumWeaponMotion.Mode.Rotate,new Vector3(0,0,1),-25f,0,.3f);
                for(int i=0;i<8;i++)
                {
                    float ang=i*45f*Mathf.Deg2Rad;
                    Vector3 p=c+new Vector3(Mathf.Cos(ang)*.47f,Mathf.Sin(ang)*.47f,0);
                    Block("SeraphWing"+i,a,p,new Vector3(.10f,.30f,.06f),i%2==0?metal:energy,new Vector3(0,0,-i*45f));
                }
                Sphere("SeraphHeart",a,c+new Vector3(0,0,.05f),new Vector3(.27f,.27f,.20f),hot);
                for(int s=-1;s<=1;s+=2)Block("AngelBranch"+s,a,new Vector3(s*.21f,.58f,.02f),new Vector3(.06f,.48f,.06f),energy,new Vector3(0,0,s*-28f));
            }
            else
            {
                Vector3 c=new Vector3(0,.94f,.06f);
                Sphere("LuxuryLifeSeed",a,c,new Vector3(.28f,.28f,.24f),hot);
                for(int i=0;i<12;i++)
                {
                    float ang=i*30f*Mathf.Deg2Rad;
                    float radius=i%2==0?.37f:.31f;
                    Vector3 p=c+new Vector3(Mathf.Cos(ang)*radius,Mathf.Sin(ang)*radius,0);
                    GameObject petal=Block("LuxuryPetal"+i,a,p,new Vector3(i%2==0?.12f:.09f,i%2==0?.34f:.27f,.065f),i%3==0?energy:metal,new Vector3(0,0,-i*30f));
                    AddMotion(petal,PremiumWeaponMotion.Mode.Breathe,Vector3.one,1.0f,.055f,i*.22f);
                }
                for(int s=-1;s<=1;s+=2)
                {
                    Block("LifeVine"+s,a,new Vector3(s*.18f,.53f,.03f),new Vector3(.050f,.55f,.050f),energy,new Vector3(0,0,s*-25f));
                    Sphere("LifeBud"+s,a,new Vector3(s*.30f,.38f,.04f),new Vector3(.11f,.11f,.11f),hot);
                }
            }
        }

        private static void EnhanceRook(Transform generated,int variant,Material dark,Material metal,Material energy,Material hot)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Transform a=FindDirect(generated,side<0?"DF_Epic_RookL":"DF_Epic_RookR");if(a==null)continue;
                if(variant==2)
                {
                    Block("LuxuryFistBody",a,new Vector3(0,-.02f,.22f),new Vector3(.60f,.56f,.62f),dark);
                    Block("LuxuryImpactFace",a,new Vector3(0,.04f,.58f),new Vector3(.58f,.37f,.26f),metal);
                    for(int i=-2;i<=2;i++)
                    {
                        float x=i*.115f;
                        Block("TitanKnuckle"+i,a,new Vector3(x,.19f,.73f),new Vector3(.10f,.18f,.24f),i%2==0?hot:metal);
                    }
                    for(int s=-1;s<=1;s+=2)
                    {
                        Block("SideRail"+s,a,new Vector3(s*.31f,-.10f,.38f),new Vector3(.075f,.46f,.36f),energy,new Vector3(0,0,s*8f));
                        GameObject piston=Cylinder("LuxuryPiston"+s,a,new Vector3(s*.22f,-.18f,.54f),new Vector3(.055f,.20f,.055f),hot,new Vector3(90,0,0));
                        AddMotion(piston,PremiumWeaponMotion.Mode.Piston,new Vector3(0,0,1),1.9f,.04f,s>0?.3f:.9f);
                    }
                    Block("WristArmor",a,new Vector3(0,-.34f,.08f),new Vector3(.52f,.25f,.42f),metal);
                }
                else
                {
                    Block("MagmaMass",a,new Vector3(0,-.01f,.25f),new Vector3(.61f,.58f,.64f),dark);
                    for(int i=0;i<6;i++)
                    {
                        float ang=(i*60f+30f)*Mathf.Deg2Rad;
                        Vector3 p=new Vector3(Mathf.Cos(ang)*.29f,Mathf.Sin(ang)*.24f,.54f);
                        Block("ObsidianSpike"+i,a,p,new Vector3(.13f,.26f,.16f),metal,new Vector3(20f,side*(i-3)*8f,-i*60f));
                    }
                    Block("MagmaCrackA",a,new Vector3(-.15f,.05f,.61f),new Vector3(.045f,.40f,.045f),energy,new Vector3(0,0,-26));
                    Block("MagmaCrackB",a,new Vector3(.13f,-.02f,.63f),new Vector3(.045f,.34f,.045f),hot,new Vector3(0,0,31));
                    Block("MagmaCrackC",a,new Vector3(.01f,.18f,.66f),new Vector3(.035f,.27f,.045f),energy,new Vector3(0,0,6));
                    Sphere("MoltenHeart",a,new Vector3(0,-.01f,.48f),new Vector3(.26f,.24f,.18f),hot);
                    Block("WristObsidian",a,new Vector3(0,-.35f,.10f),new Vector3(.54f,.27f,.43f),dark);
                }
            }
        }

        private static void EnhanceTraveler(Transform generated,int variant,Material dark,Material metal,Material energy,Material hot)
        {
            Transform a=FindDirect(generated,"DF_Epic_Traveler");if(a==null)return;
            Vector3 c=new Vector3(0,-.32f,.31f);
            if(variant==2)
            {
                Sphere("LuxuryParadoxCore",a,c,new Vector3(.30f,.30f,.30f),hot);
                float[] radii={.56f,.43f,.32f};
                Vector3[] rot={new Vector3(68,18,23),new Vector3(20,70,-18),new Vector3(47,-43,61)};
                for(int i=0;i<3;i++)
                {
                    GameObject ring=Arc("ParadoxLuxuryOrbit"+i,a,c,radii[i],i==0?.040f:.028f,i==1?metal:energy,Quaternion.Euler(rot[i]),0,360);
                    AddMotion(ring,PremiumWeaponMotion.Mode.Rotate,new Vector3(i==0?1:0,1,i==2?1:0),i%2==0?28f:-36f,0,i*.33f);
                }
                for(int i=0;i<6;i++)
                {
                    float ang=i*60f*Mathf.Deg2Rad;
                    Vector3 p=c+new Vector3(Mathf.Cos(ang)*.49f,Mathf.Sin(ang)*.49f,.02f);
                    GameObject seg=Block("FloatingArmor"+i,a,p,new Vector3(.13f,.28f,.11f),i%2==0?dark:metal,new Vector3(0,0,-i*60f));
                    AddMotion(seg,PremiumWeaponMotion.Mode.Float,Vector3.forward,1.2f,.045f,i*.45f);
                }
                Block("ChronalBlade",a,new Vector3(0,-.88f,.31f),new Vector3(.10f,.82f,.10f),energy,new Vector3(0,0,7));
            }
            else
            {
                Sphere("LuxurySingularity",a,c,new Vector3(.36f,.36f,.36f),dark);
                Sphere("SingularityCorona",a,c+new Vector3(0,0,.04f),new Vector3(.22f,.22f,.10f),hot);
                float[] radii={.58f,.46f,.35f};
                Vector3[] rot={new Vector3(74,0,0),new Vector3(16,68,31),new Vector3(42,-51,55)};
                for(int i=0;i<3;i++)
                {
                    GameObject lens=Arc("HorizonLens"+i,a,c,radii[i],i==0?.038f:.024f,i==1?hot:energy,Quaternion.Euler(rot[i]),0,360);
                    AddMotion(lens,PremiumWeaponMotion.Mode.Rotate,new Vector3(i%2,1,(i+1)%2),i%2==0?42f:-33f,0,i*.3f);
                }
                for(int s=-1;s<=1;s+=2)
                {
                    Block("GravityBlade"+s,a,c+new Vector3(s*.47f,-.04f,.01f),new Vector3(.44f,.11f,.16f),s<0?metal:dark,new Vector3(0,0,s*-31f));
                    Sphere("LensSatellite"+s,a,c+new Vector3(s*.63f,.13f,.05f),new Vector3(.09f,.09f,.09f),energy);
                }
                Block("HorizonTail",a,new Vector3(0,-.78f,.30f),new Vector3(.11f,.70f,.11f),dark);
            }
        }

        private static Material Material(string label,Color color,float metallic,float smoothness,bool emissive,float emissionStrength)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")??Shader.Find("Sprites/Default");
            Material m=new Material(shader);
            m.name="DF_"+label;
            m.color=color;
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);
            if(emissive)
            {
                Color emission=color*emissionStrength;
                if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",emission);
                m.EnableKeyword("_EMISSION");
            }
            return m;
        }

        private static GameObject Block(string name,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3 rotation=default)
        {
            return Primitive(name,PrimitiveType.Cube,parent,position,scale,material,rotation);
        }

        private static GameObject Sphere(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            return Primitive(name,PrimitiveType.Sphere,parent,position,scale,material,Vector3.zero);
        }

        private static GameObject Cylinder(string name,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3 rotation=default)
        {
            return Primitive(name,PrimitiveType.Cylinder,parent,position,scale,material,rotation);
        }

        private static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material,Vector3 rotation)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name="DF_Luxury_"+name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(rotation);
            go.transform.localScale=scale;
            Renderer renderer=go.GetComponent<Renderer>();if(renderer!=null)renderer.sharedMaterial=material;
            Collider collider=go.GetComponent<Collider>();if(collider!=null)Destroy(collider);
            return go;
        }

        private static GameObject Arc(string name,Transform parent,Vector3 position,float radius,float tube,Material material,Quaternion rotation,float startDeg,float arcDeg)
        {
            GameObject go=new GameObject("DF_Luxury_"+name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=position;
            go.transform.localRotation=rotation;
            MeshFilter filter=go.AddComponent<MeshFilter>();
            filter.sharedMesh=BuildArcMesh(name,radius,tube,startDeg,arcDeg);
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            return go;
        }

        private static Mesh BuildArcMesh(string name,float radius,float tube,float startDeg,float arcDeg)
        {
            const int arcSegments=40;
            const int tubeSegments=8;
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
                int a=i*tubeSegments+j;
                int b=i*tubeSegments+(j+1)%tubeSegments;
                int c=(i+1)*tubeSegments+j;
                int d=(i+1)*tubeSegments+(j+1)%tubeSegments;
                triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;
                triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;
            }
            Mesh mesh=new Mesh();
            mesh.name="DF_LuxuryArc_"+name;
            mesh.vertices=vertices;
            mesh.triangles=triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddMotion(GameObject go,PremiumWeaponMotion.Mode mode,Vector3 axis,float speed,float amplitude,float phase)
        {
            PremiumWeaponMotion motion=go.AddComponent<PremiumWeaponMotion>();
            motion.MotionMode=mode;
            motion.Axis=axis;
            motion.Speed=speed;
            motion.Amplitude=amplitude;
            motion.Phase=phase;
        }

        private static Transform FindDirect(Transform root,string name)
        {
            if(root==null)return null;
            for(int i=0;i<root.childCount;i++)if(root.GetChild(i).name==name)return root.GetChild(i);
            return null;
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
    }

    internal sealed class PremiumWeaponMotion : MonoBehaviour
    {
        internal enum Mode { Rotate, Breathe, Piston, Float }
        internal Mode MotionMode;
        internal Vector3 Axis=Vector3.up;
        internal float Speed=1f;
        internal float Amplitude=.05f;
        internal float Phase;
        private Vector3 basePosition;
        private Vector3 baseScale;

        private void Awake()
        {
            basePosition=transform.localPosition;
            baseScale=transform.localScale;
        }

        private void Update()
        {
            float wave=Mathf.Sin(Time.unscaledTime*Speed+Phase);
            switch(MotionMode)
            {
                case Mode.Rotate:
                    transform.Rotate(Axis.normalized,Speed*Time.unscaledDeltaTime,Space.Self);
                    break;
                case Mode.Breathe:
                    transform.localScale=baseScale*(1f+wave*Amplitude);
                    break;
                case Mode.Piston:
                case Mode.Float:
                    transform.localPosition=basePosition+Axis.normalized*(wave*Amplitude);
                    break;
            }
        }
    }
}
