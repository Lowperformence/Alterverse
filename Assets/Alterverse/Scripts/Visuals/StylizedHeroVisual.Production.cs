using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // Model coordinates preserve the existing collider, camera framing and foot planting contract.
        private void BuildProductionHero()
        {
            HeroId h=identity.Hero;
            if(h==HeroId.Veyr)
            {
                BuildVeyrShadowAssassin();
                CreateSignature();
                return;
            }
            if(h==HeroId.Rook)
            {
                BuildBrakkIndustrialBruiser();
                CreateSignature();
                return;
            }
            if(h==HeroId.Elyra)
            {
                BuildIoneVoidMage();
                CreateSignature();
                return;
            }
            if(h==HeroId.Nyra)
            {
                BuildNyraPolishedScout();
                CreateSignature();
                return;
            }
            if(h==HeroId.Mira)
            {
                BuildTahlaAscendantHealer();
                CreateSignature();
                return;
            }
            if(h==HeroId.Traveler)
            {
                BuildTravelerTimeSkirmisher();
                CreateSignature();
                return;
            }
            bool tank=h==HeroId.Kaelon,bruiser=h==HeroId.Rook,heavy=tank||bruiser;
            float breadth=tank?1.60f:bruiser?1.32f:h==HeroId.Mira?.91f:.86f;
            // Kaelon follows the supplied heavy-hammer reference: pale worn plate over a warm dark undersuit.
            Color trim=tank?new Color(.30f,.235f,.17f):Color.Lerp(accent,dark,.28f);
            Color metal=tank?new Color(.58f,.60f,.60f):h==HeroId.Mira?new Color(.72f,.77f,.70f):new Color(.29f,.33f,.40f);
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
                Plate("Shoulder"+s,new Vector3(side*(tank?.58f:heavy?.53f:.36f),tank?.46f:.43f,0),
                    tank?.3696f:heavy?.52f:.29f,tank?.2576f:heavy?.38f:.24f,tank?.3472f:heavy?.50f:.31f,
                    tank?metal:(side<0?identityPanel:trim),side*(tank?-11:-16));
                Plate("Tasset"+s,new Vector3(side*.20f*breadth,-.31f,.15f),.20f*breadth,.30f,.08f,metal,side*9);
            }
            BuildProductionHead(h,metal,trim);
            if(tank)BuildKaelonReferenceArmor(metal,trim);
            switch(h)
            {
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
        private void BuildTahlaAscendantHealer()
        {
            // Reference-driven ascendant healer: luminous white/silver robe, cyan holy energy,
            // elegant proportions, a halo language and a long ceremonial healing staff.
            Color white=new(.80f,.74f,.64f,1f);
            Color pearl=new(.63f,.72f,.57f,1f);
            Color silver=new(.34f,.48f,.29f,1f);
            Color cyan=new(.32f,.86f,.18f,1f);
            Color cyanHot=new(.75f,1f,.48f,1f);
            Color gold=new(.90f,.60f,.21f,1f);
            Color shadow=new(.12f,.085f,.11f,1f);
            Color hair=new(.36f,.19f,.15f,1f);
            Color skin=new(.73f,.55f,.48f,1f);
            Color skinShade=new(.58f,.39f,.37f,1f);
            Color eyeWhite=new(.83f,.78f,.72f,1f);
            Color iris=new(.34f,.44f,.42f,1f);

            Mesh torso=HeroMeshLibrary.Loft("DF_TahlaAscendantTorso",
                new[]{-.38f,-.24f,-.05f,.17f,.35f,.49f},
                new[]{.17f,.20f,.25f,.31f,.32f,.20f},
                new[]{.12f,.14f,.17f,.20f,.20f,.13f},
                new[]{.01f,.01f,.015f,.018f,.01f,0f},16);
            Sculpt("Torso",torso,new Vector3(0,.05f,0),new Vector3(.94f,1f,.94f),pearl);
            Plate("TahlaChest",new Vector3(0,.23f,.195f),.56f,.43f,.095f,white);
            Plate("TahlaChestInset",new Vector3(0,.22f,.252f),.34f,.29f,.030f,silver);
            Plate("TahlaHeartFrame",new Vector3(0,.23f,.288f),.22f,.27f,.034f,gold);
            for(int side=-1;side<=1;side+=2)
            {
                Plate("TahlaCorsetRib"+side,new Vector3(side*.19f,-.05f,.20f),
                    .19f,.34f,.07f,pearl,side*17);
                Detail("TahlaCorsetTrim"+side,new Vector3(side*.23f,-.05f,.246f),
                    new Vector3(.024f,.29f,.018f),gold,new Vector3(0,0,side*15),null,true);
            }
            Detail("TahlaChestLight",new Vector3(0,.25f,.279f),new Vector3(.075f,.16f,.020f),cyanHot,new Vector3(0,0,45),null,true);
            Plate("TahlaWaist",new Vector3(0,-.12f,.145f),.40f,.22f,.075f,silver);
            Detail("TahlaWaistGem",new Vector3(0,-.21f,.196f),new Vector3(.075f,.085f,.022f),cyanHot,new Vector3(0,0,45),null,true);

            Mesh pelvis=HeroMeshLibrary.Loft("DF_TahlaPelvis",
                new[]{-.43f,-.30f,-.18f},new[]{.20f,.25f,.21f},new[]{.12f,.14f,.12f});
            Sculpt("Pelvis",pelvis,new Vector3(0,-.02f,0),new Vector3(.96f,1f,.94f),shadow);

            // Slim hidden legs preserve the existing movement rig beneath the robe.
            float legX=.16f;
            Mesh leg=HeroMeshLibrary.Loft("DF_TahlaLeg",
                new[]{-1f,-.76f,-.20f,.12f,.63f,1f},
                new[]{.050f,.060f,.070f,.090f,.10f,.080f},
                new[]{.060f,.068f,.078f,.092f,.095f,.078f},
                new[]{.012f,.010f,0f,.003f,0f,0f});
            Mesh boot=HeroMeshLibrary.Loft("DF_TahlaBoot",
                new[]{-.08f,-.03f,.07f,.15f},
                new[]{.085f,.095f,.080f,.050f},
                new[]{.135f,.16f,.12f,.070f},
                new[]{.05f,.055f,.035f,0f});
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Sculpt("Leg"+s,leg,new Vector3(side*legX,-.62f,0),new Vector3(.94f,.34f,.94f),shadow);
                Sculpt("Boot"+s,boot,new Vector3(side*legX,-.93f,.015f),Vector3.one,silver);

                Transform arm=new GameObject("DF_Arm"+s).transform;
                arm.SetParent(root,false);
                arm.localPosition=new Vector3(side*.33f,.39f,.02f);
                arm.localRotation=Quaternion.Euler(-10f,0,side*-10f);

                Mesh upper=HeroMeshLibrary.Loft("DF_TahlaUpperArm",
                    new[]{-.31f,-.24f,-.05f,.04f},
                    new[]{.052f,.070f,.092f,.070f},
                    new[]{.058f,.074f,.090f,.068f});
                Sculpt("UpperArm"+s,upper,Vector3.zero,Vector3.one,white,arm);

                Mesh fore=HeroMeshLibrary.Loft("DF_TahlaForearm",
                    new[]{-.27f,-.20f,-.05f,.025f},
                    new[]{.052f,.078f,.088f,.060f},
                    new[]{.055f,.078f,.090f,.060f});
                Sculpt("Vambrace"+s,fore,new Vector3(0,-.30f,.02f),Vector3.one,silver,arm);

                Mesh glove=HeroMeshLibrary.Loft("DF_TahlaGlove",
                    new[]{-.13f,-.07f,.07f,.11f},
                    new[]{.040f,.065f,.070f,.050f},
                    new[]{.045f,.060f,.066f,.046f});
                Sculpt("Glove"+s,glove,new Vector3(0,-.59f,.052f),Vector3.one,white,arm);

                Plate("TahlaShoulder"+s,new Vector3(side*.35f,.43f,.01f),.24f,.20f,.24f,white,side*-14);
                Bind("TahlaShoulder"+s,"Arm"+s);
                Plate("TahlaShoulderGold"+s,new Vector3(side*.43f,.53f,.035f),
                    .34f,.19f,.32f,gold,side*-24);
                Bind("TahlaShoulderGold"+s,"Arm"+s);
                Plate("TahlaShoulderWing"+s,new Vector3(side*.55f,.45f,-.035f),
                    .19f,.37f,.11f,gold,side*-26);
                Bind("TahlaShoulderWing"+s,"Arm"+s);
                Detail("TahlaShoulderGem"+s,new Vector3(side*.41f,.54f,.217f),
                    new Vector3(.072f,.105f,.022f),cyanHot,new Vector3(0,0,45),null,true);
                Bind("TahlaShoulderGem"+s,"Arm"+s);
                Detail("TahlaWristLight"+s,new Vector3(0,-.39f,.105f),new Vector3(.055f,.12f,.020f),cyanHot,
                    new Vector3(0,0,side*8),"Arm"+s,true);
            }

            // Long layered robe panels create the tall priestess silhouette without cloth simulation.
            Mesh robeFront=HeroMeshLibrary.Ribbon("DF_TahlaRobeFront",
                new[]{new Vector3(0,-.16f,.11f),new Vector3(0,-.40f,.14f),new Vector3(0,-.70f,.16f),new Vector3(0,-1.00f,.18f)},
                new[]{.22f,.28f,.31f,.24f},.030f);
            Sculpt("TahlaRobeFront",robeFront,Vector3.zero,Vector3.one,white);
            Mesh robeBack=HeroMeshLibrary.Ribbon("DF_TahlaRobeBack",
                new[]{new Vector3(0,-.14f,-.11f),new Vector3(0,-.42f,-.16f),new Vector3(0,-.73f,-.20f),new Vector3(0,-1.02f,-.22f)},
                new[]{.20f,.29f,.32f,.23f},.025f);
            Sculpt("TahlaRobeBack",robeBack,Vector3.zero,Vector3.one,pearl);
            // A continuous back drape hangs from the shoulders like a robe, with a narrow hem.
            Mesh healerCape=HeroMeshLibrary.Ribbon("DF_TahlaBackDrape",
                new[]{new Vector3(0,.42f,-.19f),new Vector3(0,.02f,-.24f),
                    new Vector3(0,-.48f,-.29f),new Vector3(0,-.99f,-.34f)},
                new[]{.26f,.34f,.38f,.37f},.030f);
            Sculpt("TahlaBackDrape",healerCape,Vector3.zero,Vector3.one,pearl);
            // Seams stay close to the drape instead of projecting outward as wings.
            for(int side=-1;side<=1;side+=2)
            {
                Mesh veil=HeroMeshLibrary.Ribbon("DF_TahlaGoldenVeil"+side,
                    new[]{new Vector3(side*.23f,.40f,-.20f),new Vector3(side*.27f,.02f,-.255f),
                        new Vector3(side*.31f,-.53f,-.305f),new Vector3(side*.30f,-.96f,-.355f)},
                    new[]{.055f,.075f,.075f,.060f},.022f);
                Sculpt("TahlaGoldenVeil"+side,veil,Vector3.zero,Vector3.one,white);
                Mesh veilEdge=HeroMeshLibrary.Ribbon("DF_TahlaVeilEdge"+side,
                    new[]{new Vector3(side*.28f,.31f,-.22f),new Vector3(side*.34f,-.04f,-.27f),
                        new Vector3(side*.38f,-.55f,-.32f),new Vector3(side*.37f,-.96f,-.37f)},
                    new[]{.012f,.015f,.017f,.016f},.008f);
                Sculpt("TahlaVeilEdge"+side,veilEdge,Vector3.zero,Vector3.one,gold,null,true);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Mesh sideRobe=HeroMeshLibrary.Ribbon("DF_TahlaRobeSide"+side,
                    new[]{new Vector3(side*.16f,-.18f,.02f),new Vector3(side*.25f,-.45f,.02f),
                          new Vector3(side*.30f,-.74f,.00f),new Vector3(side*.24f,-1.00f,-.02f)},
                    new[]{.09f,.13f,.14f,.08f},.022f);
                Sculpt("TahlaRobeSide"+(side<0?"L":"R"),sideRobe,Vector3.zero,Vector3.one,silver);
            }
            Detail("TahlaRobeLine",new Vector3(0,-.61f,.205f),new Vector3(.060f,.50f,.018f),gold,default,null,true);
            Plate("TahlaHipGuardL",new Vector3(-.27f,-.28f,.16f),.21f,.30f,.10f,gold,-16);
            Plate("TahlaHipGuardR",new Vector3(.27f,-.28f,.16f),.21f,.30f,.10f,gold,16);
            Detail("TahlaRobeGold",new Vector3(0,-.91f,.205f),new Vector3(.34f,.035f,.018f),gold);

            // Rounded cheekbones, a tapered jaw and separate facial features make the
            // portrait read as a person even at the smaller in-game camera distance.
            Vector3 hp=new(0,.80f,.015f);
            Mesh head=HeroMeshLibrary.Loft("DF_TahlaFace",
                new[]{-.23f,-.20f,-.14f,-.05f,.05f,.14f,.21f,.25f},
                new[]{.035f,.075f,.115f,.156f,.171f,.162f,.118f,.035f},
                new[]{.040f,.078f,.108f,.139f,.142f,.132f,.095f,.030f},
                new[]{.055f,.049f,.037f,.018f,.006f,-.012f,-.027f,-.035f},20);
            Sculpt("Head",head,hp,Vector3.one,skin);
            Sculpt("Neck",HeroMeshLibrary.Loft("DF_TahlaNeck",new[]{-.09f,.08f},new[]{.066f,.075f},new[]{.062f,.068f}),
                new Vector3(0,.55f,0),Vector3.one,skin);
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Create("TahlaEar"+s,PrimitiveType.Sphere,hp+new Vector3(side*.165f,-.025f,-.005f),
                    new Vector3(.040f,.079f,.048f),skin);
                // Non-emissive sclera, iris and pupil replace the bright square eye lights.
                Create("TahlaEyeWhite"+s,PrimitiveType.Sphere,hp+new Vector3(side*.071f,.035f,.148f),
                    new Vector3(.079f,.034f,.018f),eyeWhite);
                Create("TahlaIris"+s,PrimitiveType.Sphere,hp+new Vector3(side*.071f,.035f,.164f),
                    new Vector3(.030f,.031f,.011f),iris);
                Create("TahlaPupil"+s,PrimitiveType.Sphere,hp+new Vector3(side*.071f,.035f,.173f),
                    new Vector3(.013f,.020f,.007f),shadow);
                Detail("TahlaUpperLid"+s,hp+new Vector3(side*.071f,.057f,.160f),
                    new Vector3(.082f,.008f,.009f),skinShade,new Vector3(0,0,side*-5));
                Detail("TahlaBrow"+s,hp+new Vector3(side*.072f,.091f,.140f),
                    new Vector3(.080f,.012f,.012f),hair,new Vector3(0,0,side*-7));
            }
            Sculpt("TahlaNose",HeroMeshLibrary.Loft("DF_TahlaNose",
                new[]{-.055f,-.025f,.030f,.085f},
                new[]{.023f,.027f,.018f,.012f},
                new[]{.025f,.030f,.022f,.014f}),
                hp+new Vector3(0,-.028f,.151f),Vector3.one,skin);
            Create("TahlaNoseTip",PrimitiveType.Sphere,hp+new Vector3(0,-.078f,.181f),
                new Vector3(.046f,.027f,.028f),skin);
            Detail("TahlaMouth",hp+new Vector3(0,-.145f,.137f),
                new Vector3(.082f,.008f,.011f),skinShade);
            Create("TahlaLowerLip",PrimitiveType.Sphere,hp+new Vector3(0,-.158f,.140f),
                new Vector3(.064f,.012f,.012f),skin);

            Mesh cap=HeroMeshLibrary.Loft("DF_TahlaHairCap",
                new[]{.01f,.12f,.21f,.28f},new[]{.17f,.18f,.145f,.03f},
                new[]{.14f,.15f,.13f,.04f},new[]{-.055f,-.043f,-.020f,-.014f});
            Sculpt("Hair",cap,hp,Vector3.one,hair);
            Mesh hairSweep=HeroMeshLibrary.Ribbon("DF_TahlaHairSweep",
                new[]{new Vector3(-.12f,1.02f,-.06f),new Vector3(.08f,.91f,-.13f),
                    new Vector3(.36f,.79f,-.18f),new Vector3(.66f,.71f,-.25f)},
                new[]{.10f,.13f,.13f,.045f},.032f);
            Sculpt("TahlaHairSweep",hairSweep,Vector3.zero,Vector3.one,hair);
            for(int side=-1;side<=1;side+=2)
            {
                Mesh strand=HeroMeshLibrary.Ribbon("DF_TahlaHairLock"+side,
                    new[]{new Vector3(side*.11f,.97f,.01f),new Vector3(side*.17f,.80f,.06f),
                          new Vector3(side*.18f,.61f,.00f),new Vector3(side*.15f,.45f,-.05f)},
                    new[]{.050f,.045f,.032f,.008f},.020f);
                Sculpt("TahlaHairLock"+(side<0?"L":"R"),strand,Vector3.zero,Vector3.one,hair);
            }
            GameObject halo=Sculpt("TahlaHalo",HeroMeshLibrary.Ring("DF_TahlaHaloMesh",.25f,.020f),
                hp+new Vector3(0,.15f,-.10f),Vector3.one,cyan,null,true);
            halo.transform.localRotation=Quaternion.Euler(76f,0f,0f);

            // No permanent floor ring: spells remain legible against the map.

            BuildTahlaLightStaff(silver,white,gold,cyan,cyanHot);
            GameObject spellFocus=Sculpt("TahlaSpellFocus",
                HeroMeshLibrary.Ring("DF_TahlaSpellFocusMesh",.22f,.024f),
                new Vector3(-.60f,-.04f,.37f),Vector3.one,cyan,null,true);
            spellFocus.transform.localRotation=Quaternion.Euler(68f,0,-20f);
            Detail("TahlaSpellCore",new Vector3(-.60f,-.04f,.39f),
                new Vector3(.10f,.10f,.032f),cyanHot,new Vector3(0,0,45),null,true);
            Detail("IdentityCore",new Vector3(0,.25f,.284f),new Vector3(.065f,.13f,.022f),cyanHot,new Vector3(0,0,45),null,true);
        }

        private void BuildTahlaLightStaff(Color silver,Color white,Color gold,Color cyan,Color cyanHot)
        {
            Transform weapon=WeaponPivot("R",new Vector3(.49f,-.03f,.18f));
            weapon.localRotation=Quaternion.Euler(0,-4f,-8f);

            Mesh shaft=HeroMeshLibrary.Loft("DF_TahlaLightStaff",
                new[]{-.88f,-.60f,.05f,.64f,.87f},
                new[]{.024f,.036f,.033f,.040f,.020f},
                new[]{.026f,.038f,.034f,.040f,.021f},
                new[]{0f,.008f,.00f,.035f,.055f});
            Sculpt("Staff",shaft,Vector3.zero,Vector3.one,silver,weapon);

            Create("StaffLowerCap",PrimitiveType.Cube,new Vector3(0,-.83f,.01f),new Vector3(.10f,.12f,.10f),gold,default,weapon);
            Create("StaffGrip",PrimitiveType.Cube,new Vector3(0,-.18f,.01f),new Vector3(.075f,.28f,.075f),white,default,weapon);
            // Raised ferrules define the grip without covering the ceremonial staff head.
            for(int band=0;band<3;band++)
                Sculpt("StaffGripFerrule"+band,HeroMeshLibrary.Ring("DF_StaffGripFerrule",.043f,.007f),
                    new Vector3(0,-.27f+band*.09f,.01f),Vector3.one,gold,weapon);
            Sculpt("StaffCrownSocket",HeroMeshLibrary.Loft("DF_StaffCrownSocket",
                new[]{-.07f,0f,.07f},new[]{.042f,.067f,.050f},new[]{.042f,.067f,.050f}),
                new Vector3(0,.56f,.03f),Vector3.one,gold,weapon);


            GameObject crown=Sculpt("Emitter",HeroMeshLibrary.Ring("DF_TahlaStaffCrown",.23f,.028f),
                new Vector3(0,.77f,.055f),Vector3.one,cyan,weapon,true);
            crown.transform.localRotation=Quaternion.Euler(70f,0f,0f);

            GameObject crown2=Sculpt("StaffCrownInner",HeroMeshLibrary.Ring("DF_TahlaStaffInner",.14f,.018f),
                new Vector3(0,.77f,.060f),Vector3.one,white,weapon,true);
            crown2.transform.localRotation=Quaternion.Euler(70f,0f,35f);

            Mesh crystal=HeroMeshLibrary.Loft("DF_TahlaStaffCrystal",
                new[]{-.16f,-.06f,.05f,.17f},
                new[]{.010f,.085f,.060f,.004f},
                new[]{.010f,.068f,.052f,.004f});
            Sculpt("EmitterLens",crystal,new Vector3(0,.77f,.065f),new Vector3(1.15f,1.20f,1.10f),cyanHot,weapon,true);
            GameObject halo=Sculpt("StaffCrownOuter",HeroMeshLibrary.Ring("DF_TahlaStaffOuter",.31f,.014f),
                new Vector3(0,.77f,.055f),Vector3.one,gold,weapon);
            halo.transform.localRotation=Quaternion.Euler(70f,0f,-23f);

            for(int side=-1;side<=1;side+=2)
            {
                Create("StaffWing"+(side<0?"L":"R"),PrimitiveType.Cube,
                    new Vector3(side*.18f,.70f,.035f),new Vector3(.055f,.24f,.055f),gold,
                    new Vector3(0,0,side*-28f),weapon);
            }

            Bind("WeaponR","ArmR");
        }

        private void BuildNyraEliteMarksman()
        {
            // Reference-driven elite marksman: heavy black armor, oversized shoulders, hooded
            // sensor helmet, red optics, a short tabard and a long precision rifle.
            Color black=new(.025f,.028f,.033f,1f);
            Color charcoal=new(.070f,.078f,.086f,1f);
            Color steel=new(.20f,.22f,.24f,1f);
            Color olive=new(.16f,.18f,.13f,1f);
            Color clothBlack=new(.035f,.038f,.043f,1f);
            Color tabard=new(.63f,.62f,.56f,1f);
            Color brass=new(.48f,.34f,.13f,1f);
            Color red=new(.94f,.035f,.025f,1f);
            Color redHot=new(1f,.16f,.07f,1f);

            Mesh torso=HeroMeshLibrary.Loft("DF_NyraEliteTorso",
                new[]{-.38f,-.22f,-.02f,.20f,.39f,.52f},
                new[]{.25f,.28f,.38f,.48f,.50f,.29f},
                new[]{.17f,.20f,.27f,.33f,.34f,.20f},
                new[]{.01f,.015f,.025f,.035f,.02f,0f},16);
            Sculpt("Torso",torso,new Vector3(0,.05f,0),new Vector3(1.08f,1f,1.04f),black);
            Plate("NyraChestL",new Vector3(-.22f,.24f,.225f),.47f,.43f,.12f,charcoal,-5);
            Plate("NyraChestR",new Vector3(.22f,.24f,.225f),.47f,.43f,.12f,charcoal,5);
            Plate("NyraAbdomen",new Vector3(0,-.08f,.16f),.48f,.28f,.10f,black);
            Plate("NyraGorget",new Vector3(0,.49f,.095f),.48f,.18f,.22f,olive);
            Detail("NyraChestSeal",new Vector3(.12f,.19f,.302f),new Vector3(.085f,.085f,.020f),brass,new Vector3(0,0,45));
            for(int i=0;i<4;i++)
                Detail("NyraAmmo"+i,new Vector3(-.24f+i*.075f,-.02f,.245f),
                    new Vector3(.040f,.12f,.025f),brass,new Vector3(0,0,-6));

            Mesh pelvis=HeroMeshLibrary.Loft("DF_NyraPelvis",
                new[]{-.44f,-.30f,-.18f},new[]{.26f,.33f,.27f},new[]{.15f,.18f,.15f});
            Sculpt("Pelvis",pelvis,new Vector3(0,-.02f,0),new Vector3(1.08f,1f,1.02f),black);

            float legX=.205f;
            Mesh leg=HeroMeshLibrary.Loft("DF_NyraArmoredLeg",
                new[]{-1f,-.76f,-.22f,.12f,.64f,1f},
                new[]{.075f,.10f,.11f,.145f,.16f,.135f},
                new[]{.09f,.105f,.115f,.15f,.15f,.125f},
                new[]{.02f,.018f,.002f,.006f,0f,0f});
            Mesh boot=HeroMeshLibrary.Loft("DF_NyraHeavyBoot",
                new[]{-.08f,-.03f,.075f,.16f},
                new[]{.14f,.155f,.13f,.085f},
                new[]{.22f,.26f,.20f,.11f},
                new[]{.085f,.095f,.06f,0f});

            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Sculpt("Leg"+s,leg,new Vector3(side*legX,-.62f,0),new Vector3(1.08f,.35f,1.06f),black);
                Sculpt("Boot"+s,boot,new Vector3(side*legX,-.93f,.025f),new Vector3(1.08f,1.03f,1.10f),steel);
                Plate("Knee"+s,new Vector3(side*legX,-.57f,.125f),.24f,.24f,.10f,charcoal,side*-3,"Leg"+s);
                Plate("NyraThigh"+s,new Vector3(side*legX,-.39f,.115f),.25f,.31f,.09f,black,side*-7,"Leg"+s);
                Plate("NyraShin"+s,new Vector3(side*legX,-.74f,.105f),.24f,.30f,.085f,charcoal,side*4,"Leg"+s);

                Transform arm=new GameObject("DF_Arm"+s).transform;
                arm.SetParent(root,false);
                arm.localPosition=new Vector3(side*.43f,.40f,.015f);

                Mesh upper=HeroMeshLibrary.Loft("DF_NyraUpperArm",
                    new[]{-.32f,-.25f,-.06f,.04f},
                    new[]{.075f,.105f,.145f,.11f},
                    new[]{.085f,.11f,.14f,.10f});
                Sculpt("UpperArm"+s,upper,Vector3.zero,new Vector3(1.10f,1f,1.06f),black,arm);

                Mesh fore=HeroMeshLibrary.Loft("DF_NyraForearm",
                    new[]{-.27f,-.21f,-.055f,.025f},
                    new[]{.075f,.11f,.125f,.085f},
                    new[]{.08f,.11f,.13f,.085f});
                Sculpt("Vambrace"+s,fore,new Vector3(0,-.30f,.02f),new Vector3(1.08f,1f,1.05f),charcoal,arm);
                Sculpt("Glove"+s,HeroMeshLibrary.Loft("DF_NyraGlove",
                    new[]{-.13f,-.07f,.07f,.11f},new[]{.060f,.095f,.10f,.075f},new[]{.070f,.080f,.090f,.060f}),
                    new Vector3(0,-.59f,.055f),Vector3.one,black,arm);

                arm.localRotation=Quaternion.Euler(side<0?-42f:-48f,0,side<0?42f:-10f);

                // Compact asymmetric shoulder plates: 30% smaller while preserving the elite-sniper silhouette.
                float shoulderX=side*.50f;
                Plate("NyraShoulder"+s,new Vector3(shoulderX,.45f,.005f),
                    side<0?.434f:.385f,side<0?.343f:.301f,side<0?.336f:.301f,
                    side<0?charcoal:black,side*-12f);
                Bind("NyraShoulder"+s,"Arm"+s);
                if(side<0)
                {
                    Plate("NyraShoulderBadge",new Vector3(shoulderX,.45f,.188f),.196f,.203f,.0245f,black,-8,"Arm"+s);
                    Detail("NyraShoulderMark",new Vector3(shoulderX,.45f,.206f),
                        new Vector3(.0385f,.126f,.0126f),tabard,new Vector3(0,0,-22),"Arm"+s);
                }
            }

            // Hood/helmet hybrid: no human face, only red target optics and a compact sensor stack.
            Vector3 hp=new(0,.80f,.02f);
            Mesh helmet=HeroMeshLibrary.Loft("DF_NyraHunterHelmet",
                new[]{-.23f,-.16f,-.05f,.09f,.19f,.27f},
                new[]{.085f,.15f,.19f,.20f,.17f,.07f},
                new[]{.080f,.14f,.16f,.16f,.13f,.06f},
                new[]{.045f,.033f,.016f,0f,-.008f,-.016f},16);
            Sculpt("Head",helmet,hp,new Vector3(1.04f,.98f,1.00f),black);
            Sculpt("Neck",HeroMeshLibrary.Loft("DF_NyraNeck",
                new[]{-.10f,.09f},new[]{.085f,.095f},new[]{.082f,.09f}),
                new Vector3(0,.55f,0),Vector3.one,charcoal);
            Plate("NyraFaceMask",hp+new Vector3(0,-.035f,.155f),.33f,.30f,.075f,black);
            Plate("NyraHoodBrow",hp+new Vector3(0,.16f,.075f),.42f,.23f,.19f,olive);
            Plate("NyraHoodLeft",hp+new Vector3(-.14f,.05f,.015f),.18f,.42f,.20f,olive,-13);
            Plate("NyraHoodRight",hp+new Vector3(.14f,.05f,.015f),.18f,.42f,.20f,olive,13);

            // Multi-lens red optics with one dominant targeting eye.
            Detail("NyraOpticMain",hp+new Vector3(-.045f,.055f,.205f),new Vector3(.105f,.105f,.022f),redHot,default,null,true);
            Detail("NyraOpticAuxA",hp+new Vector3(.065f,.080f,.198f),new Vector3(.055f,.055f,.018f),red,default,null,true);
            Detail("NyraOpticAuxB",hp+new Vector3(.080f,.010f,.196f),new Vector3(.040f,.040f,.018f),red,default,null,true);
            Detail("NyraSensorBlock",hp+new Vector3(-.09f,.20f,-.005f),new Vector3(.14f,.15f,.12f),charcoal,new Vector3(-8,0,-5));
            Detail("NyraAntenna",hp+new Vector3(.10f,.34f,-.015f),new Vector3(.018f,.30f,.018f),steel);
            Detail("NyraAntennaTip",hp+new Vector3(.10f,.50f,-.015f),new Vector3(.035f,.035f,.035f),red,default,null,true);

            // Short front tabard gives the elite-order silhouette without expensive cloth simulation.
            Mesh tabardMesh=HeroMeshLibrary.Ribbon("DF_NyraTabard",
                new[]{new Vector3(0,-.20f,.13f),new Vector3(0,-.42f,.15f),new Vector3(0,-.67f,.17f),new Vector3(0,-.88f,.18f)},
                new[]{.19f,.20f,.18f,.13f},.024f);
            Sculpt("NyraTabard",tabardMesh,Vector3.zero,Vector3.one,tabard);
            Detail("NyraTabardEdge",new Vector3(0,-.71f,.197f),new Vector3(.30f,.030f,.016f),brass);
            Detail("NyraTabardSeal",new Vector3(0,-.60f,.200f),new Vector3(.085f,.085f,.016f),black,new Vector3(0,0,45));

            BuildNyraPrecisionRifle(black,charcoal,steel,red,redHot);
            Detail("IdentityCore",new Vector3(0,.28f,.305f),new Vector3(.055f,.11f,.020f),red,default,null,true);
        }

        private void BuildNyraPrecisionRifle(Color black,Color charcoal,Color steel,Color red,Color redHot)
        {
            Transform weapon=WeaponPivot("R",new Vector3(.27f,.08f,.37f));
            weapon.localRotation=Quaternion.Euler(-2f,-3f,-2f);

            // Long battle-rifle/sniper hybrid with a visibly separate receiver, barrel and stock.
            Mesh receiver=HeroMeshLibrary.Loft("DF_NyraPrecisionReceiver",
                new[]{-.52f,-.36f,-.17f,.26f,.48f,.64f},
                new[]{.105f,.14f,.16f,.12f,.085f,.060f},
                new[]{.12f,.15f,.14f,.095f,.065f,.050f});
            GameObject rifle=Sculpt("Rifle",receiver,new Vector3(0,0,.02f),Vector3.one,charcoal,weapon);
            rifle.transform.localRotation=Quaternion.Euler(90,0,0);

            Create("RifleReceiver",PrimitiveType.Cube,new Vector3(0,.01f,.15f),
                new Vector3(.23f,.19f,.64f),black,default,weapon);
            Create("RifleStock",PrimitiveType.Cube,new Vector3(0,-.02f,-.33f),
                new Vector3(.26f,.23f,.36f),charcoal,new Vector3(-6,0,0),weapon);
            Create("RifleBarrel",PrimitiveType.Cylinder,new Vector3(0,.015f,.75f),
                new Vector3(.060f,.52f,.060f),steel,new Vector3(90,0,0),weapon);
            Create("MuzzleBrake",PrimitiveType.Cube,new Vector3(0,.015f,1.28f),
                new Vector3(.16f,.14f,.22f),black,default,weapon);
            Create("MuzzleCrown",PrimitiveType.Cube,new Vector3(0,.015f,1.345f),
                new Vector3(.085f,.075f,.085f),redHot,default,weapon);
            Create("RifleMagazine",PrimitiveType.Cube,new Vector3(0,-.18f,.10f),
                new Vector3(.15f,.27f,.24f),black,new Vector3(-12,0,0),weapon);

            Create("Scope",PrimitiveType.Cube,new Vector3(0,.22f,.23f),
                new Vector3(.16f,.14f,.48f),black,default,weapon);
            Create("ScopeBridge",PrimitiveType.Cube,new Vector3(0,.12f,.21f),
                new Vector3(.10f,.09f,.26f),steel,default,weapon);
            Create("ScopeLens",PrimitiveType.Cube,new Vector3(0,.22f,.49f),
                new Vector3(.095f,.085f,.022f),redHot,default,weapon);
            Transform lens=FindVisual("ScopeLens");if(lens!=null)ApplyGlow(lens,MaterialFor(redHot,true));

            Create("WeaponRail",PrimitiveType.Cube,new Vector3(0,.12f,.62f),
                new Vector3(.055f,.035f,.70f),steel,default,weapon);
            Create("RifleForegrip",PrimitiveType.Cube,new Vector3(0,-.12f,.57f),
                new Vector3(.11f,.20f,.14f),black,new Vector3(8,0,0),weapon);
            Create("RifleSensor",PrimitiveType.Cube,new Vector3(.12f,.12f,.40f),
                new Vector3(.07f,.10f,.16f),charcoal,default,weapon);
            Create("RifleSensorGlow",PrimitiveType.Cube,new Vector3(.158f,.12f,.43f),
                new Vector3(.018f,.055f,.055f),red,default,weapon);
            Transform sensorGlow=FindVisual("RifleSensorGlow");if(sensorGlow!=null)ApplyGlow(sensorGlow,MaterialFor(red,true));

            var muzzle=new GameObject("DF_Muzzle").transform;
            muzzle.SetParent(weapon,false);
            muzzle.localPosition=new Vector3(0,.015f,1.41f);

            Bind("WeaponR","ArmR");
        }

        private void BuildBrakkIndustrialBruiser()
        {
            // Reference-driven heavy industrial melee mech. The construction keeps the existing
            // animation contract (Arm/Leg/Boot/Gauntlet names) while replacing the old humanoid
            // bruiser silhouette with a broad yellow reactor-powered combat frame.
            Color yellow=new(.72f,.47f,.055f,1f);
            Color yellowDark=new(.42f,.27f,.035f,1f);
            Color blackMetal=new(.055f,.060f,.070f,1f);
            Color jointMetal=new(.16f,.17f,.19f,1f);
            Color edgeMetal=new(.31f,.32f,.34f,1f);
            Color reactor=new(1f,.29f,.035f,1f);
            Color reactorHot=new(1f,.64f,.10f,1f);

            // Massive compact torso with broad chest and narrower mechanical waist.
            Mesh torso=HeroMeshLibrary.Loft("DF_BrakkHeavyTorso",
                new[]{-.39f,-.25f,-.05f,.16f,.35f,.51f},
                new[]{.27f,.34f,.46f,.58f,.60f,.34f},
                new[]{.19f,.24f,.33f,.40f,.39f,.22f},
                new[]{.015f,.02f,.025f,.035f,.02f,0f},16);
            Sculpt("Torso",torso,new Vector3(0,.06f,0),new Vector3(1.18f,1f,1.08f),blackMetal);
            Plate("BrakkChestL",new Vector3(-.27f,.25f,.245f),.63f,.46f,.15f,yellow,-6);
            Plate("BrakkChestR",new Vector3(.27f,.25f,.245f),.63f,.46f,.15f,yellow,6);
            Plate("BrakkAbdomen",new Vector3(0,-.08f,.185f),.54f,.30f,.12f,jointMetal);
            Plate("BrakkGorget",new Vector3(0,.50f,.09f),.60f,.18f,.26f,yellowDark);
            Detail("BrakkHazardL",new Vector3(-.31f,.18f,.335f),new Vector3(.055f,.23f,.025f),blackMetal,new Vector3(0,0,-28));
            Detail("BrakkHazardR",new Vector3(.31f,.18f,.335f),new Vector3(.055f,.23f,.025f),blackMetal,new Vector3(0,0,28));

            // Large orange chest reactor: the visual center of the character from gameplay camera.
            GameObject reactorRing=Sculpt("BrakkReactorRing",HeroMeshLibrary.Ring("DF_BrakkReactorRingMesh",.235f,.042f),
                new Vector3(0,.20f,.375f),Vector3.one,reactor,null,true);
            reactorRing.transform.localRotation=Quaternion.Euler(90,0,0);
            GameObject reactorInner=Sculpt("BrakkReactorInner",HeroMeshLibrary.Ring("DF_BrakkReactorInnerMesh",.145f,.035f),
                new Vector3(0,.20f,.392f),Vector3.one,reactorHot,null,true);
            reactorInner.transform.localRotation=Quaternion.Euler(90,0,0);
            Detail("BrakkReactorCore",new Vector3(0,.20f,.405f),new Vector3(.115f,.115f,.030f),reactorHot,new Vector3(0,0,45),null,true);
            for(int i=-1;i<=1;i++)
                Detail("BrakkSpineGlow"+(i+1),new Vector3(0,-.01f-i*.075f,.245f),new Vector3(.075f,.050f,.025f),reactor,default,null,true);

            // Pelvis/hips: dark mechanics under yellow segmented plates.
            Mesh pelvis=HeroMeshLibrary.Loft("DF_BrakkPelvis",
                new[]{-.45f,-.31f,-.18f},new[]{.29f,.39f,.30f},new[]{.18f,.24f,.18f});
            Sculpt("Pelvis",pelvis,new Vector3(0,-.02f,0),new Vector3(1.20f,1f,1.12f),blackMetal);
            Plate("BrakkHipL",new Vector3(-.30f,-.29f,.12f),.40f,.32f,.14f,yellow,-8);
            Plate("BrakkHipR",new Vector3(.30f,-.29f,.12f),.40f,.32f,.14f,yellow,8);

            // Thick articulated legs, large knees and wide planted industrial feet.
            float legX=.255f;
            Mesh leg=HeroMeshLibrary.Loft("DF_BrakkHeavyLeg",
                new[]{-1f,-.77f,-.24f,.10f,.62f,1f},
                new[]{.095f,.13f,.14f,.18f,.21f,.17f},
                new[]{.11f,.14f,.15f,.19f,.21f,.17f},
                new[]{.03f,.025f,.005f,.012f,.005f,0f});
            Mesh boot=HeroMeshLibrary.Loft("DF_BrakkHeavyBoot",
                new[]{-.10f,-.04f,.085f,.19f},
                new[]{.17f,.19f,.16f,.10f},
                new[]{.27f,.32f,.24f,.13f},
                new[]{.105f,.115f,.070f,0f});

            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Sculpt("Leg"+s,leg,new Vector3(side*legX,-.62f,0),new Vector3(1.18f,.35f,1.18f),blackMetal);
                Sculpt("Boot"+s,boot,new Vector3(side*legX,-.94f,.035f),new Vector3(1.18f,1.05f,1.20f),edgeMetal);
                Plate("Knee"+s,new Vector3(side*legX,-.57f,.145f),.31f,.29f,.15f,yellow,side*-4,"Leg"+s);
                Plate("BrakkShin"+s,new Vector3(side*legX,-.74f,.12f),.31f,.33f,.12f,yellowDark,side*3,"Leg"+s);
                Plate("BrakkThigh"+s,new Vector3(side*legX,-.39f,.13f),.34f,.35f,.12f,yellow,side*-7,"Leg"+s);
                Detail("BrakkKneeHub"+s,new Vector3(side*legX,-.57f,.235f),new Vector3(.12f,.12f,.035f),jointMetal,default,"Leg"+s);
                Detail("BrakkKneeBearing"+s,new Vector3(side*legX,-.57f,.258f),new Vector3(.075f,.075f,.022f),reactor,default,"Leg"+s,true);
                Detail("BrakkAnkleServo"+s,new Vector3(side*legX,-.85f,.12f),new Vector3(.16f,.10f,.12f),jointMetal,default,"Leg"+s);
                Detail("BrakkToeGuard"+s,new Vector3(side*legX,-.94f,.208f),new Vector3(.25f,.10f,.07f),yellow,default,"Boot"+s);

                Transform arm=new GameObject("DF_Arm"+s).transform;
                arm.SetParent(root,false);
                arm.localPosition=new Vector3(side*.56f,.40f,.015f);
                arm.localRotation=Quaternion.Euler(-3,0,side*-7f);

                Mesh upper=HeroMeshLibrary.Loft("DF_BrakkUpperArm",
                    new[]{-.34f,-.26f,-.06f,.05f},
                    new[]{.10f,.14f,.19f,.14f},
                    new[]{.11f,.15f,.19f,.14f});
                Sculpt("UpperArm"+s,upper,Vector3.zero,new Vector3(1.22f,1f,1.18f),blackMetal,arm);

                Mesh fore=HeroMeshLibrary.Loft("DF_BrakkForearm",
                    new[]{-.28f,-.21f,-.055f,.03f},
                    new[]{.10f,.15f,.17f,.12f},
                    new[]{.11f,.15f,.18f,.12f});
                Sculpt("Vambrace"+s,fore,new Vector3(0,-.31f,.02f),new Vector3(1.24f,1f,1.18f),jointMetal,arm);

                // Keep Brakk broad, but reduce the shoulder armor to 50% of the previous size
                // so the torso, head and gauntlets remain readable from the gameplay camera.
                Plate("BrakkShoulder"+s,new Vector3(side*.63f,.45f,.01f),.34f,.23f,.26f,yellow,side*-10);
                Bind("BrakkShoulder"+s,"Arm"+s);
                Detail("BrakkShoulderBearing"+s,new Vector3(side*.58f,.41f,.16f),new Vector3(.17f,.17f,.09f),jointMetal,default,"Arm"+s);
                Detail("BrakkShoulderBearingLight"+s,new Vector3(side*.58f,.41f,.212f),new Vector3(.07f,.07f,.025f),reactor,default,"Arm"+s,true);
                Detail("BrakkElbowPiston"+s,new Vector3(side*.56f,.06f,.12f),new Vector3(.11f,.22f,.10f),edgeMetal,new Vector3(0,0,side*12),"Arm"+s);
                Detail("BrakkShoulderStripe"+s,new Vector3(side*.66f,.47f,.15f),new Vector3(.125f,.0225f,.015f),blackMetal,
                    new Vector3(0,0,side*-14),"Arm"+s);
            }

            // Small armored mech head with a narrow orange visor. No human features.
            Vector3 hp=new(0,.80f,.015f);
            Mesh head=HeroMeshLibrary.Loft("DF_BrakkCombatHead",
                new[]{-.22f,-.15f,-.04f,.09f,.18f,.24f},
                new[]{.085f,.145f,.185f,.195f,.165f,.075f},
                new[]{.080f,.135f,.155f,.155f,.125f,.065f},
                new[]{.045f,.032f,.014f,0f,-.008f,-.016f},16);
            Sculpt("Head",head,hp,new Vector3(1.02f,.95f,.98f),yellowDark);
            Sculpt("Neck",HeroMeshLibrary.Loft("DF_BrakkNeck",new[]{-.10f,.09f},new[]{.10f,.11f},new[]{.10f,.105f}),
                new Vector3(0,.55f,0),Vector3.one,blackMetal);
            Plate("BrakkFacePlate",hp+new Vector3(0,-.025f,.151f),.34f,.27f,.075f,blackMetal);
            Detail("BrakkVisor",hp+new Vector3(0,.055f,.198f),new Vector3(.25f,.030f,.018f),reactorHot,default,null,true);
            Plate("BrakkBrow",hp+new Vector3(0,.145f,.095f),.40f,.18f,.17f,yellow,0);
            Detail("BrakkJawVent",hp+new Vector3(0,-.12f,.185f),new Vector3(.20f,.035f,.020f),jointMetal);

            // Two compact shoulder/back launcher housings echo the supplied reference without adding projectiles.
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 podPos=new(side*.38f,.70f,-.20f);
                Detail("BrakkBackPod"+(side<0?"L":"R"),podPos,new Vector3(.24f,.22f,.30f),yellowDark,new Vector3(-12,side*5,0));
                for(int cell=0;cell<2;cell++)
                    Detail("BrakkPodVent"+(side<0?"L":"R")+cell,
                        podPos+new Vector3((cell==0?-.055f:.055f),.02f,.165f),
                        new Vector3(.045f,.055f,.018f),blackMetal);
            }

            // Base melee weapons are integrated gauntlets: yellow armor, dark mechanics, orange reactor strip.
            Mesh gauntlet=HeroMeshLibrary.Loft("DF_BrakkImpactGauntlet",
                new[]{-.24f,-.18f,.12f,.23f,.29f},
                new[]{.19f,.25f,.25f,.19f,.14f},
                new[]{.12f,.17f,.17f,.13f,.10f},
                new[]{0f,0f,0f,0f,0f},8);
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Vector3 p=new(side*.61f,-.17f,.20f);
                Sculpt("Gauntlet"+s,gauntlet,p,new Vector3(1.10f,1f,1.12f),yellowDark);
                // Build every digit while the gauntlet is still a direct root child:
                // Detail/Bind resolves direct children. Attach the completed hand last.
                for(int k=0;k<4;k++)
                {
                    float x=(k-1.5f)*.132f;
                    float knuckleHeight=.025f-Mathf.Abs(k-1.5f)*.020f;
                    Detail("Knuckle"+s+k,p+new Vector3(x,-.075f+knuckleHeight,.235f),
                        new Vector3(.116f,.14f,.13f),edgeMetal,new Vector3(-8,0,0),"Gauntlet"+s);
                    Detail("BrakkCurledFinger"+s+k,p+new Vector3(x,-.177f,.202f),
                        new Vector3(.109f,.12f,.13f),yellowDark,new Vector3(-24,0,0),"Gauntlet"+s);
                    Detail("BrakkFingerCrease"+s+k,p+new Vector3(x,-.137f,.271f),
                        new Vector3(.091f,.016f,.012f),blackMetal,default,"Gauntlet"+s);
                }
                Detail("BrakkThumbBase"+s,p+new Vector3(-side*.275f,.015f,.09f),
                    new Vector3(.13f,.20f,.14f),yellowDark,new Vector3(0,0,side*28f),"Gauntlet"+s);
                Detail("BrakkThumbTip"+s,p+new Vector3(-side*.255f,-.10f,.18f),
                    new Vector3(.14f,.13f,.15f),edgeMetal,new Vector3(-20,0,side*38f),"Gauntlet"+s);
                Detail("GauntletReactor"+s,p+new Vector3(0,.12f,.285f),
                    new Vector3(.27f,.060f,.025f),reactor,default,"Gauntlet"+s,true);
                Detail("BrakkFistPlate"+s,p+new Vector3(0,.02f,.255f),
                    new Vector3(.32f,.13f,.045f),yellow,default,"Gauntlet"+s);
                Detail("BrakkFistSide"+s,p+new Vector3(side*.17f,.02f,.15f),
                    new Vector3(.065f,.22f,.16f),blackMetal,new Vector3(0,0,side*8),"Gauntlet"+s);
                Detail("BrakkImpactEdge"+s,p+new Vector3(0,.075f,.283f),
                    new Vector3(.25f,.025f,.022f),reactorHot,default,"Gauntlet"+s,true);
                Bind("Gauntlet"+s,"Arm"+s);
            }

            Detail("IdentityCore",new Vector3(0,.20f,.407f),new Vector3(.065f,.065f,.018f),reactorHot,
                new Vector3(0,0,45),null,true);
        }

        private void BuildVeyrShadowAssassin()
        {
            // Reference-driven premium mobile silhouette: exposed eyes + face mask, tied dark hair,
            // narrow layered torso, high boots, asymmetrical cloth and twin violet shadow blades.
            Color shadow=new(.020f,.016f,.030f,1f);
            Color leather=new(.055f,.045f,.075f,1f);
            Color graphite=new(.12f,.11f,.16f,1f);
            Color metal=new(.25f,.22f,.31f,1f);
            Color violet=new(.76f,.025f,.065f,1f);
            Color violetHot=new(1f,.17f,.20f,1f);
            Color faceBlack=new(.004f,.003f,.008f,1f);
            Color hair=new(.018f,.014f,.028f,1f);

            // Feminine, agile proportions: narrow waist, slightly wider upper torso, long readable legs.
            Mesh torso=HeroMeshLibrary.Loft("DF_VeyrShadowTorso",
                new[]{-.38f,-.24f,-.06f,.16f,.34f,.48f},
                new[]{.18f,.20f,.24f,.33f,.36f,.24f},
                new[]{.12f,.13f,.16f,.20f,.21f,.14f},
                new[]{.01f,0f,.01f,.02f,.015f,0f},16);
            Sculpt("Torso",torso,new Vector3(0,.04f,0),new Vector3(.93f,1f,.92f),shadow);
            Plate("VeyrChestWrap",new Vector3(0,.23f,.19f),.57f,.43f,.085f,leather);
            Plate("VeyrChestEdge",new Vector3(.015f,.22f,.238f),.38f,.30f,.026f,graphite,-7);
            Detail("VeyrChestRune",new Vector3(.06f,.18f,.278f),new Vector3(.045f,.31f,.020f),violetHot,new Vector3(0,0,-24),null,true);
            Plate("VeyrWaistArmor",new Vector3(0,-.13f,.145f),.43f,.24f,.08f,shadow);
            Detail("VeyrBelt",new Vector3(0,-.27f,.13f),new Vector3(.47f,.075f,.12f),graphite);
            Detail("VeyrBeltGem",new Vector3(0,-.265f,.205f),new Vector3(.095f,.11f,.025f),violetHot,new Vector3(0,0,45),null,true);

            Mesh pelvis=HeroMeshLibrary.Loft("DF_VeyrPelvis",
                new[]{-.43f,-.30f,-.18f},new[]{.20f,.26f,.21f},new[]{.12f,.15f,.12f});
            Sculpt("Pelvis",pelvis,new Vector3(0,-.02f,0),new Vector3(.93f,1f,.94f),leather);

            float legX=.165f;
            Mesh leg=HeroMeshLibrary.Loft("DF_VeyrLongGreave",
                new[]{-1f,-.76f,-.20f,.13f,.66f,1f},
                new[]{.055f,.068f,.074f,.105f,.116f,.095f},
                new[]{.070f,.078f,.086f,.105f,.108f,.090f},
                new[]{.018f,.014f,0f,.004f,0f,0f});
            Mesh boot=HeroMeshLibrary.Loft("DF_VeyrHighBoot",
                new[]{-.10f,-.04f,.08f,.19f},new[]{.105f,.115f,.095f,.060f},
                new[]{.17f,.20f,.145f,.08f},new[]{.065f,.075f,.045f,0f});
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Sculpt("Leg"+s,leg,new Vector3(side*legX,-.62f,0),new Vector3(1f,.37f,1f),shadow);
                Sculpt("Boot"+s,boot,new Vector3(side*legX,-.94f,.025f),new Vector3(1.04f,1.06f,1.04f),leather);
                Plate("Knee"+s,new Vector3(side*legX,-.58f,.105f),.17f,.21f,.08f,metal,side*-5,"Leg"+s);
                Plate("VeyrThighGuard"+s,new Vector3(side*legX,-.39f,.105f),.17f,.28f,.065f,graphite,side*-9,"Leg"+s);
                Detail("VeyrThighStrap"+s,new Vector3(side*legX,-.31f,.14f),new Vector3(.20f,.045f,.035f),violet,default,"Leg"+s,true);

                Transform arm=new GameObject("DF_Arm"+s).transform;
                arm.SetParent(root,false);
                arm.localPosition=new Vector3(side*.345f,.39f,.015f);
                arm.localRotation=Quaternion.Euler(-5,0,side*-11f);

                Mesh upper=HeroMeshLibrary.Loft("DF_VeyrUpperArm",
                    new[]{-.32f,-.24f,-.05f,.04f},new[]{.055f,.071f,.095f,.075f},new[]{.062f,.078f,.092f,.070f});
                Sculpt("UpperArm"+s,upper,Vector3.zero,Vector3.one,shadow,arm);
                Mesh fore=HeroMeshLibrary.Loft("DF_VeyrForearm",
                    new[]{-.28f,-.20f,-.055f,.025f},new[]{.055f,.082f,.092f,.064f},new[]{.060f,.082f,.095f,.063f});
                Sculpt("Vambrace"+s,fore,new Vector3(0,-.30f,.02f),Vector3.one,graphite,arm);
                Mesh glove=HeroMeshLibrary.Loft("DF_VeyrGlove",
                    new[]{-.13f,-.07f,.07f,.11f},new[]{.045f,.075f,.080f,.060f},new[]{.052f,.065f,.073f,.050f});
                Sculpt("Glove"+s,glove,new Vector3(0,-.59f,.055f),Vector3.one,leather,arm);

                Plate("VeyrShoulder"+s,new Vector3(side*.37f,.43f,.005f),.27f,.22f,.26f,side<0?graphite:shadow,side*-18);
                Bind("VeyrShoulder"+s,"Arm"+s);
                Detail("VeyrArmRune"+s,new Vector3(0,-.31f,.115f),new Vector3(.045f,.21f,.020f),violetHot,new Vector3(0,0,side*8), "Arm"+s,true);
            }

            // Visible face and eyes with a cloth mask instead of the old full helmet.
            Vector3 hp=new(0,.80f,.015f);
            Mesh head=HeroMeshLibrary.Loft("DF_VeyrFace",
                new[]{-.22f,-.15f,-.04f,.09f,.18f,.24f},
                new[]{.052f,.112f,.158f,.164f,.140f,.060f},
                new[]{.050f,.102f,.136f,.138f,.108f,.052f},
                new[]{.048f,.033f,.016f,0f,-.004f,-.014f},16);
            Sculpt("Head",head,hp,Vector3.one,faceBlack);
            Sculpt("Neck",HeroMeshLibrary.Loft("DF_VeyrNeck",new[]{-.09f,.08f},new[]{.068f,.078f},new[]{.064f,.070f}),
                new Vector3(0,.55f,0),Vector3.one,faceBlack);
            Plate("VeyrMask",hp+new Vector3(0,-.065f,.137f),.30f,.22f,.055f,faceBlack);

            // The face is intentionally featureless black. The violet eyes now cant upward
            // toward the outer corners and are slightly thicker for a sharper top-down read.
            // No glowing mouth seam or facial stripe.
            for(int side=-1;side<=1;side+=2)
            {
                Detail("EyeGlow"+side,hp+new Vector3(side*.073f,.045f,.160f),
                    new Vector3(.108f,.024f,.013f),violetHot,
                    new Vector3(0,0,side*12f),null,true);
            }

            Mesh cap=HeroMeshLibrary.Loft("DF_VeyrHairCap",
                new[]{.01f,.12f,.21f,.28f},new[]{.172f,.181f,.148f,.032f},
                new[]{.145f,.153f,.132f,.040f},new[]{-.055f,-.043f,-.020f,-.014f});
            Sculpt("Hair",cap,hp,Vector3.one,hair);

            // High ponytail + loose side locks create the silhouette of the reference without hair physics.
            Mesh pony=HeroMeshLibrary.Ribbon("DF_VeyrPonytail",
                new[]{new Vector3(.04f,1.02f,-.05f),new Vector3(.18f,.92f,-.16f),new Vector3(.30f,.66f,-.26f),new Vector3(.42f,.35f,-.33f),new Vector3(.35f,.06f,-.36f)},
                new[]{.11f,.13f,.115f,.082f,.018f},.035f);
            Sculpt("VeyrPonytail",pony,Vector3.zero,Vector3.one,hair);
            Mesh ponyGlow=HeroMeshLibrary.Ribbon("DF_VeyrPonyGlow",
                new[]{new Vector3(.06f,1.02f,-.02f),new Vector3(.20f,.90f,-.13f),new Vector3(.31f,.64f,-.23f),new Vector3(.40f,.35f,-.30f)},
                new[]{.020f,.023f,.018f,.004f},.012f);
            Sculpt("VeyrPonyGlow",ponyGlow,Vector3.zero,Vector3.one,violet,null,true);
            for(int side=-1;side<=1;side+=2)
            {
                Mesh lockMesh=HeroMeshLibrary.Ribbon("DF_VeyrFaceLock"+side,
                    new[]{new Vector3(side*.11f,.98f,.02f),new Vector3(side*.17f,.82f,.08f),new Vector3(side*.18f,.60f,.00f),new Vector3(side*.15f,.43f,-.05f)},
                    new[]{.060f,.052f,.037f,.010f},.025f);
                Sculpt("VeyrHairLock"+(side<0?"L":"R"),lockMesh,Vector3.zero,Vector3.one,hair);
            }

            // The assassin's mantle falls as one narrow back panel with subtle edge folds.
            Mesh backCape=HeroMeshLibrary.Ribbon("DF_VeyrBackMantle",
                new[]{new Vector3(0,.18f,-.19f),new Vector3(0,-.18f,-.25f),new Vector3(0,-.51f,-.30f),new Vector3(0,-.86f,-.35f)},
                new[]{.25f,.32f,.35f,.32f},.025f);
            Sculpt("VeyrBackMantle",backCape,Vector3.zero,Vector3.one,leather);
            // "Cape" name is preserved for the motion rig.
            Mesh cape=HeroMeshLibrary.Ribbon("DF_VeyrCape",
                new[]{new Vector3(-.22f,.10f,-.20f),new Vector3(-.27f,-.22f,-.26f),new Vector3(-.31f,-.50f,-.31f),new Vector3(-.30f,-.82f,-.36f)},
                new[]{.06f,.065f,.06f,.045f},.020f);
            Sculpt("Cape",cape,Vector3.zero,Vector3.one,violetHot,null,true);
            Mesh tailR=HeroMeshLibrary.Ribbon("DF_VeyrTailR",
                new[]{new Vector3(.22f,.10f,-.20f),new Vector3(.27f,-.22f,-.26f),new Vector3(.31f,-.50f,-.31f),new Vector3(.30f,-.82f,-.36f)},
                new[]{.06f,.065f,.06f,.045f},.020f);
            Sculpt("CoatTailR",tailR,Vector3.zero,Vector3.one,violetHot,null,true);
            Mesh centerTail=HeroMeshLibrary.Ribbon("DF_VeyrCenterTail",
                new[]{new Vector3(.00f,.05f,-.18f),new Vector3(.04f,-.28f,-.24f),new Vector3(-.02f,-.58f,-.30f),new Vector3(.08f,-.83f,-.35f)},
                new[]{.09f,.12f,.10f,.025f},.018f);
            Sculpt("VeyrCenterTail",centerTail,Vector3.zero,Vector3.one,leather);

            ProductionBlades();

            Detail("IdentityCore",new Vector3(0,.29f,.288f),new Vector3(.070f,.15f,.030f),violetHot,new Vector3(0,0,45),null,true);
        }

        private GameObject Sculpt(string name,Mesh mesh,Vector3 position,Vector3 scale,Color color,Transform parent=null,bool energy=false)
        {
            var go=new GameObject("DF_"+name);go.transform.SetParent(parent!=null?parent:root,false);
            go.transform.localPosition=position;go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=HeroMeshLibrary.Painted(mesh,FinishedColor(color,energy),energy,color==cloth||color==dark?0:1);go.AddComponent<MeshRenderer>().sharedMaterial=MaterialFor(color,energy);return go;
        }
        private void BuildProductionHead(HeroId h,Color metal,Color trim)
        {
            if(h==HeroId.Kaelon)
            {
                // Closed "breacher" helmet based on the reference: broad steel shell, deep face plate
                // and a vented visor that remains readable from the top-down camera.
                Vector3 kaelonHeadPos=new(0,.80f,.02f);
                Mesh kaelonHelmet=HeroMeshLibrary.Loft("DF_KaelonBreacherHelm",
                    new[]{-.24f,-.17f,-.05f,.10f,.22f,.29f},
                    new[]{.085f,.155f,.205f,.215f,.185f,.075f},
                    new[]{.080f,.145f,.175f,.170f,.135f,.065f},
                    new[]{.045f,.035f,.015f,0f,-.010f,-.018f},16);
                Sculpt("Head",kaelonHelmet,kaelonHeadPos,new Vector3(1.18f,1.05f,1.12f),metal);
                Sculpt("Neck",HeroMeshLibrary.Loft("DF_KaelonNeck",new[]{-.10f,.09f},new[]{.09f,.10f},new[]{.085f,.09f}),
                    new Vector3(0,.54f,0),Vector3.one,dark);
                Plate("KaelonFacePlate",kaelonHeadPos+new Vector3(0,-.015f,.178f),.41f,.35f,.085f,new Color(.34f,.35f,.35f));
                Plate("KaelonBrowArmor",kaelonHeadPos+new Vector3(0,.155f,.105f),.48f,.20f,.20f,metal);
                Plate("KaelonJawGuard",kaelonHeadPos+new Vector3(0,-.155f,.135f),.36f,.18f,.16f,trim);
                for(int row=0;row<2;row++)
                for(int i=-2;i<=2;i++)
                    Detail("KaelonVent"+row+"_"+(i+2),
                        kaelonHeadPos+new Vector3(i*.068f,.055f-row*.072f,.229f),
                        new Vector3(.041f,.018f,.014f),cloth,default,null,false);
                Detail("KaelonVisorSlit",kaelonHeadPos+new Vector3(0,.112f,.232f),new Vector3(.33f,.026f,.016f),teamEdge,default,null,true);
                for(int side=-1;side<=1;side+=2)
                    Plate("KaelonHelmCheek"+side,kaelonHeadPos+new Vector3(side*.205f,-.015f,.035f),.13f,.35f,.20f,metal,side*-8);
                return;
            }
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
            // SHADOW STEEL base set: dark physical blade with a thin violet energy edge.
            Color bladeMetal=new(.11f,.10f,.15f,1f);
            Color bladeSpine=new(.24f,.21f,.31f,1f);
            Color edge=new(.56f,.12f,.90f,1f);
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                // Seat each hilt at its inward-canted wrist so the grip sits inside the glove.
                Transform weapon=WeaponPivot(s,new Vector3(side*.235f,-.20f,.09f));
                weapon.localRotation=Quaternion.Euler(6f,side*-7f,side*-10f);

                Mesh blade=HeroMeshLibrary.Ribbon("DF_ShadowSteelBlade"+s,
                    new[]{new Vector3(0,-.09f,0),new Vector3(side*.028f,-.22f,.27f),
                        new Vector3(side*.075f,-.41f,.64f),new Vector3(side*.15f,-.58f,1.08f),
                        new Vector3(side*.12f,-.63f,1.26f)},
                    new[]{.050f,.095f,.090f,.052f,.001f},.052f);
                Sculpt("Blade"+s,blade,Vector3.zero,Vector3.one,bladeMetal,weapon,false);

                Mesh energyEdge=HeroMeshLibrary.Ribbon("DF_ShadowSteelEdge"+s,
                    new[]{new Vector3(side*.010f,-.10f,.02f),new Vector3(side*.040f,-.23f,.28f),
                        new Vector3(side*.092f,-.42f,.65f),new Vector3(side*.157f,-.58f,1.08f),
                        new Vector3(side*.125f,-.63f,1.255f)},
                    new[]{.014f,.019f,.016f,.011f,.001f},.014f);
                Sculpt("BladeEdge"+s,energyEdge,Vector3.zero,Vector3.one,edge,weapon,true);
                Mesh fuller=HeroMeshLibrary.Ribbon("DF_ShadowSteelFuller"+s,
                    new[]{new Vector3(0,-.15f,.09f),new Vector3(side*.041f,-.29f,.35f),
                        new Vector3(side*.088f,-.45f,.72f),new Vector3(side*.14f,-.57f,1.05f)},
                    new[]{.013f,.018f,.013f,.002f},.006f);
                Sculpt("BladeFuller"+s,fuller,new Vector3(0,0,.027f),Vector3.one,bladeSpine,weapon);

                Detail("Hilt"+s,new Vector3(0,-.015f,-.035f),new Vector3(.105f,.235f,.105f),bladeSpine,default,"Weapon"+s);
                Detail("Guard"+s,new Vector3(0,-.115f,.060f),new Vector3(.28f,.050f,.12f),bladeMetal,new Vector3(0,0,side*8f),"Weapon"+s);
                Detail("Pommel"+s,new Vector3(0,.13f,-.060f),new Vector3(.11f,.075f,.11f),edge,new Vector3(0,0,45),"Weapon"+s,true);
                Bind("Weapon"+s,"Arm"+s);
                // The former world-space pivot sat about 9 cm outside the knuckle and
                // 9 cm forward of the glove. Place the grip at the hand's local rest point
                // so the blade is visibly held, then let the existing arm animation carry it.
                weapon.localPosition=new Vector3(0,-.59f,.055f);
            }
        }
        private void BuildKaelonReferenceArmor(Color metal,Color trim)
        {
            Color under=new(.065f,.075f,.105f,1f);
            Color steelLight=new(.76f,.76f,.70f,1f);
            Color steelDark=new(.23f,.25f,.31f,1f);
            Color regaliaGold=new(.82f,.54f,.15f,1f);
            Color jade=new(.20f,.78f,.58f,1f);

            // Layered chest slab and high collar: big shapes first for mobile/top-down readability.
            Plate("KaelonChestSlab",new Vector3(0,.24f,.285f),.95f,.53f,.15f,steelLight);
            Plate("KaelonChestInset",new Vector3(0,.20f,.373f),.62f,.34f,.045f,steelDark);
            Detail("KaelonChestBrace",new Vector3(0,.19f,.407f),new Vector3(.095f,.30f,.025f),regaliaGold,default,null,false);
            Detail("KaelonChestCore",new Vector3(0,.29f,.430f),new Vector3(.075f,.13f,.024f),jade,new Vector3(0,0,45),null,true);
            Detail("KaelonBreastSeal",new Vector3(0,.29f,.438f),new Vector3(.16f,.18f,.025f),regaliaGold,new Vector3(0,0,45));
            Detail("KaelonSealLight",new Vector3(0,.29f,.456f),new Vector3(.065f,.09f,.018f),jade,new Vector3(0,0,45),null,true);
            Plate("KaelonCollarL",new Vector3(-.29f,.54f,.11f),.34f,.24f,.30f,metal,-13);
            Plate("KaelonCollarR",new Vector3(.29f,.54f,.11f),.34f,.24f,.30f,metal,13);

            // Oversized segmented pauldrons and broad forearm armor echo the reference without extra colliders.
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Plate("KaelonPauldronOuter"+s,new Vector3(side*.69f,.48f,.00f),.252f,.2912f,.3752f,regaliaGold,side*-7);
                Plate("KaelonPauldronTop"+s,new Vector3(side*.63f,.69f,.00f),.2184f,.112f,.3304f,steelLight,side*-9);
                Detail("KaelonPauldronPin"+s,new Vector3(side*.69f,.49f,.201f),new Vector3(.09f,.09f,.025f),jade,new Vector3(0,0,45),null,true);
                Detail("KaelonShoulderVent"+s,new Vector3(side*.71f,.51f,.198f),new Vector3(.1008f,.056f,.014f),steelDark,default,null,false);

                // Root-space detail is bound after creation so it follows the arm rig.
                Plate("KaelonForearmPlate"+s,new Vector3(side*.51f,.04f,.19f),.36f,.49f,.30f,metal,side*6,"Arm"+s);
                Detail("KaelonForearmInset"+s,new Vector3(side*.51f,.04f,.352f),new Vector3(.19f,.27f,.025f),under,default,"Arm"+s,false);
                if(side>0)
                    Detail("KaelonForearmCell",new Vector3(side*.51f,.05f,.375f),new Vector3(.10f,.12f,.018f),teamEdge,default,"Arm"+s,true);
            }

            // Layered waist plating keeps the tank planted and visually heavier.
            Plate("KaelonBeltArmor",new Vector3(0,-.25f,.19f),.78f,.18f,.17f,under);
            Plate("KaelonTassetL",new Vector3(-.31f,-.39f,.16f),.31f,.42f,.15f,metal,8);
            Plate("KaelonTassetR",new Vector3(.31f,-.39f,.16f),.31f,.42f,.15f,metal,-8);
        }

        private void ProductionCitadel(Color metal,Color trim)
        {
            // Compact left-arm breacher guard instead of the old oversized fantasy shield.
            // It preserves the tank read while letting the hammer dominate the silhouette like the reference.
            var guard=WeaponPivot("L",new Vector3(-.53f,-.05f,.18f));
            guard.localRotation=Quaternion.Euler(0,0,8f);
            Create("BreachGuard",PrimitiveType.Cube,new Vector3(0,.08f,.05f),new Vector3(.38f,.62f,.28f),metal,default,guard);
            Create("BreachGuardGoldRim",PrimitiveType.Cube,new Vector3(0,.08f,.204f),new Vector3(.31f,.53f,.035f),new Color(.82f,.54f,.15f),default,guard);
            Create("BreachGuardInset",PrimitiveType.Cube,new Vector3(0,.08f,.226f),new Vector3(.24f,.43f,.035f),trim,default,guard);
            Create("BreachGuardRail",PrimitiveType.Cube,new Vector3(0,.08f,.249f),new Vector3(.065f,.28f,.018f),teamEdge,default,guard);
            Create("BreachGuardCore",PrimitiveType.Cube,new Vector3(0,.08f,.264f),new Vector3(.12f,.12f,.018f),new Color(.20f,.78f,.58f),new Vector3(0,0,45),guard);

            var weapon=WeaponPivot("R",new Vector3(.57f,-.20f,.18f));
            weapon.localRotation=Quaternion.Euler(0,-5f,-16f);

            // Long, two-hand-capable shaft and broad industrial sledge head.
            Mesh grip=HeroMeshLibrary.Loft("DF_HammerShaft",
                new[]{-.62f,-.48f,.18f,.66f},new[]{.047f,.055f,.052f,.043f},new[]{.047f,.055f,.052f,.043f});
            Sculpt("HammerGrip",grip,Vector3.zero,Vector3.one,new Color(.075f,.068f,.060f),weapon);
            Create("HammerLowerPommel",PrimitiveType.Cube,new Vector3(0,-.61f,0),new Vector3(.15f,.12f,.15f),trim,default,weapon);
            Create("HammerCollar",PrimitiveType.Cube,new Vector3(0,.31f,0),new Vector3(.18f,.16f,.18f),new Color(.32f,.33f,.33f),default,weapon);

            Create("AnchorHead",PrimitiveType.Cube,new Vector3(0,.54f,.03f),new Vector3(.86f,.43f,.48f),metal,default,weapon);
            Create("HammerFace",PrimitiveType.Cube,new Vector3(0,.54f,.285f),new Vector3(.60f,.30f,.055f),new Color(.31f,.32f,.32f),default,weapon);
            Create("HammerTopPlate",PrimitiveType.Cube,new Vector3(0,.79f,.02f),new Vector3(.64f,.09f,.40f),new Color(.69f,.69f,.67f),default,weapon);
            Create("HammerPowerSpine",PrimitiveType.Cube,new Vector3(0,.82f,.03f),new Vector3(.11f,.035f,.35f),accent,default,weapon);
            Create("HammerStrikeLens",PrimitiveType.Cube,new Vector3(0,.54f,.326f),new Vector3(.34f,.12f,.024f),teamEdge,default,weapon);
            for(int side=-1;side<=1;side+=2)
            {
                Create("HammerCheek"+side,PrimitiveType.Cube,new Vector3(side*.47f,.54f,.01f),new Vector3(.12f,.34f,.40f),trim,
                    new Vector3(0,0,side*8f),weapon);
                Create("HammerEnergy"+side,PrimitiveType.Cube,new Vector3(side*.235f,.54f,.321f),new Vector3(.115f,.18f,.022f),accent,default,weapon);
            }

            // Mechanical fasteners sit on the existing face, outside the central light strip.
            for(int side=-1;side<=1;side+=2)
                for(int row=-1;row<=1;row+=2)
                    Create("HammerFaceBolt"+side+"_"+row,PrimitiveType.Cylinder,
                        new Vector3(side*.25f,.54f+row*.11f,.319f),new Vector3(.040f,.009f,.040f),
                        metal,new Vector3(90,0,0),weapon);
            for(int band=0;band<3;band++)
                Sculpt("HammerGripFerrule"+band,HeroMeshLibrary.Ring("DF_HammerGripFerrule",.059f,.008f),
                    new Vector3(0,-.35f+band*.13f,0),Vector3.one,trim,weapon);
            // Vented strike face gives the head the same industrial language as the helmet.
            for(int i=-2;i<=2;i++)
                Create("HammerVent"+(i+2),PrimitiveType.Cube,new Vector3(i*.105f,.57f,.322f),new Vector3(.055f,.025f,.018f),cloth,default,weapon);

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


