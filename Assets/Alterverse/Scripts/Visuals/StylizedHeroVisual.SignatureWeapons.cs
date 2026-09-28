using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // The paid set replaces the silhouette of each base weapon. The gameplay pivots,
        // muzzle marker, ownership and combat components remain untouched.
        private void BuildSignatureWeapon(Shader shader, Material energy,int variant=0)
        {
            if(variant==1){BuildAlternateSignatureWeapon(shader,energy);return;}
            Color shellColor = identity.Hero == HeroId.Mira ? new Color(.82f,.64f,.29f) :
                identity.Hero == HeroId.Nyra ? new Color(.16f,.23f,.29f) : new Color(.10f,.13f,.20f);
            Material shell = SignatureMaterial(shader,"Shell",shellColor,.76f,.64f);
            Material edge = SignatureMaterial(shader,"Edge",new Color(.68f,.76f,.84f),.85f,.72f);
            switch(identity.Hero)
            {
                case HeroId.Veyr:
                    for(int side=-1;side<=1;side+=2)
                    {
                        string s=side<0?"L":"R";
                        Transform w=FindVisual("Weapon"+s);if(w==null)continue;
                        HideSignatureBase(w);
                        // Crescent sickles have a separated glowing inner edge and a forked guard.
                        SignatureRibbon("EclipseCrescent"+s,w,new[]{new Vector3(0,-.13f,.06f),new Vector3(side*.11f,-.22f,.34f),new Vector3(side*.27f,-.34f,.75f),new Vector3(side*.32f,-.51f,1.02f),new Vector3(side*.19f,-.62f,1.16f)},new[]{.07f,.11f,.12f,.07f,.001f},.055f,shell);
                        SignatureRibbon("EclipseEdge"+s,w,new[]{new Vector3(side*.04f,-.13f,.08f),new Vector3(side*.15f,-.22f,.36f),new Vector3(side*.32f,-.34f,.76f),new Vector3(side*.37f,-.51f,1.02f),new Vector3(side*.20f,-.62f,1.16f)},new[]{.01f,.018f,.021f,.012f,.001f},.014f,energy);
                        SignatureBlock("EclipseGrip"+s,w,new Vector3(0,0,-.05f),new Vector3(.09f,.26f,.10f),shell);
                        SignatureBlock("EclipseGuard"+s,w,new Vector3(side*.10f,-.12f,.08f),new Vector3(.29f,.045f,.12f),edge,side*22f);
                        SignatureBlock("EclipseHeart"+s,w,new Vector3(0,-.15f,.13f),new Vector3(.09f,.10f,.045f),energy,45f);
                    }
                    break;
                case HeroId.Kaelon:
                {
                    Transform w=FindVisual("WeaponR");if(w==null)break;HideSignatureBase(w);
                    SignatureBlock("QuakeShaft",w,new Vector3(0,.04f,0),new Vector3(.11f,1.28f,.11f),shell);
                    SignatureBlock("QuakeCrown",w,new Vector3(0,.59f,.02f),new Vector3(.82f,.32f,.38f),shell);
                    for(int s=-1;s<=1;s+=2)
                    {
                        SignatureBlock("QuakeJaw"+s,w,new Vector3(s*.44f,.60f,.02f),new Vector3(.17f,.52f,.42f),edge,s*-12f);
                        SignatureBlock("QuakeChannel"+s,w,new Vector3(s*.21f,.60f,.24f),new Vector3(.055f,.24f,.025f),energy);
                    }
                    SignatureBlock("QuakeCap",w,new Vector3(0,.83f,.03f),new Vector3(.42f,.09f,.33f),edge);
                    SignatureBlock("QuakeReactor",w,new Vector3(0,.59f,.24f),new Vector3(.19f,.19f,.045f),energy,45f);
                    break;
                }
                case HeroId.Nyra:
                {
                    Transform w=FindVisual("WeaponR");if(w==null)break;HideSignatureBase(w);
                    SignatureBlock("CometReceiver",w,new Vector3(0,0,.03f),new Vector3(.25f,.25f,.69f),shell);
                    SignatureBlock("CometStock",w,new Vector3(0,-.08f,-.45f),new Vector3(.18f,.21f,.40f),edge);
                    SignatureBlock("CometBarrel",w,new Vector3(0,.01f,.74f),new Vector3(.09f,.09f,1.22f),edge);
                    for(int s=-1;s<=1;s+=2)
                    {
                        SignatureBlock("CometRail"+s,w,new Vector3(s*.145f,.02f,.64f),new Vector3(.04f,.07f,.99f),energy);
                        SignatureBlock("CometMuzzleFin"+s,w,new Vector3(s*.13f,.04f,1.33f),new Vector3(.09f,.22f,.29f),shell,s*-12f);
                    }
                    SignatureBlock("CometScope",w,new Vector3(0,.24f,-.06f),new Vector3(.14f,.14f,.40f),shell);
                    SignatureBlock("CometEye",w,new Vector3(0,.24f,.16f),new Vector3(.09f,.09f,.035f),energy);
                    SignatureBlock("CometCell",w,new Vector3(0,-.22f,.02f),new Vector3(.17f,.24f,.21f),energy);
                    break;
                }
                case HeroId.Elyra:
                {
                    HideSignatureNamed("Orb");HideSignatureNamed("OrbitL");HideSignatureNamed("OrbitR");
                    Transform w=FindVisual("WeaponR");if(w==null){w=WeaponPivot("R",new Vector3(.48f,.15f,.30f));Bind("WeaponR","ArmR");}
                    SignatureBlock("PrismHandle",w,new Vector3(0,-.32f,0),new Vector3(.10f,.55f,.10f),shell);
                    SignatureBlock("PrismRoot",w,new Vector3(0,.02f,0),new Vector3(.27f,.18f,.16f),edge,45f);
                    SignatureRibbon("PrismSpire",w,new[]{new Vector3(0,.01f,0),new Vector3(0,.19f,.03f),new Vector3(0,.43f,.05f),new Vector3(0,.69f,.07f)},new[]{.13f,.19f,.12f,.001f},.095f,edge);
                    SignatureRibbon("PrismCore",w,new[]{new Vector3(0,.10f,.11f),new Vector3(0,.28f,.13f),new Vector3(0,.50f,.12f)},new[]{.02f,.085f,.001f},.020f,energy);
                    SignatureRing("PrismOrbitA",w,new Vector3(0,.26f,.04f),.31f,energy,Quaternion.Euler(65,0,28));
                    SignatureRing("PrismOrbitB",w,new Vector3(0,.29f,.04f),.23f,edge,Quaternion.Euler(25,54,-20));
                    break;
                }
                case HeroId.Mira:
                {
                    Transform w=FindVisual("WeaponR");if(w==null)break;HideSignatureBase(w);
                    SignatureBlock("SunScepter",w,new Vector3(0,.02f,0),new Vector3(.095f,1.63f,.095f),shell);
                    SignatureBlock("SunCollar",w,new Vector3(0,.55f,0),new Vector3(.27f,.14f,.18f),edge);
                    for(int s=-1;s<=1;s+=2)
                    {
                        SignatureRibbon("SunPetal"+s,w,new[]{new Vector3(0,.56f,0),new Vector3(s*.19f,.77f,.02f),new Vector3(s*.28f,1.03f,.03f),new Vector3(s*.09f,1.18f,.03f)},new[]{.06f,.09f,.06f,.001f},.045f,shell);
                        SignatureBlock("SunRay"+s,w,new Vector3(s*.21f,.85f,.09f),new Vector3(.025f,.25f,.025f),energy,s*-15f);
                    }
                    SignatureRing("SunHalo",w,new Vector3(0,.90f,.04f),.31f,energy,Quaternion.Euler(80,0,0));
                    SignatureBlock("SunHeart",w,new Vector3(0,.90f,.09f),new Vector3(.17f,.22f,.08f),energy,45f);
                    break;
                }
                case HeroId.Rook:
                    for(int s=-1;s<=1;s+=2)
                    {
                        string suffix=s<0?"L":"R";Transform w=FindVisual("Gauntlet"+suffix);if(w==null)continue;
                        HideSignatureBase(w);
                        SignatureBlock("RiftFist"+suffix,w,new Vector3(0,-.02f,.10f),new Vector3(.36f,.40f,.38f),shell);
                        SignatureBlock("RiftKnuckle"+suffix,w,new Vector3(0,-.14f,.34f),new Vector3(.34f,.18f,.11f),edge);
                        for(int i=-1;i<=1;i++)
                        {
                            SignatureBlock("RiftClaw"+suffix+i,w,new Vector3(i*.13f,-.27f,.42f),new Vector3(.09f,.38f,.13f),shell,i*8f);
                            SignatureBlock("RiftClawEdge"+suffix+i,w,new Vector3(i*.13f,-.29f,.50f),new Vector3(.027f,.27f,.025f),energy,i*8f);
                        }
                        SignatureBlock("RiftCell"+suffix,w,new Vector3(0,.13f,.30f),new Vector3(.18f,.11f,.06f),energy);
                    }
                    break;
                case HeroId.Traveler:
                {
                    Transform w=FindVisual("WeaponR");if(w==null)break;HideSignatureBase(w);
                    SignatureRing("ParadoxOuter",w,new Vector3(0,-.34f,.28f),.37f,energy,Quaternion.Euler(67,18,24));
                    SignatureRing("ParadoxInner",w,new Vector3(0,-.34f,.28f),.23f,edge,Quaternion.Euler(22,67,-17));
                    SignatureRibbon("ParadoxNeedle",w,new[]{new Vector3(0,-.72f,.28f),new Vector3(-.07f,-.49f,.30f),new Vector3(0,-.22f,.30f),new Vector3(.06f,.12f,.28f)},new[]{.001f,.075f,.11f,.001f},.058f,shell);
                    SignatureBlock("ParadoxCore",w,new Vector3(0,-.34f,.37f),new Vector3(.19f,.19f,.08f),energy,45f);
                    for(int s=-1;s<=1;s+=2)SignatureBlock("ParadoxSatellite"+s,w,new Vector3(s*.32f,-.34f,.30f),new Vector3(.08f,.18f,.075f),edge,s*-28f);
                    break;
                }
            }
        }

        private void BuildAlternateSignatureWeapon(Shader shader,Material energy)
        {
            Color shellColor=identity.Hero==HeroId.Kaelon?new Color(.16f,.12f,.27f):
                identity.Hero==HeroId.Nyra?new Color(.10f,.18f,.25f):
                identity.Hero==HeroId.Mira?new Color(.74f,.82f,.84f):
                identity.Hero==HeroId.Traveler?new Color(.82f,.72f,.48f):new Color(.12f,.10f,.22f);
            Material shell=SignatureMaterial(shader,"AltShell",shellColor,.84f,.72f);
            Material edge=SignatureMaterial(shader,"AltEdge",new Color(.66f,.74f,.84f),.90f,.78f);
            switch(identity.Hero)
            {
                case HeroId.Veyr:
                    for(int side=-1;side<=1;side+=2)
                    {
                        string suffix=side<0?"L":"R";Transform w=FindVisual("Weapon"+suffix);if(w==null)continue;HideSignatureBase(w);
                        SignatureRibbon("DuskglassFang"+suffix,w,new[]{new Vector3(0,-.18f,.02f),new Vector3(side*.10f,-.28f,.29f),new Vector3(side*.24f,-.42f,.62f),new Vector3(side*.20f,-.62f,.88f)},new[]{.09f,.11f,.07f,.001f},.05f,shell);
                        SignatureRibbon("DuskglassEdge"+suffix,w,new[]{new Vector3(side*.03f,-.17f,.07f),new Vector3(side*.13f,-.30f,.32f),new Vector3(side*.27f,-.43f,.65f),new Vector3(side*.22f,-.61f,.86f)},new[]{.012f,.019f,.012f,.001f},.014f,energy);
                        SignatureBlock("DuskglassGrip"+suffix,w,new Vector3(0,0,-.03f),new Vector3(.085f,.28f,.09f),edge);
                        SignatureBlock("DuskglassGuard"+suffix,w,new Vector3(side*.09f,-.10f,.07f),new Vector3(.26f,.045f,.11f),energy,side*20);
                    }
                    break;
                case HeroId.Kaelon:
                {
                    Transform w=FindVisual("WeaponR");if(w==null)break;HideSignatureBase(w);
                    SignatureBlock("AmethystHaft",w,new Vector3(0,-.02f,0),new Vector3(.105f,1.32f,.105f),shell);
                    SignatureBlock("AmethystHeadCore",w,new Vector3(0,.57f,.02f),new Vector3(.58f,.48f,.43f),shell);
                    for(int side=-1;side<=1;side+=2)
                    {
                        SignatureBlock("AmethystStrikeFace"+side,w,new Vector3(side*.34f,.57f,.23f),new Vector3(.22f,.34f,.075f),energy,side*-8);
                        SignatureBlock("AmethystCheek"+side,w,new Vector3(side*.40f,.58f,-.02f),new Vector3(.16f,.56f,.39f),edge,side*-12);
                        SignatureBlock("AmethystVent"+side,w,new Vector3(side*.15f,.57f,.27f),new Vector3(.045f,.28f,.025f),edge);
                    }
                    SignatureRing("AmethystReactor",w,new Vector3(0,.57f,.29f),.16f,energy,Quaternion.Euler(80,0,0));
                    SignatureBlock("AmethystCap",w,new Vector3(0,.84f,.02f),new Vector3(.28f,.10f,.31f),shell);
                    break;
                }
                case HeroId.Nyra:
                {
                    Transform w=FindVisual("WeaponR");if(w==null)break;HideSignatureBase(w);
                    SignatureBlock("AfterburnReceiver",w,new Vector3(0,.01f,.08f),new Vector3(.25f,.24f,.62f),shell);
                    SignatureBlock("AfterburnStock",w,new Vector3(0,-.08f,-.42f),new Vector3(.17f,.18f,.39f),edge);
                    SignatureBlock("AfterburnBarrel",w,new Vector3(0,.02f,.72f),new Vector3(.075f,.075f,1.28f),edge);
                    for(int side=-1;side<=1;side+=2)
                    {
                        SignatureBlock("AfterburnRail"+side,w,new Vector3(side*.13f,.015f,.67f),new Vector3(.035f,.055f,1.04f),energy);
                        SignatureBlock("AfterburnFin"+side,w,new Vector3(side*.105f,.03f,1.27f),new Vector3(.10f,.20f,.27f),shell,side*-13);
                    }
                    SignatureBlock("AfterburnOptic",w,new Vector3(0,.20f,.10f),new Vector3(.13f,.12f,.30f),shell);
                    SignatureBlock("AfterburnLens",w,new Vector3(0,.20f,.26f),new Vector3(.085f,.08f,.04f),energy);
                    SignatureBlock("AfterburnCell",w,new Vector3(0,-.20f,.04f),new Vector3(.14f,.22f,.19f),energy);
                    break;
                }
                case HeroId.Elyra:
                {
                    HideSignatureNamed("Orb");HideSignatureNamed("OrbitL");HideSignatureNamed("OrbitR");
                    Transform w=FindVisual("WeaponR");if(w==null){w=WeaponPivot("R",new Vector3(.48f,.15f,.30f));Bind("WeaponR","ArmR");}
                    HideSignatureBase(w);
                    SignatureBlock("NullstarHandle",w,new Vector3(0,-.30f,0),new Vector3(.09f,.65f,.09f),shell);
                    SignatureRibbon("NullstarSpire",w,new[]{new Vector3(0,-.01f,0),new Vector3(-.13f,.22f,.03f),new Vector3(.08f,.46f,.06f),new Vector3(0,.75f,.05f)},new[]{.09f,.15f,.13f,.001f},.08f,edge);
                    SignatureRing("NullstarRingA",w,new Vector3(0,.34f,.04f),.32f,energy,Quaternion.Euler(65,0,28));
                    SignatureRing("NullstarRingB",w,new Vector3(0,.34f,.04f),.25f,edge,Quaternion.Euler(24,58,-22));
                    SignatureRing("NullstarRingC",w,new Vector3(0,.34f,.04f),.18f,energy,Quaternion.Euler(84,32,18));
                    SignatureBlock("NullstarCore",w,new Vector3(0,.34f,.13f),new Vector3(.12f,.16f,.08f),energy,45);
                    break;
                }
                case HeroId.Mira:
                {
                    Transform w=FindVisual("WeaponR");if(w==null)break;HideSignatureBase(w);
                    SignatureBlock("TidelightShaft",w,new Vector3(0,.02f,0),new Vector3(.09f,1.60f,.09f),shell);
                    SignatureRibbon("TidelightForkL",w,new[]{new Vector3(0,.52f,0),new Vector3(-.16f,.73f,.02f),new Vector3(-.25f,.98f,.03f),new Vector3(-.16f,1.17f,.02f)},new[]{.065f,.08f,.06f,.001f},.04f,edge);
                    SignatureRibbon("TidelightForkR",w,new[]{new Vector3(0,.52f,0),new Vector3(.16f,.73f,.02f),new Vector3(.25f,.98f,.03f),new Vector3(.16f,1.17f,.02f)},new[]{.065f,.08f,.06f,.001f},.04f,edge);
                    SignatureRing("TidelightLensFrame",w,new Vector3(0,.94f,.05f),.24f,energy,Quaternion.Euler(78,0,0));
                    SignatureBlock("TidelightLens",w,new Vector3(0,.94f,.09f),new Vector3(.14f,.19f,.07f),energy,45);
                    SignatureBlock("TidelightGrip",w,new Vector3(0,-.24f,.01f),new Vector3(.14f,.35f,.13f),edge);
                    break;
                }
                case HeroId.Rook:
                    for(int side=-1;side<=1;side+=2)
                    {
                        string suffix=side<0?"L":"R";Transform w=FindVisual("Gauntlet"+suffix);if(w==null)continue;HideSignatureBase(w);
                        SignatureBlock("RiftbreakerShell"+suffix,w,new Vector3(0,-.02f,.12f),new Vector3(.39f,.43f,.40f),shell);
                        SignatureBlock("RiftbreakerKnuckles"+suffix,w,new Vector3(0,-.16f,.38f),new Vector3(.34f,.18f,.12f),edge);
                        for(int i=-1;i<=1;i++)
                        {
                            SignatureBlock("RiftbreakerPiston"+suffix+i,w,new Vector3(i*.13f,-.10f,.47f),new Vector3(.05f,.24f,.09f),energy);
                            SignatureBlock("RiftbreakerRail"+suffix+i,w,new Vector3(i*.13f,-.30f,.42f),new Vector3(.055f,.28f,.14f),shell,i*8);
                        }
                        SignatureBlock("RiftbreakerCore"+suffix,w,new Vector3(0,.14f,.32f),new Vector3(.16f,.11f,.06f),energy);
                    }
                    break;
                case HeroId.Traveler:
                {
                    Transform w=FindVisual("WeaponR");if(w==null)break;HideSignatureBase(w);
                    SignatureBlock("ChronoluxShaft",w,new Vector3(0,-.02f,0),new Vector3(.10f,1.48f,.10f),shell);
                    SignatureRing("ChronoluxOuter",w,new Vector3(0,.61f,.08f),.35f,edge,Quaternion.Euler(68,0,24));
                    SignatureRing("ChronoluxInner",w,new Vector3(0,.61f,.09f),.23f,energy,Quaternion.Euler(22,64,-18));
                    SignatureBlock("ChronoluxCore",w,new Vector3(0,.61f,.14f),new Vector3(.14f,.20f,.09f),energy,45);
                    SignatureBlock("ChronoluxProngL",w,new Vector3(-.25f,.73f,.02f),new Vector3(.06f,.42f,.07f),edge,-24);
                    SignatureBlock("ChronoluxProngR",w,new Vector3(.25f,.73f,.02f),new Vector3(.06f,.42f,.07f),edge,24);
                    SignatureBlock("ChronoluxFoot",w,new Vector3(0,-.74f,.02f),new Vector3(.18f,.12f,.16f),edge);
                    break;
                }
            }
        }

        private Material SignatureMaterial(Shader shader,string label,Color color,float metallic,float smoothness)
        {
            Material material=new Material(shader){name="DF_Signature"+label+identity.Hero,color=color};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metallic);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            ownedMaterials.Add(material);return material;
        }
        private void HideSignatureBase(Transform pivot)
        {
            foreach(Renderer r in pivot.GetComponentsInChildren<Renderer>(true))r.enabled=false;
        }
        private void HideSignatureNamed(string name)
        {
            Transform t=FindVisual(name);if(t!=null)HideSignatureBase(t);
        }
        private void SignatureBlock(string name,Transform parent,Vector3 position,Vector3 scale,Material material,float angle=0f)
        {
            GameObject part=Create("Signature"+name,PrimitiveType.Cube,position,scale,Color.white,new Vector3(0,0,angle),parent);
            part.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private void SignatureRibbon(string name,Transform parent,Vector3[] path,float[] widths,float depth,Material material)
        {
            GameObject part=Sculpt("Signature"+name,HeroMeshLibrary.Ribbon("DF_Signature"+name,path,widths,depth),Vector3.zero,Vector3.one,Color.white,parent);
            part.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private void SignatureRing(string name,Transform parent,Vector3 position,float radius,Material material,Quaternion rotation)
        {
            GameObject part=Sculpt("Signature"+name,HeroMeshLibrary.Ring("DF_Signature"+name,radius,.018f),position,Vector3.one,Color.white,parent);
            part.transform.localRotation=rotation;part.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
