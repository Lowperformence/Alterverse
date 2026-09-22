using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // Reference-driven VOID MAGE presentation for Ione Vaska.
        // Visual only: combat stats, timings, targeting and ability logic remain untouched.
        private void BuildIoneVoidMage()
        {
            Color voidDark=new(.018f,.018f,.038f,1f);
            Color indigo=new(.085f,.070f,.175f,1f);
            Color silver=new(.46f,.51f,.62f,1f);
            Color violetHot=new(.70f,.24f,1f,1f);

            // Tall, narrow arcane torso with layered armor instead of the old open-face caster silhouette.
            Mesh torso=HeroMeshLibrary.Loft("DF_IoneVoidTorso",
                new[]{-.38f,-.23f,-.04f,.17f,.36f,.50f},
                new[]{.18f,.21f,.29f,.38f,.40f,.22f},
                new[]{.12f,.14f,.19f,.24f,.25f,.14f},
                new[]{.01f,.01f,.02f,.025f,.015f,0f},16);
            Sculpt("Torso",torso,new Vector3(0,.05f,0),new Vector3(.96f,1f,.98f),voidDark);
            Plate("MageChestL",new Vector3(-.16f,.23f,.22f),.40f,.47f,.13f,indigo,-7);
            Plate("MageChestR",new Vector3(.16f,.23f,.22f),.40f,.47f,.13f,indigo,7);
            Plate("MageGorget",new Vector3(0,.49f,.08f),.58f,.19f,.23f,silver);
            Plate("MageAbdomen",new Vector3(0,-.07f,.17f),.43f,.31f,.10f,voidDark);

            GameObject chestRing=Sculpt("MageChestRing",HeroMeshLibrary.Ring("DF_MageChestRingMesh",.145f,.027f),
                new Vector3(0,.24f,.326f),Vector3.one,violetHot,null,true);
            chestRing.transform.localRotation=Quaternion.Euler(90,0,0);
            Detail("MageChestCore",new Vector3(0,.24f,.344f),new Vector3(.095f,.095f,.028f),
                violetHot,new Vector3(0,0,45),null,true);

            Mesh pelvis=HeroMeshLibrary.Loft("DF_IoneVoidPelvis",
                new[]{-.44f,-.30f,-.18f},new[]{.21f,.28f,.23f},new[]{.13f,.16f,.13f});
            Sculpt("Pelvis",pelvis,new Vector3(0,-.03f,0),Vector3.one,indigo);

            // Readable mobile legs under a long split robe.
            float legX=.17f;
            Mesh leg=HeroMeshLibrary.Loft("DF_IoneMageLeg",
                new[]{-1f,-.74f,-.20f,.12f,.63f,1f},
                new[]{.055f,.072f,.079f,.11f,.12f,.10f},
                new[]{.070f,.082f,.09f,.11f,.115f,.095f},
                new[]{.018f,.015f,0f,.004f,0f,0f});
            Mesh boot=HeroMeshLibrary.Loft("DF_IoneMageBoot",
                new[]{-.09f,-.03f,.08f,.18f},
                new[]{.105f,.115f,.095f,.060f},
                new[]{.16f,.19f,.145f,.08f},
                new[]{.06f,.075f,.045f,0f});
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"L":"R";
                Sculpt("Leg"+s,leg,new Vector3(side*legX,-.62f,0),new Vector3(1f,.36f,1f),voidDark);
                Sculpt("Boot"+s,boot,new Vector3(side*legX,-.94f,.025f),Vector3.one,silver);
                Plate("MageKnee"+s,new Vector3(side*legX,-.58f,.11f),.18f,.22f,.085f,silver,side*-5,"Leg"+s);
                Plate("MageShin"+s,new Vector3(side*legX,-.74f,.10f),.16f,.31f,.075f,indigo,side*4,"Leg"+s);

                Transform arm=new GameObject("DF_Arm"+s).transform;
                arm.SetParent(root,false);
                arm.localPosition=new Vector3(side*.39f,.40f,.015f);
                arm.localRotation=Quaternion.Euler(-10f,0,side*-11f);

                Mesh upper=HeroMeshLibrary.Loft("DF_IoneMageUpperArm",
                    new[]{-.32f,-.24f,-.05f,.04f},
                    new[]{.060f,.080f,.108f,.082f},
                    new[]{.067f,.085f,.105f,.078f});
                Sculpt("UpperArm"+s,upper,Vector3.zero,Vector3.one,voidDark,arm);
                Mesh fore=HeroMeshLibrary.Loft("DF_IoneMageForearm",
                    new[]{-.28f,-.20f,-.055f,.025f},
                    new[]{.060f,.087f,.10f,.067f},
                    new[]{.066f,.088f,.105f,.066f});
                Sculpt("Vambrace"+s,fore,new Vector3(0,-.30f,.02f),Vector3.one,silver,arm);
                Sculpt("Glove"+s,HeroMeshLibrary.Loft("DF_IoneMageGlove",
                    new[]{-.13f,-.07f,.07f,.11f},
                    new[]{.047f,.078f,.083f,.060f},
                    new[]{.055f,.068f,.076f,.052f}),
                    new Vector3(0,-.59f,.055f),Vector3.one,voidDark,arm);

                // Blade-like pauldrons reduced by 30% for a cleaner mage silhouette.
                Plate("MageShoulder"+s,new Vector3(side*.47f,.48f,.005f),.336f,.224f,.308f,silver,side*-17);
                Bind("MageShoulder"+s,"Arm"+s);
                Plate("MageShoulderWing"+s,new Vector3(side*.60f,.58f,-.02f),.14f,.35f,.119f,indigo,side*-31);
                Bind("MageShoulderWing"+s,"Arm"+s);
                Detail("MageShoulderGem"+s,new Vector3(side*.48f,.52f,.175f),
                    new Vector3(.0595f,.084f,.0175f),violetHot,new Vector3(0,0,45),"Arm"+s,true);
            }

            // Closed crown-mask: no exposed face, only a narrow violet eye slit and a central crystal.
            Vector3 headPos=new(0,.81f,.015f);
            Mesh helmet=HeroMeshLibrary.Loft("DF_IoneVoidCrown",
                new[]{-.23f,-.16f,-.04f,.10f,.20f,.27f},
                new[]{.070f,.135f,.178f,.185f,.155f,.060f},
                new[]{.066f,.125f,.150f,.148f,.118f,.052f},
                new[]{.045f,.034f,.016f,0f,-.006f,-.014f},16);
            Sculpt("Head",helmet,headPos,new Vector3(1.05f,1.04f,1.04f),voidDark);
            Sculpt("Neck",HeroMeshLibrary.Loft("DF_IoneMageNeck",
                new[]{-.09f,.08f},new[]{.073f,.081f},new[]{.068f,.075f}),
                new Vector3(0,.55f,0),Vector3.one,voidDark);
            Plate("MageFaceMask",headPos+new Vector3(0,-.025f,.153f),.34f,.31f,.075f,indigo);
            Detail("MageEyeSlit",headPos+new Vector3(0,.055f,.201f),
                new Vector3(.27f,.024f,.016f),violetHot,default,null,true);
            Plate("MageCrownCenter",headPos+new Vector3(0,.275f,-.015f),.12f,.56f,.16f,silver);
            for(int side=-1;side<=1;side+=2)
            {
                Plate("MageCrownWing"+side,headPos+new Vector3(side*.18f,.22f,-.015f),
                    .12f,.48f,.14f,silver,side*-24);
                Detail("MageCrownGem"+side,headPos+new Vector3(side*.13f,.16f,.132f),
                    new Vector3(.055f,.075f,.018f),violetHot,new Vector3(0,0,45),null,true);
            }
            Detail("MageForeheadGem",headPos+new Vector3(0,.17f,.167f),
                new Vector3(.080f,.115f,.020f),violetHot,new Vector3(0,0,45),null,true);

            // Split robe panels preserve a strong caster silhouette from the gameplay camera.
            Mesh robeL=HeroMeshLibrary.Ribbon("DF_IoneRobeL",
                new[]{new Vector3(-.19f,.10f,-.13f),new Vector3(-.24f,-.20f,-.20f),
                    new Vector3(-.30f,-.53f,-.27f),new Vector3(-.40f,-.92f,-.34f)},
                new[]{.15f,.19f,.18f,.055f},.026f);
            Sculpt("MageRobeL",robeL,Vector3.zero,Vector3.one,voidDark);
            Mesh robeR=HeroMeshLibrary.Ribbon("DF_IoneRobeR",
                new[]{new Vector3(.19f,.10f,-.13f),new Vector3(.24f,-.20f,-.20f),
                    new Vector3(.30f,-.53f,-.27f),new Vector3(.40f,-.92f,-.34f)},
                new[]{.15f,.19f,.18f,.055f},.026f);
            Sculpt("MageRobeR",robeR,Vector3.zero,Vector3.one,indigo);
            Mesh centerRobe=HeroMeshLibrary.Ribbon("DF_IoneCenterRobe",
                new[]{new Vector3(0,.06f,-.15f),new Vector3(0,-.27f,-.22f),
                    new Vector3(.01f,-.58f,-.29f),new Vector3(0,-.86f,-.33f)},
                new[]{.11f,.14f,.12f,.03f},.020f);
            Sculpt("MageCenterRobe",centerRobe,Vector3.zero,Vector3.one,silver);

            // Small visual-only crystal focus in the free hand.
            Transform leftArm=root.Find("DF_ArmL");
            Mesh handCrystal=HeroMeshLibrary.Loft("DF_IoneHandCrystal",
                new[]{-.14f,-.04f,.09f,.19f},new[]{.006f,.075f,.065f,.004f},new[]{.006f,.065f,.060f,.004f});
            Sculpt("ArcaneHandCrystal",handCrystal,new Vector3(0,-.69f,.24f),Vector3.one,violetHot,leftArm,true);
            GameObject handRing=Sculpt("ArcaneHandOrbit",HeroMeshLibrary.Ring("DF_IoneHandOrbitMesh",.14f,.014f),
                new Vector3(0,-.69f,.24f),Vector3.one,violetHot,leftArm,true);
            handRing.transform.localRotation=Quaternion.Euler(72,0,24);

            BuildIoneVoidStaff(voidDark,indigo,silver,violetHot);
            Detail("IdentityCore",new Vector3(0,.24f,.345f),new Vector3(.055f,.055f,.018f),
                violetHot,new Vector3(0,0,45),null,true);
        }

        private void BuildIoneVoidStaff(Color voidDark,Color indigo,Color silver,Color violetHot)
        {
            Transform weapon=WeaponPivot("R",new Vector3(.50f,-.15f,.18f));
            weapon.localRotation=Quaternion.Euler(0,-7f,-9f);

            Mesh shaft=HeroMeshLibrary.Loft("DF_IoneVoidStaffShaft",
                new[]{-.78f,-.60f,.18f,.70f},
                new[]{.030f,.040f,.038f,.030f},
                new[]{.030f,.040f,.038f,.030f},
                new[]{0f,.008f,.012f,.018f});
            Sculpt("MageStaffShaft",shaft,Vector3.zero,Vector3.one,voidDark,weapon);
            Create("MageStaffPommel",PrimitiveType.Cube,new Vector3(0,-.77f,.01f),
                new Vector3(.12f,.13f,.12f),silver,new Vector3(0,0,45),weapon);
            Create("MageStaffCollar",PrimitiveType.Cube,new Vector3(0,.58f,.02f),
                new Vector3(.14f,.13f,.14f),silver,new Vector3(0,0,45),weapon);

            // The staff core is intentionally named Orb: HeroVisualMotion already uses it as the
            // mage's visual release point, so projectiles/VFX originate at the new crystal without gameplay changes.
            Mesh crystal=HeroMeshLibrary.Loft("DF_IoneStaffCrystal",
                new[]{-.23f,-.10f,.10f,.30f},
                new[]{.005f,.15f,.17f,.005f},
                new[]{.005f,.13f,.15f,.005f});
            Sculpt("Orb",crystal,new Vector3(0,.86f,.06f),Vector3.one,violetHot,weapon,true);

            GameObject ringA=Sculpt("MageOrbitRingA",HeroMeshLibrary.Ring("DF_IoneStaffRingA",.28f,.021f),
                new Vector3(0,.86f,.06f),Vector3.one,silver,weapon);
            ringA.transform.localRotation=Quaternion.Euler(68f,0,18f);
            GameObject ringB=Sculpt("MageOrbitRingB",HeroMeshLibrary.Ring("DF_IoneStaffRingB",.22f,.017f),
                new Vector3(0,.86f,.06f),Vector3.one,violetHot,weapon,true);
            ringB.transform.localRotation=Quaternion.Euler(28f,68f,-18f);

            for(int side=-1;side<=1;side+=2)
            {
                Create("MageStaffCrown"+side,PrimitiveType.Cube,new Vector3(side*.23f,.88f,.03f),
                    new Vector3(.075f,.34f,.095f),silver,new Vector3(0,0,side*-31f),weapon);
                Create("MageStaffShard"+side,PrimitiveType.Cube,new Vector3(side*.34f,.81f,.05f),
                    new Vector3(.070f,.12f,.050f),violetHot,new Vector3(0,0,45),weapon);
            }
            Create("MageStaffTopSpike",PrimitiveType.Cube,new Vector3(0,1.18f,.03f),
                new Vector3(.075f,.28f,.075f),silver,Vector3.zero,weapon);

            Bind("WeaponR","ArmR");
        }

        private void ApplyAstralOrbitOverdrive(Shader shader,Material energyMaterial)
        {
            Transform weapon=FindVisual("WeaponR");if(weapon==null)return;
            weapon.localScale*=1.16f;

            Color blackMetal=new(.025f,.030f,.060f,1f);
            Material darkMaterial=new Material(shader){name="DF_AstralOrbitDarkMetal",color=blackMetal};
            if(darkMaterial.HasProperty("_BaseColor"))darkMaterial.SetColor("_BaseColor",blackMetal);
            if(darkMaterial.HasProperty("_Metallic"))darkMaterial.SetFloat("_Metallic",.88f);
            if(darkMaterial.HasProperty("_Smoothness"))darkMaterial.SetFloat("_Smoothness",.48f);
            ownedMaterials.Add(darkMaterial);

            Color moonSilver=new(.58f,.66f,.82f,1f);
            Material silverMaterial=new Material(shader){name="DF_AstralOrbitSilver",color=moonSilver};
            if(silverMaterial.HasProperty("_BaseColor"))silverMaterial.SetColor("_BaseColor",moonSilver);
            if(silverMaterial.HasProperty("_Metallic"))silverMaterial.SetFloat("_Metallic",.92f);
            if(silverMaterial.HasProperty("_Smoothness"))silverMaterial.SetFloat("_Smoothness",.60f);
            ownedMaterials.Add(silverMaterial);

            Transform shaft=FindVisual("MageStaffShaft");if(shaft!=null)ApplyGlow(shaft,darkMaterial);
            Transform pommel=FindVisual("MageStaffPommel");if(pommel!=null)ApplyGlow(pommel,silverMaterial);
            Transform collar=FindVisual("MageStaffCollar");if(collar!=null)ApplyGlow(collar,silverMaterial);
            Transform core=FindVisual("Orb");if(core!=null){core.localScale*=1.24f;ApplyGlow(core,energyMaterial);}
            Transform ringA=FindVisual("MageOrbitRingA");if(ringA!=null){ringA.localScale*=1.18f;ApplyGlow(ringA,silverMaterial);}
            Transform ringB=FindVisual("MageOrbitRingB");if(ringB!=null){ringB.localScale*=1.23f;ApplyGlow(ringB,energyMaterial);}
            Transform top=FindVisual("MageStaffTopSpike");if(top!=null)ApplyGlow(top,silverMaterial);

            for(int side=-1;side<=1;side+=2)
            {
                Transform crown=FindVisual("MageStaffCrown"+side);if(crown!=null)ApplyGlow(crown,darkMaterial);
                Transform shard=FindVisual("MageStaffShard"+side);if(shard!=null){shard.localScale*=1.22f;ApplyGlow(shard,energyMaterial);}
            }

            // Epic shop-only orbital frame and suspended shards.
            GameObject haloA=Sculpt("AstralHaloA",HeroMeshLibrary.Ring("DF_AstralHaloA",.39f,.020f),
                new Vector3(0,.86f,.06f),Vector3.one,WeaponSkinCatalog.For(identity.Hero).Glow,weapon,true);
            haloA.transform.localRotation=Quaternion.Euler(76f,0f,36f);
            MeshRenderer haloARenderer=haloA.GetComponent<MeshRenderer>();if(haloARenderer!=null)haloARenderer.sharedMaterial=energyMaterial;

            GameObject haloB=Sculpt("AstralHaloB",HeroMeshLibrary.Ring("DF_AstralHaloB",.34f,.016f),
                new Vector3(0,.86f,.06f),Vector3.one,WeaponSkinCatalog.For(identity.Hero).Glow,weapon,true);
            haloB.transform.localRotation=Quaternion.Euler(22f,68f,-25f);
            MeshRenderer haloBRenderer=haloB.GetComponent<MeshRenderer>();if(haloBRenderer!=null)haloBRenderer.sharedMaterial=silverMaterial;

            CreateAstralOrbitPart("AstralCrownL",weapon,new Vector3(-.31f,.96f,.03f),
                new Vector3(.070f,.40f,.095f),new Vector3(0,0,34f),silverMaterial);
            CreateAstralOrbitPart("AstralCrownR",weapon,new Vector3(.31f,.96f,.03f),
                new Vector3(.070f,.40f,.095f),new Vector3(0,0,-34f),silverMaterial);
            CreateAstralOrbitPart("AstralShardL",weapon,new Vector3(-.43f,.85f,.06f),
                new Vector3(.075f,.15f,.055f),new Vector3(0,0,45f),energyMaterial);
            CreateAstralOrbitPart("AstralShardR",weapon,new Vector3(.43f,.85f,.06f),
                new Vector3(.075f,.15f,.055f),new Vector3(0,0,45f),energyMaterial);
            CreateAstralOrbitPart("AstralCoreFrame",weapon,new Vector3(0,.86f,.095f),
                new Vector3(.20f,.045f,.025f),new Vector3(0,0,45f),silverMaterial);
        }

        private GameObject CreateAstralOrbitPart(string name,Transform parent,Vector3 localPosition,
            Vector3 localScale,Vector3 localRotation,Material material)
        {
            GameObject part=Create(name,PrimitiveType.Cube,localPosition,localScale,Color.white,localRotation,parent);
            MeshRenderer renderer=part.GetComponent<MeshRenderer>();if(renderer!=null)renderer.sharedMaterial=material;
            return part;
        }
    }
}
