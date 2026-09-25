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

            // Full floor-length hooded mantle based on the latest reference.
            // Width reduced by 40% while preserving the long vertical drape and open front.
            Mesh mantleBack=HeroMeshLibrary.Ribbon("DF_TravelerMantleBack",
                new[]{new Vector3(0,.50f,-.10f),new Vector3(0,.22f,-.18f),
                    new Vector3(0,-.18f,-.27f),new Vector3(0,-.62f,-.35f),
                    new Vector3(0,-1.02f,-.40f)},
                new[]{.204f,.276f,.312f,.336f,.288f},.032f);
            Sculpt("TravelerMantleBack",mantleBack,Vector3.zero,Vector3.one,voidBlack);

            Mesh mantleL=HeroMeshLibrary.Ribbon("DF_TravelerMantleL",
                new[]{new Vector3(-.138f,.48f,-.05f),new Vector3(-.204f,.20f,-.14f),
                    new Vector3(-.252f,-.18f,-.22f),new Vector3(-.324f,-.60f,-.29f),
                    new Vector3(-.378f,-1.00f,-.34f)},
                new[]{.120f,.168f,.204f,.228f,.180f},.030f);
            Sculpt("TravelerMantleL",mantleL,Vector3.zero,Vector3.one,voidBlack);

            Mesh mantleR=HeroMeshLibrary.Ribbon("DF_TravelerMantleR",
                new[]{new Vector3(.138f,.48f,-.05f),new Vector3(.204f,.20f,-.14f),
                    new Vector3(.252f,-.18f,-.22f),new Vector3(.324f,-.60f,-.29f),
                    new Vector3(.378f,-1.00f,-.34f)},
                new[]{.120f,.168f,.204f,.228f,.180f},.030f);
            Sculpt("TravelerMantleR",mantleR,Vector3.zero,Vector3.one,voidBlack);

            Mesh innerFoldL=HeroMeshLibrary.Ribbon("DF_TravelerMantleFoldL",
                new[]{new Vector3(-.066f,.38f,-.02f),new Vector3(-.108f,.02f,-.10f),
                    new Vector3(-.138f,-.40f,-.18f),new Vector3(-.162f,-.90f,-.25f)},
                new[]{.033f,.042f,.045f,.027f},.014f);
            Sculpt("TravelerMantleFoldL",innerFoldL,Vector3.zero,Vector3.one,charcoal);

            Mesh innerFoldR=HeroMeshLibrary.Ribbon("DF_TravelerMantleFoldR",
                new[]{new Vector3(.066f,.38f,-.02f),new Vector3(.108f,.02f,-.10f),
                    new Vector3(.138f,-.40f,-.18f),new Vector3(.162f,-.90f,-.25f)},
                new[]{.033f,.042f,.045f,.027f},.014f);
            Sculpt("TravelerMantleFoldR",innerFoldR,Vector3.zero,Vector3.one,charcoal);

            BuildTravelerPlanetFlux(voidBlack,chronosteel,paleGold,lightCore,paradoxRed);
            Detail("IdentityCore",new Vector3(0,.27f,.325f),new Vector3(.048f,.048f,.016f),
                lightCore,new Vector3(0,0,45),null,true);
        }

        private void BuildTravelerPlanetFlux(Color voidBlack,Color chronosteel,Color paleGold,Color lightCore,Color paradoxRed)
        {
            // The Traveler no longer carries a staff. His weapon is a miniature Planet Flux:
            // a floating fractured world held just beyond the right hand.
            Transform weapon=WeaponPivot("R",new Vector3(.48f,-.10f,.18f));
            weapon.localRotation=Quaternion.Euler(8f,-8f,-6f);

            Mesh planet=HeroMeshLibrary.Loft("DF_TravelerPlanetFluxCore",
                new[]{-.15f,-.11f,-.04f,.04f,.11f,.15f},
                new[]{.020f,.095f,.140f,.140f,.095f,.020f},
                new[]{.020f,.090f,.132f,.132f,.090f,.020f},
                new[]{0f,.005f,.010f,.010f,.005f,0f},20);
            Sculpt("Orb",planet,new Vector3(0,-.34f,.28f),Vector3.one,chronosteel,weapon,false);
            GameObject fracture=Sculpt("PlanetFluxEquator",HeroMeshLibrary.Ring("DF_TravelerPlanetFluxEquator",.145f,.012f),
                new Vector3(0,-.34f,.28f),Vector3.one,paradoxRed,weapon,true);
            fracture.transform.localRotation=Quaternion.Euler(77f,0f,12f);

            GameObject ring0=Sculpt("PlanetFluxRing0",HeroMeshLibrary.Ring("DF_TravelerPlanetFluxRing0",.205f,.014f),
                new Vector3(0,-.34f,.28f),Vector3.one,paleGold,weapon,true);
            ring0.transform.localRotation=Quaternion.Euler(70f,0,16f);
            GameObject ring1=Sculpt("PlanetFluxRing1",HeroMeshLibrary.Ring("DF_TravelerPlanetFluxRing1",.255f,.011f),
                new Vector3(0,-.34f,.28f),Vector3.one,lightCore,weapon,true);
            ring1.transform.localRotation=Quaternion.Euler(28f,62f,-18f);
            GameObject ring2=Sculpt("PlanetFluxRing2",HeroMeshLibrary.Ring("DF_TravelerPlanetFluxRing2",.305f,.009f),
                new Vector3(0,-.34f,.28f),Vector3.one,paleGold,weapon,true);
            ring2.transform.localRotation=Quaternion.Euler(82f,44f,38f);

            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f+.35f;
                Vector3 pos=new(Mathf.Cos(a)*.33f,-.34f+Mathf.Sin(a*1.7f)*.055f,.28f+Mathf.Sin(a)*.20f);
                Create("PlanetFluxShard"+i,PrimitiveType.Cube,pos,
                    new Vector3(.038f,.060f,.038f),i%2==0?paradoxRed:chronosteel,
                    new Vector3(18f,i*47f,34f),weapon);
            }

            Detail("PlanetFluxHandGlow",new Vector3(0,-.51f,.17f),new Vector3(.060f,.030f,.060f),
                paleGold,new Vector3(0,0,45),"WeaponR",true);
            Bind("WeaponR","ArmR");
        }

        private void ApplyPlanetFluxEpic(Shader shader,Material energyMaterial)
        {
            Transform weapon=FindVisual("WeaponR");if(weapon==null)return;

            Color white=new(1f,.99f,.94f,1f);
            Material whiteGlow=new Material(shader){name="DF_PlanetFluxEpicWhite",color=white};
            if(whiteGlow.HasProperty("_BaseColor"))whiteGlow.SetColor("_BaseColor",white);
            if(whiteGlow.HasProperty("_Metallic"))whiteGlow.SetFloat("_Metallic",.08f);
            if(whiteGlow.HasProperty("_Smoothness"))whiteGlow.SetFloat("_Smoothness",.96f);
            if(whiteGlow.HasProperty("_EmissionColor"))
            {
                whiteGlow.EnableKeyword("_EMISSION");
                whiteGlow.SetColor("_EmissionColor",white*4.8f);
            }
            ownedMaterials.Add(whiteGlow);

            Color softWhite=new(.88f,.94f,1f,1f);
            Material orbitGlow=new Material(shader){name="DF_PlanetFluxEpicOrbit",color=softWhite};
            if(orbitGlow.HasProperty("_BaseColor"))orbitGlow.SetColor("_BaseColor",softWhite);
            if(orbitGlow.HasProperty("_Smoothness"))orbitGlow.SetFloat("_Smoothness",.92f);
            if(orbitGlow.HasProperty("_EmissionColor"))
            {
                orbitGlow.EnableKeyword("_EMISSION");
                orbitGlow.SetColor("_EmissionColor",softWhite*3.8f);
            }
            ownedMaterials.Add(orbitGlow);

            Transform orb=FindVisual("Orb");if(orb!=null){orb.localScale*=1.10f;ApplyGlow(orb,whiteGlow);}
            for(int i=0;i<3;i++)
            {
                Transform ring=FindVisual("PlanetFluxRing"+i);
                if(ring!=null){ring.localScale*=1.08f+i*.025f;ApplyGlow(ring,i==1?whiteGlow:orbitGlow);}
            }
            for(int i=0;i<4;i++)
            {
                Transform shard=FindVisual("PlanetFluxShard"+i);
                if(shard!=null){shard.localScale*=1.10f;ApplyGlow(shard,whiteGlow);}
            }
            Transform handGlow=FindVisual("PlanetFluxHandGlow");if(handGlow!=null)ApplyGlow(handGlow,whiteGlow);

            GameObject halo=Sculpt("PlanetFluxEpicHalo",
                HeroMeshLibrary.Ring("DF_PlanetFluxEpicHaloMesh",.355f,.012f),
                new Vector3(0,-.34f,.28f),Vector3.one,Color.white,weapon,true);
            halo.transform.localRotation=Quaternion.Euler(38f,24f,68f);
            MeshRenderer haloRenderer=halo.GetComponent<MeshRenderer>();if(haloRenderer!=null)haloRenderer.sharedMaterial=whiteGlow;

            GameObject inner=Sculpt("PlanetFluxEpicInnerOrbit",
                HeroMeshLibrary.Ring("DF_PlanetFluxEpicInnerOrbitMesh",.165f,.010f),
                new Vector3(0,-.34f,.28f),Vector3.one,Color.white,weapon,true);
            inner.transform.localRotation=Quaternion.Euler(76f,65f,-24f);
            MeshRenderer innerRenderer=inner.GetComponent<MeshRenderer>();if(innerRenderer!=null)innerRenderer.sharedMaterial=orbitGlow;
        }
    }
}
