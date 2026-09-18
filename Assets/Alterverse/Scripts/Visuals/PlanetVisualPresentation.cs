using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    public sealed class PlanetVisualPresentation:MonoBehaviour
    {
        private readonly List<Mesh> meshes=new();
        private readonly List<Material> materials=new();
        public void Build(Color color,int variant)
        {
            Material stone=Mat(new Color(.7f,.73f,.78f),false),energy=Mat(Color.Lerp(color,Color.white,.15f),true),debris=Mat(new Color(.12f,.14f,.17f),false);
            var lunar=FindAnyObjectByType<ProductionLunarGround>();
            if(lunar!=null&&lunar.SurfaceAlbedo!=null){stone.SetTexture("_BaseMap",lunar.SurfaceAlbedo);stone.SetTexture("_MainTex",lunar.SurfaceAlbedo);}
            GetComponent<Renderer>().sharedMaterial=stone;
            Mesh sphere=GetComponent<MeshFilter>().sharedMesh;
            Mesh rock=LunarFragmentGeometry.Create(29+variant);meshes.Add(rock);
            for(int i=0;i<3;i++)
            {
                Mesh ring=ProductionMeshFactory.Ring(.93f+i*.21f,i==0?.025f:.014f,40,5);meshes.Add(ring);
                GameObject obj=Part(i==0?"DF_PlanetRing":"DF_PlanetOrbitRing",ring,energy);
                obj.transform.localRotation=Quaternion.Euler(68-i*19,variant*17+i*39,18+i*21);
            }
            // Floating fragments share one fractured mesh instead of alternating primitive cubes/balls.
            for(int i=0;i<5;i++)
            {
                float a=(i*72+variant*21)*Mathf.Deg2Rad;
                GameObject obj=Part("DF_PlanetShard",rock,debris);obj.transform.localPosition=new Vector3(Mathf.Cos(a)*1.45f,Mathf.Sin(i*1.7f)*.24f,Mathf.Sin(a)*1.45f);
                obj.transform.localScale=new Vector3(.16f,.23f,.15f);obj.transform.localRotation=Quaternion.Euler(15,i*47,21);
            }
            if(variant==1)for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;GameObject moon=Part("DF_GuardMoon",sphere,stone);
                moon.transform.localPosition=new Vector3(Mathf.Cos(a)*2.05f,.55f+Mathf.Sin(a)*.35f,Mathf.Sin(a)*2.05f);moon.transform.localScale=Vector3.one*.22f;
            }
            Mesh lens=ProductionMeshFactory.Ring(.3f,.045f,24,6);meshes.Add(lens);
            GameObject core=Part("DF_PlanetCore",lens,energy);core.transform.localPosition=Vector3.up*.46f;
            Mesh beaconMesh=ProductionMeshFactory.Ring(1.2f,.022f,48,5);meshes.Add(beaconMesh);
            GameObject beacon=Part("DF_PlanetGroundBeacon",beaconMesh,energy);beacon.transform.position=new Vector3(transform.position.x,.14f,transform.position.z);
        }
        private GameObject Part(string label,Mesh mesh,Material material)
        {
            GameObject obj=ProductionMeshFactory.MeshObject(label,transform,mesh,material);
            MeshRenderer renderer=obj.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return obj;
        }
        private Material Mat(Color color,bool emission)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material material=new(shader){name="DF_PlanetSurface",color=color};material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",emission?.25f:.09f);material.SetFloat("_Metallic",0);
            if(emission){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*.75f);}materials.Add(material);return material;
        }
        private void OnDestroy(){foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);foreach(Material material in materials)if(material!=null)Destroy(material);}
    }
}
