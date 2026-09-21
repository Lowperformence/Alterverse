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
            bool tank=h==HeroId.Kaelon,bruiser=h==HeroId.Rook,heavy=tank||bruiser;
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
        private void BuildVeyrShadowAssassin()
        {
            // Reference-driven premium mobile silhouette: exposed eyes + face mask, tied dark hair,
            // narrow layered torso, high boots, asymmetrical cloth and twin violet shadow blades.
            Color shadow=new(.020f,.016f,.030f,1f);
            Color leather=new(.055f,.045f,.075f,1f);
            Color graphite=new(.12f,.11f,.16f,1f);
            Color metal=new(.25f,.22f,.31f,1f);
            Color violet=new(.58f,.12f,.92f,1f);
            Color violetHot=new(.86f,.30f,1f,1f);
            Color skin=new(.68f,.49f,.43f,1f);
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
            Sculpt("Head",head,hp,Vector3.one,skin);
            Sculpt("Neck",HeroMeshLibrary.Loft("DF_VeyrNeck",new[]{-.09f,.08f},new[]{.068f,.078f},new[]{.064f,.070f}),
                new Vector3(0,.55f,0),Vector3.one,skin);
            Plate("VeyrMask",hp+new Vector3(0,-.065f,.137f),.30f,.22f,.055f,shadow);
            Detail("VeyrMaskSeam",hp+new Vector3(0,-.045f,.170f),new Vector3(.20f,.025f,.012f),violet,default,null,true);
            for(int side=-1;side<=1;side+=2)
            {
                Detail("EyeWhite"+side,hp+new Vector3(side*.073f,.040f,.148f),new Vector3(.061f,.025f,.015f),armorLight);
                Detail("Iris"+side,hp+new Vector3(side*.071f,.040f,.160f),new Vector3(.022f,.026f,.010f),violetHot,default,null,true);
                Detail("Brow"+side,hp+new Vector3(side*.071f,.074f,.148f),new Vector3(.079f,.020f,.018f),hair,new Vector3(0,0,side*-12));
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

            // Layered asymmetrical cloth tails keep her readable in motion and from the top-down camera.
            Mesh cape=HeroMeshLibrary.Ribbon("DF_VeyrCape",
                new[]{new Vector3(-.17f,.13f,-.16f),new Vector3(-.23f,-.18f,-.22f),new Vector3(-.31f,-.52f,-.29f),new Vector3(-.43f,-.90f,-.35f)},
                new[]{.13f,.18f,.17f,.055f},.022f);
            Sculpt("Cape",cape,Vector3.zero,Vector3.one,shadow);
            Mesh tailR=HeroMeshLibrary.Ribbon("DF_VeyrTailR",
                new[]{new Vector3(.15f,.10f,-.16f),new Vector3(.22f,-.22f,-.23f),new Vector3(.35f,-.50f,-.29f),new Vector3(.47f,-.78f,-.34f)},
                new[]{.11f,.16f,.14f,.045f},.020f);
            Sculpt("CoatTailR",tailR,Vector3.zero,Vector3.one,Color.Lerp(shadow,violet,.20f));
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
            // SHADOW STEEL base set: dark physical blade with a thin violet energy edge.
            Color bladeMetal=new(.11f,.10f,.15f,1f);
            Color bladeSpine=new(.24f,.21f,.31f,1f);
            Color edge=new(.56f,.12f,.90f,1f);
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Transform weapon=WeaponPivot(s,new Vector3(side*.43f,-.19f,.16f));
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

                Detail("Hilt"+s,new Vector3(0,-.015f,-.035f),new Vector3(.105f,.235f,.105f),bladeSpine,default,"Weapon"+s);
                Detail("Guard"+s,new Vector3(0,-.115f,.060f),new Vector3(.28f,.050f,.12f),bladeMetal,new Vector3(0,0,side*8f),"Weapon"+s);
                Detail("Pommel"+s,new Vector3(0,.13f,-.060f),new Vector3(.11f,.075f,.11f),edge,new Vector3(0,0,45),"Weapon"+s,true);
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
