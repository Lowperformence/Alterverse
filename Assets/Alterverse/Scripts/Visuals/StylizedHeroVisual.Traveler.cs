using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // Premium presentation only. Gameplay statistics and hitboxes remain owned by the shared combat systems.
        private void BuildTravelerTimeSkirmisher()
        {
            Color voidBlack=new(.012f,.014f,.024f,1f);
            Color charcoal=new(.045f,.050f,.065f,1f);
            Color chronosteel=new(.23f,.26f,.31f,1f);
            Color paleGold=new(.94f,.82f,.54f,1f);
            Color lightCore=new(.96f,.95f,.84f,1f);
            Color paradoxRed=new(1f,.035f,.025f,1f);

            Mesh torso=HeroMeshLibrary.Loft("DF_TravelerTorso",
                new[]{-.40f,-.24f,-.04f,.18f,.37f,.50f},
                new[]{.17f,.20f,.27f,.34f,.36f,.21f},
                new[]{.11f,.13f,.18f,.21f,.22f,.13f},
                new[]{.012f,.012f,.018f,.024f,.015f,0f},16);
            Sculpt("Torso",torso,new Vector3(0,.04f,0),new Vector3(.94f,1f,.94f),voidBlack);
            Plate("TravelerChestL",new Vector3(-.15f,.23f,.205f),.37f,.43f,.105f,charcoal,-8);
            Plate("TravelerChestR",new Vector3(.15f,.23f,.205f),.37f,.43f,.105f,charcoal,8);
            Plate("TravelerGorget",new Vector3(0,.49f,.075f),.50f,.17f,.19f,chronosteel);
            Detail("TravelerChestLine",new Vector3(0,.24f,.292f),new Vector3(.040f,.30f,.018f),paleGold,default,null,true);

            GameObject chestOrbit=Sculpt("TravelerChestOrbit",HeroMeshLibrary.Ring("DF_TravelerChestOrbitMesh",.125f,.018f),
                new Vector3(0,.27f,.307f),Vector3.one,paleGold,null,true);
            chestOrbit.transform.localRotation=Quaternion.Euler(90,0,0);
            Detail("TravelerChestCore",new Vector3(0,.27f,.323f),new Vector3(.070f,.070f,.021f),lightCore,new Vector3(0,0,45),null,true);

            Mesh pelvis=HeroMeshLibrary.Loft("DF_TravelerPelvis",
                new[]{-.43f,-.30f,-.18f},new[]{.19f,.25f,.20f},new[]{.11f,.14f,.11f});
            Sculpt("Pelvis",pelvis,new Vector3(0,-.03f,0),Vector3.one,charcoal);

            float legX=.16f;
            Mesh leg=HeroMeshLibrary.Loft("DF_TravelerLeg",
                new[]{-1f,-.75f,-.20f,.12f,.64f,1f},
                new[]{.052f,.067f,.074f,.103f,.112f,.092f},
                new[]{.065f,.075f,.082f,.102f,.105f,.086f},
                new[]{.015f,.012f,0f,.004f,0f,0f});
            Mesh boot=HeroMeshLibrary.Loft("DF_TravelerBoot",
                new[]{-.09f,-.03f,.08f,.18f},new[]{.098f,.108f,.090f,.055f},
                new[]{.16f,.185f,.14f,.078f},new[]{.055f,.068f,.042f,0f});

            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";
                Sculpt("Leg"+suffix,leg,new Vector3(side*legX,-.62f,0),new Vector3(1f,.36f,1f),voidBlack);
                Sculpt("Boot"+suffix,boot,new Vector3(side*legX,-.94f,.024f),Vector3.one,chronosteel);
                Plate("TravelerKnee"+suffix,new Vector3(side*legX,-.58f,.105f),.16f,.20f,.075f,chronosteel,side*-5,"Leg"+suffix);
                Plate("TravelerShin"+suffix,new Vector3(side*legX,-.74f,.095f),.145f,.30f,.065f,charcoal,side*3,"Leg"+suffix);

                Transform arm=new GameObject("DF_Arm"+suffix).transform;
                arm.SetParent(root,false);
                arm.localPosition=new Vector3(side*.35f,.39f,.012f);
                arm.localRotation=Quaternion.Euler(-8f,0,side*-10f);

                Mesh upper=HeroMeshLibrary.Loft("DF_TravelerUpperArm",
                    new[]{-.32f,-.24f,-.05f,.04f},
                    new[]{.054f,.071f,.095f,.073f},
                    new[]{.060f,.078f,.093f,.069f});
                Sculpt("UpperArm"+suffix,upper,Vector3.zero,Vector3.one,voidBlack,arm);

                Mesh fore=HeroMeshLibrary.Loft("DF_TravelerForearm",
                    new[]{-.28f,-.20f,-.055f,.025f},
                    new[]{.052f,.080f,.090f,.061f},
                    new[]{.058f,.081f,.093f,.060f});
                Sculpt("Vambrace"+suffix,fore,new Vector3(0,-.30f,.02f),Vector3.one,chronosteel,arm);

                Mesh glove=HeroMeshLibrary.Loft("DF_TravelerGlove",
                    new[]{-.13f,-.07f,.07f,.11f},
                    new[]{.044f,.070f,.075f,.055f},
                    new[]{.050f,.062f,.068f,.047f});
                Sculpt("Glove"+suffix,glove,new Vector3(0,-.59f,.052f),Vector3.one,charcoal,arm);

                Plate("TravelerShoulder"+suffix,new Vector3(side*.38f,.43f,0),.30f,.22f,.27f,
                    side<0?charcoal:chronosteel,side*-16);
                Bind("TravelerShoulder"+suffix,"Arm"+suffix);
                Detail("TravelerArmChronoLine"+suffix,new Vector3(0,-.31f,.106f),
                    new Vector3(.026f,.19f,.015f),paleGold,new Vector3(0,0,side*7f),"Arm"+suffix,true);
            }

            // Hooded, faceless reference silhouette. Only the red eyes identify a living face.
            Vector3 headPos=new(0,.80f,.012f);
            Mesh hood=HeroMeshLibrary.Loft("DF_TravelerHood",
                new[]{-.25f,-.18f,-.05f,.10f,.22f,.31f},
                new[]{.072f,.142f,.195f,.207f,.180f,.072f},
                new[]{.074f,.135f,.172f,.174f,.140f,.060f},
                new[]{.050f,.038f,.018f,0f,-.010f,-.020f},16);
            Sculpt("Head",hood,headPos,new Vector3(1.08f,1.07f,1.06f),voidBlack);
            Sculpt("Neck",HeroMeshLibrary.Loft("DF_TravelerNeck",new[]{-.09f,.08f},new[]{.070f,.078f},new[]{.066f,.073f}),
                new Vector3(0,.55f,0),Vector3.one,voidBlack);
            Plate("TravelerFaceMask",headPos+new Vector3(0,-.025f,.166f),.33f,.30f,.062f,charcoal);
            Detail("TravelerEyeL",headPos+new Vector3(-.072f,.055f,.205f),new Vector3(.095f,.022f,.014f),
                paradoxRed,new Vector3(0,0,-7),null,true);
            Detail("TravelerEyeR",headPos+new Vector3(.072f,.055f,.205f),new Vector3(.095f,.022f,.014f),
                paradoxRed,new Vector3(0,0,7),null,true);
            Plate("TravelerHoodBrow",headPos+new Vector3(0,.145f,.090f),.40f,.20f,.18f,voidBlack);
            for(int side=-1;side<=1;side+=2)
                Plate("TravelerHoodWing"+side,headPos+new Vector3(side*.19f,.08f,-.025f),.13f,.42f,.16f,charcoal,side*-12);

            // Long split mantle that keeps the reference's dark mysterious read from the gameplay camera.
            Mesh cloakL=HeroMeshLibrary.Ribbon("DF_TravelerCloakL",
                new[]{new Vector3(-.16f,.14f,-.13f),new Vector3(-.21f,-.15f,-.20f),
                    new Vector3(-.28f,-.48f,-.28f),new Vector3(-.43f,-.88f,-.36f)},
                new[]{.15f,.19f,.17f,.045f},.026f);
            Sculpt("TravelerCloakL",cloakL,Vector3.zero,Vector3.one,voidBlack);
            Mesh cloakR=HeroMeshLibrary.Ribbon("DF_TravelerCloakR",
                new[]{new Vector3(.16f,.14f,-.13f),new Vector3(.21f,-.15f,-.20f),
                    new Vector3(.28f,-.48f,-.28f),new Vector3(.43f,-.88f,-.36f)},
                new[]{.15f,.19f,.17f,.045f},.026f);
            Sculpt("TravelerCloakR",cloakR,Vector3.zero,Vector3.one,charcoal);
            Mesh center=HeroMeshLibrary.Ribbon("DF_TravelerCloakCenter",
                new[]{new Vector3(0,.08f,-.16f),new Vector3(0,-.26f,-.23f),
                    new Vector3(.01f,-.57f,-.31f),new Vector3(0,-.84f,-.35f)},
                new[]{.09f,.12f,.10f,.025f},.020f);
            Sculpt("TravelerCloakCenter",center,Vector3.zero,Vector3.one,chronosteel);

            BuildTravelerChronoluxBase(voidBlack,chronosteel,paleGold,lightCore,paradoxRed);
            Detail("IdentityCore",new Vector3(0,.27f,.325f),new Vector3(.048f,.048f,.016f),
                lightCore,new Vector3(0,0,45),null,true);
        }

        private void BuildTravelerChronoluxBase(Color voidBlack,Color chronosteel,Color paleGold,Color lightCore,Color paradoxRed)
        {
            Transform weapon=WeaponPivot("R",new Vector3(.48f,-.10f,.17f));
            weapon.localRotation=Quaternion.Euler(0,-5f,-8f);

            Mesh shaft=HeroMeshLibrary.Loft("DF_ChronoluxShaft",
                new[]{-.90f,-.66f,.08f,.66f,.88f},
                new[]{.022f,.033f,.030f,.035f,.018f},
                new[]{.023f,.034f,.031f,.036f,.019f},
                new[]{0f,.006f,.010f,.028f,.045f});
            Sculpt("TravelerStaffShaft",shaft,Vector3.zero,Vector3.one,voidBlack,weapon);
            Create("TravelerStaffGrip",PrimitiveType.Cube,new Vector3(0,-.16f,.01f),
                new Vector3(.068f,.27f,.068f),chronosteel,default,weapon);
            Create("TravelerStaffLowerCap",PrimitiveType.Cube,new Vector3(0,-.85f,.01f),
                new Vector3(.085f,.12f,.085f),paleGold,new Vector3(0,0,45),weapon);

            // Named Orb so the shared projectile delivery automatically uses the time-core as its release point.
            Mesh core=HeroMeshLibrary.Loft("DF_ChronoluxCore",
                new[]{-.19f,-.07f,.07f,.21f},
                new[]{.005f,.105f,.115f,.004f},
                new[]{.005f,.090f,.100f,.004f});
            Sculpt("Orb",core,new Vector3(0,.80f,.065f),Vector3.one,lightCore,weapon,true);

            GameObject ringA=Sculpt("ChronoluxRingA",HeroMeshLibrary.Ring("DF_ChronoluxRingA",.235f,.019f),
                new Vector3(0,.80f,.065f),Vector3.one,paleGold,weapon,true);
            ringA.transform.localRotation=Quaternion.Euler(72f,0,22f);
            GameObject ringB=Sculpt("ChronoluxRingB",HeroMeshLibrary.Ring("DF_ChronoluxRingB",.175f,.014f),
                new Vector3(0,.80f,.065f),Vector3.one,lightCore,weapon,true);
            ringB.transform.localRotation=Quaternion.Euler(28f,62f,-18f);

            for(int side=-1;side<=1;side+=2)
            {
                Create("TravelerStaffProng"+side,PrimitiveType.Cube,new Vector3(side*.18f,.83f,.035f),
                    new Vector3(.052f,.27f,.060f),chronosteel,new Vector3(0,0,side*-30f),weapon);
                Create("TravelerParadoxGem"+side,PrimitiveType.Cube,new Vector3(side*.25f,.76f,.052f),
                    new Vector3(.046f,.080f,.034f),paradoxRed,new Vector3(0,0,45),weapon);
            }
            Bind("WeaponR","ArmR");
        }

        private void ApplyChronoluxLegendary(Shader shader,Material energyMaterial)
        {
            Transform weapon=FindVisual("WeaponR");if(weapon==null)return;
            weapon.localScale*=1.12f;

            Color blackSteel=new(.018f,.022f,.032f,1f);
            Material steel=new Material(shader){name="DF_ChronoluxBlackSteel",color=blackSteel};
            if(steel.HasProperty("_BaseColor"))steel.SetColor("_BaseColor",blackSteel);
            if(steel.HasProperty("_Metallic"))steel.SetFloat("_Metallic",.88f);
            if(steel.HasProperty("_Smoothness"))steel.SetFloat("_Smoothness",.46f);
            ownedMaterials.Add(steel);

            Color silver=new(.50f,.54f,.62f,1f);
            Material silverMat=new Material(shader){name="DF_ChronoluxSilver",color=silver};
            if(silverMat.HasProperty("_BaseColor"))silverMat.SetColor("_BaseColor",silver);
            if(silverMat.HasProperty("_Metallic"))silverMat.SetFloat("_Metallic",.94f);
            if(silverMat.HasProperty("_Smoothness"))silverMat.SetFloat("_Smoothness",.62f);
            ownedMaterials.Add(silverMat);

            Color red=new(1f,.028f,.018f,1f);
            Material redMat=new Material(shader){name="DF_ChronoluxParadoxRed",color=red};
            if(redMat.HasProperty("_BaseColor"))redMat.SetColor("_BaseColor",red);
            if(redMat.HasProperty("_Smoothness"))redMat.SetFloat("_Smoothness",.94f);
            if(redMat.HasProperty("_EmissionColor"))
            {
                redMat.EnableKeyword("_EMISSION");
                redMat.SetColor("_EmissionColor",red*3.6f);
            }
            ownedMaterials.Add(redMat);

            Transform shaft=FindVisual("TravelerStaffShaft");if(shaft!=null)ApplyGlow(shaft,steel);
            Transform grip=FindVisual("TravelerStaffGrip");if(grip!=null)ApplyGlow(grip,silverMat);
            Transform cap=FindVisual("TravelerStaffLowerCap");if(cap!=null)ApplyGlow(cap,silverMat);
            Transform orb=FindVisual("Orb");if(orb!=null){orb.localScale*=1.24f;ApplyGlow(orb,energyMaterial);}
            Transform ringA=FindVisual("ChronoluxRingA");if(ringA!=null){ringA.localScale*=1.18f;ApplyGlow(ringA,energyMaterial);}
            Transform ringB=FindVisual("ChronoluxRingB");if(ringB!=null){ringB.localScale*=1.20f;ApplyGlow(ringB,silverMat);}

            for(int side=-1;side<=1;side+=2)
            {
                Transform prong=FindVisual("TravelerStaffProng"+side);if(prong!=null)ApplyGlow(prong,steel);
                Transform gem=FindVisual("TravelerParadoxGem"+side);if(gem!=null){gem.localScale*=1.18f;ApplyGlow(gem,redMat);}
            }

            GameObject haloOuter=Sculpt("ChronoluxLegendaryHaloOuter",
                HeroMeshLibrary.Ring("DF_ChronoluxLegendaryHaloOuterMesh",.34f,.018f),
                new Vector3(0,.80f,.065f),Vector3.one,WeaponSkinCatalog.For(identity.Hero).Glow,weapon,true);
            haloOuter.transform.localRotation=Quaternion.Euler(76,0,38);
            MeshRenderer outerRenderer=haloOuter.GetComponent<MeshRenderer>();if(outerRenderer!=null)outerRenderer.sharedMaterial=energyMaterial;

            GameObject haloInner=Sculpt("ChronoluxLegendaryHaloInner",
                HeroMeshLibrary.Ring("DF_ChronoluxLegendaryHaloInnerMesh",.285f,.013f),
                new Vector3(0,.80f,.065f),Vector3.one,silver,weapon,true);
            haloInner.transform.localRotation=Quaternion.Euler(24,70,-26);
            MeshRenderer innerRenderer=haloInner.GetComponent<MeshRenderer>();if(innerRenderer!=null)innerRenderer.sharedMaterial=silverMat;

            CreateChronoluxPart("ChronoluxRedShardL",weapon,new Vector3(-.36f,.80f,.060f),
                new Vector3(.040f,.14f,.030f),new Vector3(0,0,48),redMat);
            CreateChronoluxPart("ChronoluxRedShardR",weapon,new Vector3(.36f,.80f,.060f),
                new Vector3(.040f,.14f,.030f),new Vector3(0,0,48),redMat);
            CreateChronoluxPart("ChronoluxCrownL",weapon,new Vector3(-.25f,.94f,.035f),
                new Vector3(.052f,.31f,.060f),new Vector3(0,0,34),silverMat);
            CreateChronoluxPart("ChronoluxCrownR",weapon,new Vector3(.25f,.94f,.035f),
                new Vector3(.052f,.31f,.060f),new Vector3(0,0,-34),silverMat);
        }

        private GameObject CreateChronoluxPart(string name,Transform parent,Vector3 localPosition,
            Vector3 localScale,Vector3 localRotation,Material material)
        {
            GameObject part=Create(name,PrimitiveType.Cube,localPosition,localScale,Color.white,localRotation,parent);
            MeshRenderer renderer=part.GetComponent<MeshRenderer>();if(renderer!=null)renderer.sharedMaterial=material;
            return part;
        }
    }
}
