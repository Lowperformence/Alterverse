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
                uv[i]=new Vector2((vertices[i].x+48f)/96f,(vertices[i].z+42f)/84f);
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
            Material crust=Mat("GJ504b_FracturedRoseRim",new Color(.49f,.055f,.31f));
            Material lower=Mat("GJ504b_FracturedRoseUndercut",new Color(.24f,.035f,.14f));
            detail.Apply(crust);detail.Apply(lower);
            ShatteredArenaEdge.Build(transform,ShatteredArenaEdge.Perimeter(vertices,xs,zs),crust,crust,lower,meshes);
        }

        void BuildSandstoneRanges()
        {
            Material rock=Mat("GJ504b_RoseQuartzBasalt",new Color(.49f,.055f,.31f));
            Material strata=Mat("GJ504b_PinkSandstoneBands",new Color(.94f,.20f,.55f));
            Material crystal=Mat("GJ504b_MagentaGeode",new Color(.86f,.035f,.42f));
            crystal.EnableKeyword("_EMISSION");if(crystal.HasProperty("_EmissionColor"))crystal.SetColor("_EmissionColor",new Color(.30f,.008f,.12f));
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
            // A broken rose-quartz ridgeline fills the side shelves and distant perimeter.
            // Keep the central hot zone and the broad north/south travel corridors clear.
            for(int side=-1;side<=1;side+=2)
            for(int row=0;row<3;row++)
            for(int i=0;i<13;i++)
            {
                float x=side*(12f+row*8f)+(i%2)*1.1f;
                float z=side*(21f+row*5f)+(i-6)*1.55f;
                if(Mathf.Abs(x)<13f&&Mathf.Abs(z)<13f)continue;
                float h=1.1f+(i%5)*.42f+(row%2)*.25f;
                float width=.75f+(i%4)*.28f;
                Vector3 p=new(x,FrontierPlanetLayout.Surface(x,z).y+h*.34f,z);
                Vector3 scale=new(width,h,.72f+(i%3)*.28f);
                var ci=new CombineInstance{mesh=fragment,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,(i*37+row*23)%360,side*5f),scale)};
                if(i%4==0)light.Add(ci);else dark.Add(ci);
            }
            // Low pebble fans visually tie the larger formations into the sand.
            for(int i=0;i<64;i++)
            {
                float a=i*2.399963f,r=13f+(i%7)*2.8f;
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r*.82f;
                if(Mathf.Abs(x)<11f&&Mathf.Abs(z)<11f)continue;
                float size=.24f+(i%4)*.12f;
                dark.Add(new CombineInstance{mesh=fragment,transform=Matrix4x4.TRS(new Vector3(x,FrontierPlanetLayout.Surface(x,z).y+size*.24f,z),Quaternion.Euler(4,i*41,12),new Vector3(size*1.5f,size*.72f,size))});
            }
            // Fractured sandstone teeth break the old straight silhouette at the map edge.
            // They are baked into the existing static batches and never alter the floor or collision.
            for(int edge=0;edge<4;edge++)for(int i=1;i<=11;i++)
            {
                float t=-1f+i/6f;
                float x,z;
                switch(edge)
                {
                    case 0:x=t*39.5f;z=35.45f+(i%3)*.32f;break;
                    case 1:x=t*39.5f;z=-35.45f-(i%3)*.32f;break;
                    case 2:x=41.45f+(i%2)*.32f;z=t*33f;break;
                    default:x=-41.45f-(i%2)*.32f;z=t*33f;break;
                }
                float h=1.05f+(i*3+edge)%5*.34f;
                float width=.92f+(i+edge)%4*.24f;
                Vector3 p=new(x,FrontierPlanetLayout.Surface(x,z).y+h*.30f,z);
                var tooth=new CombineInstance
                {
                    mesh=fragment,
                    transform=Matrix4x4.TRS(p,Quaternion.Euler(8+(i%3)*5,(i*47+edge*61)%360,(i%2==0?-1:1)*(7+(i%3)*3)),
                        new Vector3(width,h,.82f+(i+edge)%3*.25f))
                };
                if((i+edge)%4==0)light.Add(tooth);else dark.Add(tooth);
            }
            Batch("DF_GJ504bSandstoneMesas",dark,rock);
            Batch("DF_GJ504bSedimentaryLayers",light,strata);
            var geodes=new List<CombineInstance>();
            for(int i=0;i<22;i++)
            {
                float a=i*2.399963f,r=17f+(i%3)*6.5f;
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r;
                if(Mathf.Abs(x)<12f&&Mathf.Abs(z)<12f)continue;
                float h=.45f+(i%4)*.22f;
                Vector3 p=new(x,FrontierPlanetLayout.Surface(x,z).y+h*.24f,z);
                geodes.Add(new CombineInstance{mesh=fragment,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,i*29,8),new Vector3(h*.85f,h,h*.72f))});
            }
            Batch("DF_GJ504bMagentaGeodes",geodes,crystal);
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
