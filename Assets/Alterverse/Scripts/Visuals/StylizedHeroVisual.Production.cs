using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // Model coordinates preserve the existing collider, camera framing and foot planting contract.
        private void BuildProductionHero()
        {
            HeroId h=identity.Hero;bool tank=h==HeroId.Kaelon,bruiser=h==HeroId.Rook,heavy=tank||bruiser;
            float breadth=tank?1.48f:bruiser?1.32f:h==HeroId.Mira?.91f:.86f;
            Color trim=tank?new Color(.66f,.43f,.16f):Color.Lerp(accent,dark,.28f);
            Color metal=tank?new Color(.41f,.44f,.47f):h==HeroId.Mira?new Color(.72f,.77f,.70f):new Color(.29f,.33f,.40f);
            Color identityPanel=Color.Lerp(accent,metal,.32f);
            Mesh chest=HeroMeshLibrary.Loft("DF_AnatomicalCuirass",new[]{-.36f,-.19f,.08f,.34f,.45f,.53f},new[]{.24f,.23f,.32f,.39f,.33f,.14f},new[]{.15f,.14f,.21f,.23f,.19f,.12f});
            Sculpt("Torso",chest,Vector3.zero,new Vector3(breadth,1,heavy?1.18f:1),dark);
            // Overlapping breastplate and abdominal courses share a contoured rib cage, not a stack of boxes.
            Plate("Breastplate",new Vector3(0,.24f,.20f),.61f*breadth,.46f,.12f,identityPanel);
            Plate("Abdomen",new Vector3(0,-.08f,.155f),.39f*breadth,.30f,.11f,dark);
            Plate("Gorget",new Vector3(0,.48f,.10f),.38f*breadth,.17f,.22f,trim);
            Mesh pelvis=HeroMeshLibrary.Loft("DF_Pelvis",new[]{-.43f,-.30f,-.19f},new[]{.25f,.30f,.24f},new[]{.14f,.17f,.14f});
            Sculpt("Pelvis",pelvis,Vector3.zero,new Vector3(breadth,1,1),cloth);
            float legWidth=heavy?.245f:.175f;
            Mesh leg=HeroMeshLibrary.Loft("DF_ArticulatedGreave",new[]{-1f,-.74f,-.18f,.12f,.63f,1f},new[]{.065f,.087f,.092f,.13f,.145f,.12f},new[]{.085f,.095f,.10f,.13f,.13f,.105f},new[]{.025f,.02f,0f,.005f,0f,0f});
            Mesh boot=HeroMeshLibrary.Loft("DF_ArmoredBoot",new[]{-.07f,-.03f,.075f,.16f},new[]{.12f,.13f,.11f,.075f},new[]{.18f,.22f,.16f,.09f},new[]{.075f,.09f,.055f,0f});
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Sculpt("Leg"+s,leg,new Vector3(side*legWidth,-.62f,0),new Vector3(heavy?1.26f:1,.34f,heavy?1.18f:1),dark);
                Sculpt("Boot"+s,boot,new Vector3(side*legWidth,-.91f,.02f),Vector3.one,metal);
                Plate("Knee"+s,new Vector3(side*legWidth,-.57f,.115f),heavy?.25f:.18f,.22f,.095f,metal,0,"Leg"+s);
                Plate("Thigh"+s,new Vector3(side*legWidth,-.40f,.10f),heavy?.27f:.19f,.28f,.08f,trim,side*-8,"Leg"+s);
                Transform arm=new GameObject("DF_Arm"+s).transform;arm.SetParent(root,false);arm.localPosition=new Vector3(side*(heavy?.49f:.345f),.40f,.015f);
                Mesh upper=HeroMeshLibrary.Loft("DF_UpperArm",new[]{-.32f,-.25f,-.06f,.04f},new[]{.072f,.09f,.13f,.10f},new[]{.08f,.10f,.12f,.09f});
                Sculpt("UpperArm"+s,upper,Vector3.zero,new Vector3(heavy?1.35f:1,1,1),bruiser?new Color(.44f,.29f,.22f):dark,arm);
                Mesh fore=HeroMeshLibrary.Loft("DF_Forearm",new[]{-.26f,-.21f,-.055f,.025f},new[]{.065f,.095f,.105f,.073f},new[]{.07f,.095f,.11f,.07f});
                Sculpt("Vambrace"+s,fore,new Vector3(0,-.30f,.02f),new Vector3(heavy?1.35f:1,1,1),metal,arm);
                Mesh glove=HeroMeshLibrary.Loft("DF_Glove",new[]{-.13f,-.07f,.07f,.11f},new[]{.055f,.09f,.095f,.07f},new[]{.065f,.075f,.085f,.055f});
                Sculpt("Glove"+s,glove,new Vector3(0,-.59f,.06f),Vector3.one,dark,arm);
                arm.localRotation=Quaternion.Euler(h==HeroId.Nyra?-48:h==HeroId.Elyra?-18:0,0,h==HeroId.Nyra?(side<0?55:-8):side*(heavy?-8:-10));
                Plate("Shoulder"+s,new Vector3(side*(heavy?.53f:.36f),.43f,0),heavy?.52f:.29f,heavy?.38f:.24f,heavy?.50f:.31f,side<0?identityPanel:trim,side*-16);
                Plate("Tasset"+s,new Vector3(side*.20f*breadth,-.31f,.15f),.20f*breadth,.30f,.08f,metal,side*9);
            }
            BuildProductionHead(h,metal,trim);
            switch(h)
            {
                case HeroId.Veyr: ProductionBlades();ProductionCoat(false);break;
                case HeroId.Kaelon: ProductionCitadel(metal,trim);break;
                case HeroId.Nyra: ProductionRifle(metal);break;
                case HeroId.Elyra: ProductionOrbit();ProductionCoat(true);break;
                case HeroId.Mira: ProductionStaff(metal);ProductionCoat(true);break;
                case HeroId.Rook: ProductionGauntlets(metal);break;
            }
            Detail("IdentityCore",new Vector3(0,.29f,.29f),new Vector3(.08f,.17f,.035f),accent,new Vector3(0,0,45),null,true);
            CreateSignature();
            // This remains the same runtime model in matches and in the menu showcase.
        }
        private GameObject Sculpt(string name,Mesh mesh,Vector3 position,Vector3 scale,Color color,Transform parent=null,bool energy=false)
        {
            var go=new GameObject("DF_"+name);go.transform.SetParent(parent!=null?parent:root,false);
            go.transform.localPosition=position;go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=HeroMeshLibrary.Painted(mesh,FinishedColor(color,energy),energy);go.AddComponent<MeshRenderer>().sharedMaterial=MaterialFor(color,energy);return go;
        }
        private void BuildProductionHead(HeroId h,Color metal,Color trim)
        {
            bool helmet=h==HeroId.Veyr||h==HeroId.Kaelon;
            float[] y={-.22f,-.15f,-.04f,.09f,.18f,.24f};
            Mesh head=HeroMeshLibrary.Loft(helmet?"DF_VisoredHelm":"DF_SculptedFace",y,new[]{.055f,.115f,.165f,.17f,.145f,.065f},new[]{.055f,.105f,.14f,.14f,.11f,.055f},new[]{.05f,.035f,.018f,0f,-.005f,-.015f},16);
            Vector3 p=new Vector3(0,.79f,.015f);Color skin=h==HeroId.Rook?new Color(.52f,.34f,.26f):new Color(.73f,.55f,.47f);
            Sculpt("Head",head,p,Vector3.one*(h==HeroId.Kaelon?1.08f:1),helmet?metal:skin);
            Mesh neck=HeroMeshLibrary.Loft("DF_Neck",new[]{-.09f,.08f},new[]{.078f,.086f},new[]{.072f,.076f});
            Sculpt("Neck",neck,new Vector3(0,.55f,0),Vector3.one,helmet?dark:skin);
            if(helmet)
            {
                Plate("FaceGuard",p+new Vector3(0,-.04f,.14f),.27f,.34f,.085f,dark);
                for(int side=-1;side<=1;side+=2)
                {
                    Detail("Visor"+side,p+new Vector3(side*.071f,.055f,.155f),new Vector3(.15f,.028f,.024f),accent,new Vector3(0,0,side*28),null,true);
                    Plate("HelmFin"+side,p+new Vector3(side*.14f,.14f,-.025f),.10f,.37f,.18f,trim,side*-20);
                }
                Plate("HelmKeel",p+new Vector3(0,.17f,-.03f),.085f,.38f,.27f,trim);
            }
            else
            {
                // Eyes, cheek planes and a small bridge read in the menu without photorealistic texture work.
                for(int side=-1;side<=1;side+=2)
                {
                    Detail("EyeWhite"+side,p+new Vector3(side*.075f,.035f,.149f),new Vector3(.062f,.026f,.018f),armorLight);
                    Detail("Iris"+side,p+new Vector3(side*.072f,.035f,.16f),new Vector3(.022f,.025f,.012f),accent,default,null,h==HeroId.Elyra);
                    Detail("Brow"+side,p+new Vector3(side*.071f,.069f,.148f),new Vector3(.078f,.021f,.020f),cloth,new Vector3(0,0,side*-10));
                }
                Plate("Nose",p+new Vector3(0,-.015f,.148f),.038f,.085f,.034f,skin);
                Detail("Mouth",p+new Vector3(0,-.103f,.131f),new Vector3(.070f,.013f,.012f),new Color(.30f,.17f,.16f));
                Color hair=h==HeroId.Elyra?new Color(.42f,.32f,.58f):h==HeroId.Mira?new Color(.46f,.52f,.34f):cloth;
                Mesh cap=HeroMeshLibrary.Loft("DF_HairCap",new[]{.02f,.13f,.22f,.28f},new[]{.174f,.18f,.145f,.03f},new[]{.145f,.15f,.13f,.04f},new[]{-.06f,-.045f,-.02f,-.015f});
                Sculpt("Hair",cap,p,Vector3.one,hair);
                for(int side=-1;side<=1;side+=2)
                {
                    Mesh strand=HeroMeshLibrary.Ribbon("DF_HairLock"+side,new[]{new Vector3(side*.12f,.23f,0),new Vector3(side*.18f,.06f,.06f),new Vector3(side*.19f,-.15f,-.02f),new Vector3(side*.16f,-.42f,-.10f)},new[]{.085f,.065f,.055f,.012f},.035f);
                    if(h!=HeroId.Rook)Sculpt("HairLock"+side,strand,p,new Vector3(1,h==HeroId.Nyra?.60f:1,1),hair);
                }
                if(h==HeroId.Rook)Plate("Beard",p+new Vector3(0,-.15f,.13f),.19f,.19f,.065f,cloth);
                if(h==HeroId.Nyra)Detail("Comms",p+new Vector3(-.20f,.075f,0),new Vector3(.08f,.18f,.18f),accent,default,null,true);
            }
        }
        private Transform WeaponPivot(string suffix,Vector3 point)
        {var t=new GameObject("DF_Weapon"+suffix).transform;t.SetParent(root,false);t.localPosition=point;return t;}
        private void ProductionBlades()
        {
            // Base Zhaev blades stay deliberately neutral so the purchasable weapon skin
            // remains visually distinct. ApplyWeaponOverdrive() still replaces both complete
            // weapon pivots with the cosmetic glow when that skin is equipped.
            Color baseBladeGray=new Color(.42f,.45f,.49f,1f);
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";Transform weapon=WeaponPivot(s,new Vector3(side*.43f,-.18f,.13f));
                Mesh blade=HeroMeshLibrary.Ribbon("DF_DimensionBlade"+s,new[]{new Vector3(0,-.10f,0),new Vector3(side*.035f,-.26f,.24f),new Vector3(side*.12f,-.49f,.66f),new Vector3(side*.21f,-.67f,1.05f)},new[]{.048f,.105f,.08f,.001f},.06f);
                Sculpt("Blade"+s,blade,Vector3.zero,Vector3.one,baseBladeGray,weapon,false);
                Detail("Hilt"+s,weapon.localPosition,new Vector3(.14f,.20f,.12f),dark,default,"Weapon"+s);
                Bind("Weapon"+s,"Arm"+s);
            }
        }
        private void ProductionCitadel(Color metal,Color trim)
        {
            var shield=WeaponPivot("L",new Vector3(-.72f,.06f,.32f));
            Plate("Shield",shield.localPosition,.80f,1.25f,.19f,metal,0,"WeaponL");
            Plate("ShieldInset",shield.localPosition+Vector3.forward*.115f,.63f,1.06f,.055f,trim,0,"WeaponL");
            Plate("ShieldDark",shield.localPosition+Vector3.forward*.15f,.45f,.85f,.03f,dark,0,"WeaponL");
            Detail("ShieldCore",shield.localPosition+Vector3.forward*.18f,new Vector3(.08f,.57f,.03f),accent,default,"WeaponL",true);
            var weapon=WeaponPivot("R",new Vector3(.58f,-.18f,.14f));
            Mesh grip=HeroMeshLibrary.Loft("DF_HammerShaft",new[]{-.38f,.48f},new[]{.045f,.05f},new[]{.045f,.05f});
            Sculpt("HammerGrip",grip,Vector3.zero,Vector3.one,cloth,weapon);
            Plate("AnchorHead",weapon.localPosition+Vector3.up*.43f,.63f,.38f,.42f,metal,0,"WeaponR");
            for(int side=-1;side<=1;side+=2)Detail("HammerEnergy"+side,weapon.localPosition+new Vector3(side*.24f,.43f,.225f),new Vector3(.095f,.23f,.03f),accent,default,"WeaponR",true);
            Bind("WeaponL","ArmL");Bind("WeaponR","ArmR");
        }
        private void ProductionRifle(Color metal)
        {
            Transform weapon=WeaponPivot("R",new Vector3(.24f,.10f,.40f));
            Mesh rifle=HeroMeshLibrary.Loft("DF_RifleReceiver",new[]{-.58f,-.39f,-.20f,.38f,.57f,.78f},new[]{.10f,.12f,.145f,.095f,.07f,.07f},new[]{.12f,.14f,.12f,.065f,.05f,.05f});
            var gun=Sculpt("Rifle",rifle,Vector3.zero,Vector3.one,metal,weapon);gun.transform.localRotation=Quaternion.Euler(90,0,0);
            Detail("WeaponRail",weapon.localPosition+new Vector3(0,.10f,.18f),new Vector3(.045f,.04f,1.05f),accent,default,"WeaponR",true);
            Detail("RifleMagazine",weapon.localPosition+new Vector3(0,-.18f,-.12f),new Vector3(.14f,.27f,.23f),dark,default,"WeaponR");
            Detail("Scope",weapon.localPosition+new Vector3(0,.22f,-.12f),new Vector3(.12f,.12f,.34f),dark,default,"WeaponR");
            Detail("ScopeLens",weapon.localPosition+new Vector3(0,.22f,.055f),new Vector3(.075f,.065f,.015f),accent,default,"WeaponR",true);
            var muzzle=new GameObject("DF_Muzzle").transform;muzzle.SetParent(weapon,false);muzzle.localPosition=new Vector3(0,0,.83f);
            Bind("WeaponR","ArmR");
        }
        private void ProductionOrbit()
        {
            Mesh orb=HeroMeshLibrary.Loft("DF_FocusOrb",new[]{-.16f,-.10f,0f,.10f,.16f},new[]{.015f,.125f,.16f,.125f,.015f},new[]{.015f,.125f,.16f,.125f,.015f});
            Sculpt("Orb",orb,new Vector3(.48f,.22f,.38f),Vector3.one,accent,null,true);
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                var ring=Sculpt("Orbit"+s,HeroMeshLibrary.Ring("DF_FocusOrbit",.27f,.022f),new Vector3(side*.58f,.28f,.03f),Vector3.one,accent,null,true);
                ring.transform.localRotation=Quaternion.Euler(38,0,side*35);
                Plate("OrbitShard"+s,ring.transform.localPosition+Vector3.up*.28f,.12f,.25f,.10f,armorLight,side*22,"Orbit"+s);
            }
        }
        private void ProductionStaff(Color metal)
        {
            var weapon=WeaponPivot("R",new Vector3(.52f,-.05f,.18f));
            Mesh shaft=HeroMeshLibrary.Loft("DF_OrganicStaff",new[]{-.84f,-.53f,.16f,.74f,.9f},new[]{.022f,.04f,.035f,.045f,.02f},new[]{.025f,.04f,.035f,.04f,.025f},new[]{0f,.015f,0f,.05f,.08f});
            Sculpt("Staff",shaft,Vector3.zero,Vector3.one,metal,weapon);
            var ring=Sculpt("Emitter",HeroMeshLibrary.Ring("DF_StaffCrown",.19f,.035f),new Vector3(0,.80f,.06f),Vector3.one,accent,weapon,true);ring.transform.localRotation=Quaternion.Euler(70,0,0);
            Mesh seed=HeroMeshLibrary.Loft("DF_LifeSeed",new[]{-.15f,-.02f,.08f,.18f},new[]{.008f,.08f,.055f,.002f},new[]{.006f,.06f,.05f,.002f});
            Sculpt("EmitterLens",seed,new Vector3(0,.80f,.06f),Vector3.one,accent,weapon,true);Bind("WeaponR","ArmR");
        }
        private void ProductionGauntlets(Color metal)
        {
            Mesh gauntlet=HeroMeshLibrary.Loft("DF_PowerGauntlet",new[]{-.24f,-.18f,.02f,.23f,.31f},new[]{.16f,.23f,.24f,.19f,.12f},new[]{.18f,.23f,.24f,.19f,.12f});
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";Vector3 p=new Vector3(side*.53f,-.15f,.18f);
                Sculpt("Gauntlet"+s,gauntlet,p,Vector3.one,dark);
                for(int k=0;k<3;k++)Detail("Knuckle"+s+k,p+new Vector3((k-1)*.125f,-.10f,.23f),new Vector3(.10f,.17f,.075f),metal,default,"Gauntlet"+s);
                Detail("GauntletReactor"+s,p+new Vector3(0,.11f,.242f),new Vector3(.25f,.075f,.025f),accent,default,"Gauntlet"+s,true);
                Bind("Gauntlet"+s,"Arm"+s);
            }
        }
        private void ProductionCoat(bool longCoat)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Mesh clothMesh=HeroMeshLibrary.Ribbon("DF_CoatTail"+side+(longCoat?"Long":"Short"),new[]{new Vector3(side*.18f,.16f,-.15f),new Vector3(side*.24f,-.24f,-.20f),new Vector3(side*.34f,-.59f,-.27f),new Vector3(side*.46f,longCoat?-.92f:-.73f,-.35f)},new[]{.10f,.15f,.16f,.06f},.025f);
                Sculpt(side<0?"Cape":"CoatTailR",clothMesh,Vector3.zero,Vector3.one,side<0?cloth:Color.Lerp(accent,cloth,.42f));
            }
        }
    }
}
