using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>Own sand sea, wind-cut mesas and layered planetary rim. Does not build lunar props.</summary>
    public sealed class GJ504bArenaLandscape : MonoBehaviour
    {
        readonly List<Mesh> meshes=new();
        readonly List<Material> materials=new();
        Texture2D albedo,normals;
        PlanetSurfaceMaterials detail;

        void Start()
        {
            if(!ArenaVariantRuntime.IsGJ504b)return;
            gameObject.AddComponent<LunarLighting>();
            LunarSurfaceField.Bake(out albedo,out normals);
            detail=new PlanetSurfaceMaterials(albedo);
            Material sand=Mat("GJ504b_MagentaSand",Color.white);
            detail.Apply(sand,true);
            sand.SetTexture("_BaseMap",albedo);
            sand.SetTexture("_BumpMap",normals);
            BuildSandSea(sand);
            BuildSandstoneRanges();
            BuildDust();
        }

        Material Mat(string name,Color color)
        {
            Material m=RuntimeShaderLibrary.Create(color,false,.02f,.10f);
            if(m==null)throw new System.InvalidOperationException("GJ 504 b surface shader unavailable");
            m.name=name;m.enableInstancing=true;materials.Add(m);return m;
        }

        void BuildSandSea(Material material)
        {
            const int xs=168,zs=144;
            var vertices=new Vector3[(xs+1)*(zs+1)];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[xs*zs*6];
            for(int z=0;z<=zs;z++)for(int x=0;x<=xs;x++)
            {
                int i=z*(xs+1)+x;
                float px=-42f+84f*x/xs,pz=-36f+72f*z/zs;
                vertices[i]=FrontierPlanetLayout.Surface(px,pz);
                uv[i]=new Vector2((px+48f)/96f,(pz+42f)/84f);
            }
            int t=0;
            for(int z=0;z<zs;z++)for(int x=0;x<xs;x++)
            {
                int a=z*(xs+1)+x,b=a+1,c=a+xs+1,d=c+1;
                triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;
                triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;
            }
            Mesh mesh=new(){name="GJ504b_IndependentDuneSurface",vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();meshes.Add(mesh);
            Render("DF_GJ504bSandSea",mesh,material);
        }

        void BuildSandstoneRanges()
        {
            Material rock=Mat("GJ504b_WeatheredSandstone",new Color(.42f,.09f,.29f));
            Material strata=Mat("GJ504b_PaleSandstoneLayers",new Color(.64f,.22f,.43f));
            detail.Apply(rock);detail.Apply(strata);
            Mesh fragment=LunarFragmentGeometry.Create(83);meshes.Add(fragment);
            var dark=new List<CombineInstance>();var light=new List<CombineInstance>();
            // Four authored range groups: mesas, eroded needles, gravel fans and a low broken rim.
            Vector2[] anchors={new(35,30),new(18,31),new(39,15),new(8,33)};
            for(int side=-1;side<=1;side+=2)for(int site=0;site<anchors.Length;site++)
            {
                Vector2 a=anchors[site]*side;
                for(int layer=0;layer<4;layer++)
                {
                    float height=site==2?2.4f:1.25f+site*.26f;
                    Vector3 p=new(a.x+Mathf.Sin(layer+site)*.35f,height*layer*.28f,a.y);
                    Vector3 scale=new(2.5f-layer*.34f,height*.38f,1.9f-layer*.21f);
                    var ci=new CombineInstance{mesh=fragment,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,site*39f+layer*11f,side*4f),scale)};
                    (layer%2==0?dark:light).Add(ci);
                }
                for(int i=0;i<12;i++)
                {
                    float angle=i*2.399963f,r=2f+(i%4)*.85f;
                    Vector3 p=new(a.x+Mathf.Cos(angle)*r,.05f,a.y+Mathf.Sin(angle)*r*.6f);
                    float size=.16f+(i%3)*.09f;
                    dark.Add(new CombineInstance{mesh=fragment,transform=Matrix4x4.TRS(p,Quaternion.Euler(5,i*43,12),new Vector3(size*1.5f,size*.65f,size))});
                }
            }
            for(int i=0;i<44;i++)
            {
                float a=i*Mathf.PI*2f/44f;
                Vector3 p=new(Mathf.Cos(a)*45f,-1.15f,Mathf.Sin(a)*39f);
                dark.Add(new CombineInstance{mesh=fragment,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,i*31,8),new Vector3(3.3f,1.4f+(i%3)*.35f,2.8f))});
            }
            Batch("DF_GJ504bSandstoneMesas",dark,rock);
            Batch("DF_GJ504bSedimentaryLayers",light,strata);
        }

        void Batch(string name,List<CombineInstance> instances,Material material)
        {
            Mesh mesh=new(){name=name,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(instances.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
            Render(name,mesh,material);
        }

        void Render(string name,Mesh mesh,Material material)
        {
            GameObject go=new(name);go.transform.SetParent(transform,false);go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
        }

        void BuildDust()
        {
            Material material=RuntimeShaderLibrary.Create(new Color(.64f,.25f,.43f),true);
            if(material==null)return;materials.Add(material);
            for(int side=-1;side<=1;side+=2)
            {
                GameObject go=new("DF_GJ504bWindblownSand");go.transform.SetParent(transform,false);
                go.transform.localPosition=new Vector3(side*20f,.13f,side*30f);
                var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=true;main.startLifetime=4f;main.startSpeed=.45f;
                main.startSize=.055f;main.startColor=new Color(.72f,.28f,.50f,.25f);
                main.maxParticles=60;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=ps.emission;emission.rateOverTime=10f;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(22,.10f,3);
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
                velocity.x=1.1f;velocity.y=.025f;velocity.z=.20f;
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;ps.Play();
            }
        }

        void OnDestroy()
        {
            detail?.Dispose();if(albedo!=null)Destroy(albedo);if(normals!=null)Destroy(normals);
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(var material in materials)if(material!=null)Destroy(material);
        }
    }
}
