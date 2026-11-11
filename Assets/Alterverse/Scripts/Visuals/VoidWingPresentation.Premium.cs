using System;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    public sealed partial class VoidWingPresentation
    {
        [Serializable] internal sealed class WingMeshData
        {
            public float[] positions,colors;
            public int[] body,facet,energy;
        }
        [Serializable] internal sealed class WingDesignData { public WingMeshData wing,back; }
        [Serializable] internal sealed class WingLibraryData { public WingDesignData angel,rgb; }
        static WingLibraryData premiumLibrary;
        Mesh mountMesh;
        bool angel,rgbRift;

        public void BuildAngel(Transform visual,bool isPreview,HeroId hero)
            =>BuildPremium(visual,isPreview,hero,false);
        public void BuildRgbRift(Transform visual,bool isPreview,HeroId hero)
            =>BuildPremium(visual,isPreview,hero,true);

        void BuildPremium(Transform visual,bool isPreview,HeroId hero,bool rgb)
        {
            if(mesh!=null)return;
            if(premiumLibrary==null)
            {
                var asset=Resources.Load<TextAsset>("Wings/PremiumWingMeshes");
                if(asset==null){Debug.LogWarning("DIMENSIONS FRONT: premium wing mesh asset is missing.");return;}
                premiumLibrary=JsonUtility.FromJson<WingLibraryData>(asset.text);
            }
            var design=rgb?premiumLibrary.rgb:premiumLibrary.angel;
            Shader surface=WingCoreShader();
            Shader energy=rgb?Resources.Load<Shader>("DF_RGBWingEnergy"):RuntimeShaderLibrary.Unlit;
            if(surface==null||energy==null||design==null)return;
            preview=isPreview;angel=!rgb;rgbRift=rgb;
            core=CreateWingMaterial(surface,rgb?"DF_MechanicalAnthracite":"DF_AngelGold",
                rgb?new Color(.045f,.052f,.064f):new Color(.92f,.56f,.12f));
            if(core.HasProperty("_Metallic"))core.SetFloat("_Metallic",rgb?.85f:.88f);
            if(core.HasProperty("_Smoothness"))core.SetFloat("_Smoothness",.76f);
            EnableWingEmission(core,rgb?new Color(.12f,.18f,.32f)*1.25f:new Color(1f,.68f,.18f)*1.55f);

            facet=CreateWingMaterial(surface,rgb?"DF_MechanicalGunmetal":"DF_AngelIvory",
                rgb?new Color(.17f,.19f,.22f):new Color(1f,.93f,.72f));
            if(facet.HasProperty("_Metallic"))facet.SetFloat("_Metallic",.46f);
            if(facet.HasProperty("_Smoothness"))facet.SetFloat("_Smoothness",.72f);
            EnableWingEmission(facet,rgb?new Color(.28f,.42f,.68f)*1.75f:new Color(1f,.92f,.66f)*2.15f);

            energyColor=rgb?Color.white:new Color(1f,.76f,.28f);
            edge=CreateWingMaterial(energy,rgb?"DF_RiftSpectrumEnergy":"DF_AngelWarmLight",energyColor);
            if(rgb){edge.SetFloat("_Intensity",2.95f);}
            else
            {
                edge.EnableKeyword("_EMISSION");
                if(edge.HasProperty("_BaseColor"))edge.SetColor("_BaseColor",energyColor*2.65f);
                if(edge.HasProperty("_Color"))edge.SetColor("_Color",energyColor*2.65f);
                if(edge.HasProperty("_EmissionColor"))edge.SetColor("_EmissionColor",energyColor*2.65f);
            }

            mesh=PremiumMesh(design.wing,rgb?"DF_MechanicalRgbBlades":"DF_AngelGoldenFeathers");
            mountMesh=PremiumMesh(design.back,rgb?"DF_MechanicalBackMount":"DF_AngelAureoleMount");
            float scale=HeroScale(hero,1f,.92f,.94f,.98f,1.10f,1.12f)*WingSize;
            for(int side=0;side<2;side++)
            {
                var wing=new GameObject((rgb?"DF_RgbRiftWing":"DF_AngelWing")+(side==0?"Left":"Right"));
                wing.transform.SetParent(visual,false);wings[side]=wing.transform;
                wing.transform.localPosition=new Vector3(0,.25f,-.34f);
                wing.transform.localScale=new Vector3((side==0?-1:1)*scale,scale,scale);
                PremiumRenderer(wing,mesh);
            }
            var mount=new GameObject(rgb?"DF_RgbRiftBackMount":"DF_AngelBackMount");
            mount.transform.SetParent(visual,false);
            mount.transform.localPosition=new Vector3(0,.25f,-.34f);
            mount.transform.localScale=Vector3.one*scale;
            PremiumRenderer(mount,mountMesh);
            lastPosition=transform.position;
        }

        void PremiumRenderer(GameObject owner,Mesh geometry)
        {
            owner.AddComponent<MeshFilter>().sharedMesh=geometry;
            var renderer=owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=new[]{core,facet,edge};
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }

        internal static Mesh PremiumMesh(WingMeshData data,string label)
        {
            int count=data.positions.Length/3;
            var vertices=new Vector3[count];var colors=new Color[count];
            for(int i=0;i<count;i++)
            {
                vertices[i]=new Vector3(data.positions[i*3],data.positions[i*3+1],data.positions[i*3+2]);
                colors[i]=new Color(data.colors[i*3],data.colors[i*3+1],data.colors[i*3+2],1);
            }
            var geometry=new Mesh{name=label};geometry.vertices=vertices;geometry.colors=colors;
            geometry.subMeshCount=3;
            geometry.SetTriangles(data.body,0,false);geometry.SetTriangles(data.facet,1,false);geometry.SetTriangles(data.energy,2,false);
            geometry.RecalculateNormals();geometry.RecalculateBounds();
            return geometry;
        }
    }
}
