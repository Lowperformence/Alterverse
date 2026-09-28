using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // First complete model in the polished style. The named animation/weapon pivots
        // use the same coordinate contract as the existing marksman and its cosmetics.
        private void BuildNyraPolishedScout()
        {
            dark=new Color(.045f,.065f,.10f);
            Color pearl=new(.69f,.77f,.83f),steel=new(.24f,.32f,.40f);
            Color red=new(.96f,.055f,.035f),hot=new(1f,.34f,.16f);
            Mesh torso=HeroMeshLibrary.Loft("DF_ScoutTorsoV1",
                new[]{-.36f,-.23f,-.08f,.08f,.24f,.38f,.46f,.53f},
                new[]{.22f,.23f,.18f,.185f,.25f,.30f,.25f,.10f},
                new[]{.125f,.14f,.12f,.125f,.17f,.18f,.13f,.08f},null,24);
            Sculpt("Torso",torso,Vector3.zero,Vector3.one,dark);
            ScoutShell("ChestCarapace",new Vector3(0,.265f,.015f),
                new Vector3(.285f,.245f,.185f),pearl);
            ScoutShell("AbdominalSeal",new Vector3(0,-.035f,.025f),
                new Vector3(.172f,.17f,.126f),steel);
            ScoutShell("HipCarapace",new Vector3(0,-.255f,0),
                new Vector3(.232f,.12f,.145f),steel);
            ScoutShell("Collar",new Vector3(0,.49f,0),new Vector3(.12f,.065f,.105f),steel);
            ScoutShell("Neck",new Vector3(0,.59f,0),new Vector3(.072f,.11f,.07f),dark);
            ScoutShell("IdentityCore",new Vector3(0,.275f,.197f),
                new Vector3(.031f,.11f,.016f),red,null,true);
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Mesh leg=HeroMeshLibrary.Loft("DF_ScoutLegV1",
                    new[]{-1f,-.80f,-.55f,-.25f,.02f,.28f,.65f,1f},
                    new[]{.056f,.07f,.084f,.072f,.073f,.10f,.115f,.105f},
                    new[]{.064f,.079f,.085f,.077f,.078f,.10f,.12f,.115f},null,20);
                Sculpt("Leg"+suffix,leg,new Vector3(side*.155f,-.61f,0),new Vector3(1,.35f,1),dark);
                ScoutShell("ThighPlate"+suffix,new Vector3(side*.155f,-.40f,.087f),
                    new Vector3(.091f,.16f,.054f),pearl);
                Bind("ThighPlate"+suffix,"Leg"+suffix);
                ScoutShell("Knee"+suffix,new Vector3(side*.155f,-.59f,.077f),
                    new Vector3(.078f,.072f,.045f),steel);
                Bind("Knee"+suffix,"Leg"+suffix);
                ScoutShell("Greave"+suffix,new Vector3(side*.155f,-.77f,.060f),
                    new Vector3(.077f,.15f,.047f),pearl);
                Bind("Greave"+suffix,"Leg"+suffix);
                ScoutShell("GreaveEnergy"+suffix,new Vector3(side*.155f,-.76f,.108f),
                    new Vector3(.012f,.10f,.01f),red,null,true);
                Bind("GreaveEnergy"+suffix,"Leg"+suffix);
                Mesh boot=HeroMeshLibrary.Loft("DF_ScoutBootV1",
                    new[]{-.06f,-.035f,.025f,.075f,.13f},
                    new[]{.078f,.089f,.085f,.068f,.052f},
                    new[]{.13f,.165f,.15f,.10f,.064f},new[]{.06f,.07f,.06f,.025f,0f},20);
                Sculpt("Boot"+suffix,boot,new Vector3(side*.155f,-.95f,.015f),Vector3.one,steel);
                Transform arm=new GameObject("DF_Arm"+suffix).transform;
                arm.SetParent(root,false);arm.localPosition=new Vector3(side*.32f,.40f,.015f);
                ScoutShell("UpperArm"+suffix,new Vector3(0,-.135f,0),new Vector3(.079f,.19f,.083f),dark,arm);
                ScoutShell("Shoulder"+suffix,new Vector3(side*.024f,.015f,0),
                    new Vector3(side<0?.145f:.117f,.14f,.14f),pearl,arm);
                ScoutShell("Vambrace"+suffix,new Vector3(0,-.43f,.025f),new Vector3(.082f,.155f,.088f),steel,arm);
                ScoutShell("ForearmPlate"+suffix,new Vector3(0,-.41f,.082f),new Vector3(.067f,.14f,.037f),pearl,arm);
                ScoutShell("ForearmEnergy"+suffix,new Vector3(0,-.40f,.12f),new Vector3(.012f,.09f,.009f),red,arm,true);
                ScoutShell("Glove"+suffix,new Vector3(0,-.605f,.042f),new Vector3(.062f,.083f,.070f),dark,arm);
                arm.localRotation=Quaternion.Euler(side<0?-42f:-48f,0,side<0?42f:-10f);
            }
            // Exposed face and compact hood make Nyra read as a person and a field ranger,
            // while the single brow optic keeps the sci-fi identity distinct.
            Vector3 facePos=new(0,.84f,.015f);
            Mesh face=HeroMeshLibrary.Loft("DF_ScoutFaceV2",
                new[]{-.21f,-.15f,-.06f,.04f,.13f,.20f,.24f},
                new[]{.036f,.092f,.128f,.145f,.14f,.105f,.035f},
                new[]{.035f,.083f,.115f,.128f,.12f,.09f,.03f},
                new[]{.035f,.024f,.012f,0f,-.006f,-.012f,-.018f},24);
            Color skin=new(.61f,.39f,.29f),hair=new(.035f,.050f,.078f);
            Sculpt("Head",face,facePos,Vector3.one,skin);
            ScoutShell("RangerHood",new Vector3(0,.91f,-.075f),new Vector3(.22f,.28f,.14f),dark);
            ScoutShell("HoodCrown",new Vector3(0,1.075f,-.015f),new Vector3(.145f,.075f,.115f),steel);
            ScoutShell("HairCap",new Vector3(0,1.015f,.005f),new Vector3(.135f,.09f,.12f),hair);
            ScoutShell("CheekGuard",new Vector3(.105f,.79f,.105f),new Vector3(.055f,.095f,.035f),steel);
            ScoutShell("BrowOptic",new Vector3(-.070f,.886f,.139f),new Vector3(.087f,.028f,.024f),red,null,true);
            ScoutShell("OpticLens",new Vector3(-.108f,.886f,.16f),new Vector3(.027f,.026f,.018f),hot,null,true);
            ScoutShell("EyeL",new Vector3(-.064f,.865f,.139f),new Vector3(.022f,.012f,.010f),dark);
            ScoutShell("EyeR",new Vector3(.064f,.865f,.139f),new Vector3(.022f,.012f,.010f),dark);
            ScoutShell("Comms",new Vector3(-.166f,.85f,-.005f),new Vector3(.040f,.073f,.076f),steel);
            for(int side=-1;side<=1;side+=2)
            {
                Mesh hoodEdge=HeroMeshLibrary.Ribbon("DF_ScoutHoodEdge"+side,
                    new[]{new Vector3(side*.15f,1.02f,-.05f),new Vector3(side*.20f,.91f,.015f),
                        new Vector3(side*.19f,.74f,-.01f),new Vector3(side*.24f,.59f,-.12f)},
                    new[]{.023f,.030f,.022f,.003f},.012f);
                Sculpt("HoodEdge"+side,hoodEdge,Vector3.zero,Vector3.one,pearl);
            }
            Mesh scarf=HeroMeshLibrary.Ribbon("DF_ScoutScarfV1",
                new[]{new Vector3(-.16f,.45f,-.14f),new Vector3(-.22f,.12f,-.20f),
                    new Vector3(-.27f,-.21f,-.23f),new Vector3(-.34f,-.49f,-.27f),new Vector3(-.42f,-.70f,-.30f)},
                new[]{.13f,.14f,.125f,.085f,.008f},.018f);
            Sculpt("Cape",scarf,Vector3.zero,Vector3.one,dark);
            BuildNyraPolishedRifle(steel,pearl,red,hot);
        }

        private GameObject ScoutShell(string name,Vector3 position,Vector3 scale,Color color,Transform parent=null,bool energy=false)
        {
            Mesh shell=HeroMeshLibrary.Loft("DF_ScoutShellV1",
                new[]{-1f,-.85f,-.58f,-.2f,.2f,.58f,.85f,1f},
                new[]{.18f,.58f,.87f,1f,1f,.87f,.58f,.18f},
                new[]{.18f,.58f,.87f,1f,1f,.87f,.58f,.18f},null,20);
            return Sculpt(name,shell,position,scale,color,parent,energy);
        }

        private void BuildNyraPolishedRifle(Color steel,Color pearl,Color red,Color hot)
        {
            Transform weapon=WeaponPivot("R",new Vector3(.27f,.08f,.37f));
            weapon.localRotation=Quaternion.Euler(-2f,-3f,-2f);
            GameObject Along(string name,Vector3 position,Vector3 scale,Color color,bool energy=false)
            {
                GameObject part=ScoutShell(name,position,scale,color,weapon,energy);
                part.transform.localRotation=Quaternion.Euler(90,0,0);return part;
            }
            Along("Rifle",new Vector3(0,0,.19f),new Vector3(.103f,.60f,.09f),steel);
            Along("RifleStock",new Vector3(0,-.015f,-.31f),new Vector3(.095f,.21f,.105f),dark);
            Along("RifleReceiver",new Vector3(0,.024f,.25f),new Vector3(.112f,.36f,.07f),pearl);
            Along("RifleBarrel",new Vector3(0,.015f,.93f),new Vector3(.033f,.36f,.033f),steel);
            Along("MuzzleBrake",new Vector3(0,.015f,1.28f),new Vector3(.065f,.10f,.065f),pearl);
            Along("MuzzleEmitter",new Vector3(0,.015f,1.365f),new Vector3(.038f,.025f,.038f),hot,true);
            Along("Scope",new Vector3(0,.18f,.17f),new Vector3(.058f,.20f,.055f),dark);
            Along("ScopeLens",new Vector3(0,.18f,.366f),new Vector3(.041f,.013f,.041f),hot,true);
            Along("ScopeBridge",new Vector3(0,.105f,.17f),new Vector3(.036f,.12f,.034f),steel);
            Along("WeaponRail",new Vector3(-.105f,.027f,.33f),new Vector3(.014f,.39f,.018f),red,true);
            Along("RifleSensorGlow",new Vector3(.105f,.027f,.33f),new Vector3(.014f,.39f,.018f),red,true);
            ScoutShell("RifleMagazine",new Vector3(0,-.14f,.08f),new Vector3(.066f,.14f,.075f),dark,weapon);
            ScoutShell("RifleForegrip",new Vector3(0,-.13f,.55f),new Vector3(.048f,.115f,.050f),dark,weapon);

            // Compact controls follow the rifle, not the hero's torso or off-hand.
            Create("ScopeAdjustmentDial",PrimitiveType.Cylinder,new Vector3(.067f,.18f,.12f),
                new Vector3(.048f,.014f,.048f),steel,new Vector3(0,0,90),weapon);
            Create("ScopeTopDial",PrimitiveType.Cylinder,new Vector3(0,.237f,.12f),
                new Vector3(.049f,.012f,.049f),steel,default,weapon);
            Create("RifleTriggerGuardBase",PrimitiveType.Cube,new Vector3(0,-.20f,-.13f),
                new Vector3(.048f,.027f,.15f),steel,default,weapon);
            Create("RifleTriggerGuardRear",PrimitiveType.Cube,new Vector3(0,-.15f,-.20f),
                new Vector3(.048f,.12f,.025f),steel,default,weapon);
            for(int rib=0;rib<3;rib++)
                Create("RifleForegripRib"+rib,PrimitiveType.Cube,new Vector3(0,-.075f-rib*.055f,.59f),
                    new Vector3(.083f,.012f,.018f),steel,default,weapon);
            Transform muzzle=new GameObject("DF_Muzzle").transform;
            muzzle.SetParent(weapon,false);muzzle.localPosition=new Vector3(0,.015f,1.41f);
            Bind("WeaponR","ArmR");
        }
    }
}
