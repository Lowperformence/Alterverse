using System.Collections.Generic;
using Alterverse.Match;
using Alterverse.Combat;
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
        SceneryBuildingClearance clearance;

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
            clearance=new SceneryBuildingClearance(ArenaLayout.LengthScale);
            BuildSandstoneRanges();
            BuildDust();
            ArenaLayout.StretchTerrain(transform);
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

            Material crust=Mat("GJ504b_FracturedRoseRim",new Color(.34f,.075f,.22f));
            Material lower=Mat("GJ504b_FracturedRoseUndercut",new Color(.18f,.035f,.12f));
            detail.Apply(crust);detail.Apply(lower);
            ShatteredArenaEdge.Build(transform,ShatteredArenaEdge.Perimeter(vertices,xs,zs),crust,crust,lower,meshes);
        }

        void BuildSandstoneRanges()
        {
            Material rock=Mat("GJ504b_RoseQuartzBasalt",new Color(.34f,.082f,.22f));
            Material strata=Mat("GJ504b_PinkSandstoneBands",new Color(.62f,.21f,.39f));
            Material talus=Mat("GJ504b_ObsidianPlumTalus",new Color(.21f,.048f,.14f));
            Material crystal=Mat("GJ504b_MagentaGeode",new Color(.92f,.18f,.58f));
            crystal.EnableKeyword("_EMISSION");
            if(crystal.HasProperty("_EmissionColor"))crystal.SetColor("_EmissionColor",new Color(.52f,.06f,.30f));
            detail.Apply(rock);detail.Apply(strata);detail.Apply(talus);
            PlanetSceneryMaterials.Apply(crystal,ArenaVariant.GJ504b,SceneryFinish.Ice);

            Mesh mesaMesh=CreateStratifiedMesaMesh(83);meshes.Add(mesaMesh);
            Mesh cragMesh=CreateAngularCragMesh(137);meshes.Add(cragMesh);
            Mesh boulderMesh=CreateAngularCragMesh(211);meshes.Add(boulderMesh);
            Mesh spireMesh=CreateQuartzSpireMesh();meshes.Add(spireMesh);

            var dark=new List<CombineInstance>();
            var light=new List<CombineInstance>();
            var baseTalus=new List<CombineInstance>();
            var geodes=new List<CombineInstance>();

            // 1. Symmetric canyon mesa landmarks across all four quadrants and midfield flanks.
            Vector2[] anchors={
                new(35,30),new(18,31),new(39,15),new(8,33),
                new(-35,30),new(-18,31),new(-39,15),new(-8,33),
                new(25,19),new(-25,19),new(15,21),new(-15,21)
            };
            for(int side=-1;side<=1;side+=2)for(int site=0;site<anchors.Length;site++)
            {
                if(site<4&&clearance.Blocks(FrontierPlanetLayout.ObstacleSceneryBounds(side<0?site:site+4)))continue;
                Vector2 a=anchors[site]*side;
                float groundY=FrontierPlanetLayout.Surface(a.x,a.y).y;
                float mainHeight=site==2?2.55f:1.65f+site*.28f;

                // Broad dark basal bedrock shelf anchoring the mesa into the dune field
                baseTalus.Add(new CombineInstance
                {
                    mesh=boulderMesh,
                    transform=Matrix4x4.TRS(new Vector3(a.x,groundY+.14f,a.y),
                        Quaternion.Euler(0,site*41f+side*19f,0),
                        new Vector3(3.15f,.42f,2.45f))
                });

                // Stepped canyon mesa core + eroded upper sandstone cap
                dark.Add(new CombineInstance
                {
                    mesh=mesaMesh,
                    transform=Matrix4x4.TRS(new Vector3(a.x,groundY+mainHeight*.36f,a.y),
                        Quaternion.Euler(0,site*53f+(side<0?0f:180f),side*2.5f),
                        new Vector3(2.35f,mainHeight*.88f,1.82f))
                });
                light.Add(new CombineInstance
                {
                    mesh=mesaMesh,
                    transform=Matrix4x4.TRS(new Vector3(a.x+.18f*side,groundY+mainHeight*.76f,a.y-.12f*side),
                        Quaternion.Euler(2f,site*53f+28f,-side*2f),
                        new Vector3(1.68f,mainHeight*.42f,1.32f))
                });

                // Flanking wind-carved crag buttresses
                for(int wing=-1;wing<=1;wing+=2)
                {
                    Vector3 wp=new(a.x+wing*1.42f,groundY+mainHeight*.24f,a.y+wing*.58f*side);
                    var buttress=new CombineInstance
                    {
                        mesh=cragMesh,
                        transform=Matrix4x4.TRS(wp,
                            Quaternion.Euler(wing*5f,site*67f+wing*43f,wing*-6f),
                            new Vector3(1.18f,mainHeight*.58f,.96f))
                    };
                    if(wing<0)dark.Add(buttress);else light.Add(buttress);
                }

                // Natural talus boulder skirt around the mesa base
                for(int i=0;i<14;i++)
                {
                    float angle=i*2.399963f+site*.7f,r=1.85f+(i%4)*.68f;
                    float bx=a.x+Mathf.Cos(angle)*r,bz=a.y+Mathf.Sin(angle)*r*.68f;
                    float by=FrontierPlanetLayout.Surface(bx,bz).y;
                    float size=.22f+(i%4)*.11f;
                    var item=new CombineInstance
                    {
                        mesh=i%2==0?boulderMesh:cragMesh,
                        transform=Matrix4x4.TRS(new Vector3(bx,by+size*.22f,bz),
                            Quaternion.Euler(6+(i%3)*4,i*47+site*23,10-(i%3)*5),
                            new Vector3(size*1.45f,size*(.72f+(i%3)*.22f),size*1.12f))
                    };
                    if(i%3==0)baseTalus.Add(item);
                    else if(i%4==1)light.Add(item);
                    else dark.Add(item);
                }

                // Multi-spire rose-quartz geode cluster erupting from the mesa flank
                AddCrystalCluster(geodes,baseTalus,spireMesh,boulderMesh,
                    a.x-side*1.55f,a.y-side*.75f,site*37+side*11,1.08f);
            }

            // 2. Organic canyon rock outcroppings across all four quadrants and midfield shoulders.
            Vector2[] shelfClusters={
                new(14f,24f),new(22f,26f),new(29f,23f),new(36f,22f),
                new(16f,33f),new(26f,32f),new(33f,18f),new(28f,15f),new(38f,8f),
                new(-14f,24f),new(-22f,26f),new(-29f,23f),new(-36f,22f),
                new(-16f,33f),new(-26f,32f),new(-33f,18f),new(-28f,15f),new(-38f,8f),
                new(20f,13.5f),new(-20f,13.5f),new(13.8f,14.5f),new(-13.8f,14.5f),new(0f,21.5f)
            };
            for(int side=-1;side<=1;side+=2)
            for(int c=0;c<shelfClusters.Length;c++)
            {
                float cx=shelfClusters[c].x*side,cz=shelfClusters[c].y*side;
                if(Mathf.Abs(cx)<13f&&Mathf.Abs(cz)<13f)continue;
                float cy=FrontierPlanetLayout.Surface(cx,cz).y;
                float h=1.05f+(c%4)*.38f;
                float w=.88f+(c%3)*.26f;

                // Primary chiseled canyon crag
                dark.Add(new CombineInstance
                {
                    mesh=c%2==0?mesaMesh:cragMesh,
                    transform=Matrix4x4.TRS(new Vector3(cx,cy+h*.34f,cz),
                        Quaternion.Euler(4f,(c*59+side*31)%360,side*5f),
                        new Vector3(w*1.25f,h,w*.95f))
                });
                // Eroded sandstone strata ledge overlapping the crag
                light.Add(new CombineInstance
                {
                    mesh=mesaMesh,
                    transform=Matrix4x4.TRS(new Vector3(cx+.42f*side,cy+h*.22f,cz-.32f*side),
                        Quaternion.Euler(-3f,(c*83+17)%360,-side*4f),
                        new Vector3(w*1.05f,h*.56f,w*.82f))
                });
                // 3 basal talus rocks tying the outcropping into the sand
                for(int k=0;k<3;k++)
                {
                    float ka=(k*2.1f+c)*Mathf.PI*.65f;
                    float kx=cx+Mathf.Cos(ka)*w*1.15f,kz=cz+Mathf.Sin(ka)*w*.95f;
                    float ky=FrontierPlanetLayout.Surface(kx,kz).y;
                    float ks=.26f+k*.09f;
                    baseTalus.Add(new CombineInstance
                    {
                        mesh=boulderMesh,
                        transform=Matrix4x4.TRS(new Vector3(kx,ky+ks*.20f,kz),
                            Quaternion.Euler(8f,c*41+k*67,6f),
                            new Vector3(ks*1.4f,ks*.75f,ks*1.1f))
                    });
                }
                if(c%2==0)
                    AddCrystalCluster(geodes,baseTalus,spireMesh,boulderMesh,
                        cx-.65f*side,cz+.55f*side,c*43+side*7,.86f+(c%3)*.16f);
            }

            // 3. Low wind-eroded sandstone slabs and pebble fans along the mid-ground transition.
            for(int i=0;i<68;i++)
            {
                float a=i*2.399963f,r=13.2f+(i%7)*2.75f;
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r*.82f;
                if(Mathf.Abs(x)<11.5f&&Mathf.Abs(z)<11.5f)continue;
                float size=.24f+(i%4)*.12f;
                float y=FrontierPlanetLayout.Surface(x,z).y;
                var slab=new CombineInstance
                {
                    mesh=i%3==0?mesaMesh:boulderMesh,
                    transform=Matrix4x4.TRS(new Vector3(x,y+size*.20f,z),
                        Quaternion.Euler(3+(i%3)*3,i*41,8-(i%2)*14),
                        new Vector3(size*1.65f,size*.62f,size*1.18f))
                };
                if(i%4==0)light.Add(slab);
                else if(i%3==0)baseTalus.Add(slab);
                else dark.Add(slab);
            }

            // 4. Stratified canyon rim formations & jagged cliff teeth around the arena perimeter.
            for(int edge=0;edge<4;edge++)for(int i=1;i<=13;i++)
            {
                float t=-1f+i/7f;
                float x,z;
                switch(edge)
                {
                    case 0:x=t*39.5f;z=35.45f+(i%3)*.32f;break;
                    case 1:x=t*39.5f;z=-35.45f-(i%3)*.32f;break;
                    case 2:x=41.45f+(i%2)*.32f;z=t*33f;break;
                    default:x=-41.45f-(i%2)*.32f;z=t*33f;break;
                }
                float h=1.15f+(i*3+edge)%5*.38f;
                float width=1.05f+(i+edge)%4*.28f;
                float sy=FrontierPlanetLayout.Surface(x,z).y;
                Vector3 p=new(x,sy+h*.32f,z);
                var tooth=new CombineInstance
                {
                    mesh=i%2==0?mesaMesh:cragMesh,
                    transform=Matrix4x4.TRS(p,
                        Quaternion.Euler(6+(i%3)*4,(i*47+edge*61)%360,(i%2==0?-1:1)*(6+(i%3)*3)),
                        new Vector3(width,h,.92f+(i+edge)%3*.28f))
                };
                if((i+edge)%3==0)light.Add(tooth);else dark.Add(tooth);

                // Low basal cliff shoulder rock at each rim tooth
                baseTalus.Add(new CombineInstance
                {
                    mesh=boulderMesh,
                    transform=Matrix4x4.TRS(new Vector3(x*.985f,sy+.22f,z*.985f),
                        Quaternion.Euler(0,(i*73+edge*29)%360,0),
                        new Vector3(width*1.32f,h*.36f,width*1.05f))
                });
            }

            // 5. Rose-quartz multi-crystal geode clusters framing the outer court and lane shoulders.
            for(int i=0;i<24;i++)
            {
                float a=i*2.399963f,r=16.5f+(i%3)*6.2f;
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r*.84f;
                if(Mathf.Abs(x)<12.5f&&Mathf.Abs(z)<12.5f)continue;
                if(Mathf.Abs(z)>13f&&Mathf.Abs(z)<19f)continue; // Keep north/south lanes clear
                float scale=.72f+(i%4)*.18f;
                AddCrystalCluster(geodes,baseTalus,spireMesh,boulderMesh,x,z,i*31+7,scale);
            }

            Batch("DF_GJ504bSandstoneMesas",dark,rock);
            Batch("DF_GJ504bSedimentaryLayers",light,strata);
            Batch("DF_GJ504bBasaltTalus",baseTalus,talus);
            Batch("DF_GJ504bMagentaGeodes",geodes,crystal);
        }

        static void AddCrystalCluster(List<CombineInstance> geodes,List<CombineInstance> talus,
            Mesh spireMesh,Mesh socketMesh,float x,float z,int seed,float scale)
        {
            float y=FrontierPlanetLayout.Surface(x,z).y;
            // Dark basalt socket stone anchoring the crystal cluster
            talus.Add(new CombineInstance
            {
                mesh=socketMesh,
                transform=Matrix4x4.TRS(new Vector3(x,y+.08f*scale,z),
                    Quaternion.Euler(0,seed*29f,0),
                    new Vector3(.62f*scale,.22f*scale,.56f*scale))
            });
            // Central tall hexagonal rose-quartz spire
            float mainH=(.68f+(seed&3)*.14f)*scale;
            geodes.Add(new CombineInstance
            {
                mesh=spireMesh,
                transform=Matrix4x4.TRS(new Vector3(x,y+mainH*.42f,z),
                    Quaternion.Euler(5f,seed*37f,6f),
                    new Vector3(.26f*scale,mainH,.26f*scale))
            });
            // 3 angled secondary crystal shards sprouting around the main spire
            for(int s=0;s<3;s++)
            {
                float sa=(s*120f+seed*23f)*Mathf.Deg2Rad;
                float ox=Mathf.Cos(sa)*.20f*scale,oz=Mathf.Sin(sa)*.20f*scale;
                float sh=mainH*(.58f+(s%2)*.16f);
                Quaternion rot=Quaternion.Euler(0,sa*Mathf.Rad2Deg,0)*Quaternion.Euler(18f+s*5f,0,12f-s*6f);
                geodes.Add(new CombineInstance
                {
                    mesh=spireMesh,
                    transform=Matrix4x4.TRS(new Vector3(x+ox,y+sh*.36f,z+oz),
                        rot,new Vector3(.17f*scale,sh,.17f*scale))
                });
            }
        }

        // Stepped, flat-faceted canyon mesa mesh with horizontal sedimentary ledges and vertical flutes.
        static Mesh CreateStratifiedMesaMesh(int seed)
        {
            const int sides=18,tiers=6;
            float[] heights={-.42f,-.16f,.08f,.34f,.58f,.82f};
            float[] radii={1.06f,.96f,.88f,.74f,.66f,.48f};
            Vector3[] rings=new Vector3[sides*tiers];
            for(int t=0;t<tiers;t++)for(int i=0;i<sides;i++)
            {
                float a=(i+.06f*Mathf.Sin(i*5+seed))*Mathf.PI*2f/sides;
                float flute=1f+.14f*Mathf.Cos(i*3f+seed)+.08f*Mathf.Sin(i*7f+seed*1.7f);
                float ledge=t%2==1?.94f:1.03f;
                float r=radii[t]*flute*ledge;
                float y=heights[t]+.025f*Mathf.Sin(i*4f+seed+t);
                rings[t*sides+i]=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
            }
            return BuildFacetedColumnMesh("DF_GJ504b_StratifiedMesa_"+seed,rings,sides,tiers,-.42f,.84f);
        }

        // Chiseled, asymmetric fractured boulder / crag mesh with crisp planar rock faces.
        static Mesh CreateAngularCragMesh(int seed)
        {
            const int sides=14,tiers=5;
            float[] heights={-.38f,-.08f,.24f,.56f,.84f};
            float[] radii={.98f,1.04f,.82f,.54f,.24f};
            Vector3[] rings=new Vector3[sides*tiers];
            for(int t=0;t<tiers;t++)for(int i=0;i<sides;i++)
            {
                float a=(i+.09f*Mathf.Sin(i*6+seed))*Mathf.PI*2f/sides;
                float facet=1f+.21f*Mathf.Sin(i*4f+seed*1.3f)+.11f*Mathf.Cos(i*9f+t*2f+seed);
                float r=radii[t]*facet;
                float drift=t>=2?.10f*(t-1):0f;
                float y=heights[t]+.05f*Mathf.Sin(i*3f+seed+t);
                rings[t*sides+i]=new Vector3(Mathf.Cos(a)*r+drift,y,Mathf.Sin(a)*r);
            }
            return BuildFacetedColumnMesh("DF_GJ504b_AngularCrag_"+seed,rings,sides,tiers,-.38f,.88f);
        }

        // Crisp 6-sided hexagonal quartz prism with pointed pyramid termination.
        static Mesh CreateQuartzSpireMesh()
        {
            const int sides=6,tiers=3;
            float[] heights={-.45f,.32f,.68f};
            float[] radii={.52f,.44f,.22f};
            Vector3[] rings=new Vector3[sides*tiers];
            for(int t=0;t<tiers;t++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2f/sides;
                float asym=1f+(i%2==0?.06f:-.05f);
                rings[t*sides+i]=new Vector3(Mathf.Cos(a)*radii[t]*asym,heights[t],Mathf.Sin(a)*radii[t]*asym);
            }
            return BuildFacetedColumnMesh("DF_GJ504b_QuartzSpire",rings,sides,tiers,-.45f,1.02f);
        }

        static Mesh BuildFacetedColumnMesh(string name,Vector3[] rings,int sides,int tiers,float bottomY,float apexY)
        {
            var vertices=new List<Vector3>();
            var uvs=new List<Vector2>();
            var indices=new List<int>();
            for(int t=0;t<tiers-1;t++)for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides,a=t*sides+i,b=t*sides+j,c=a+sides,d=b+sides;
                AddFace(vertices,uvs,indices,rings[a],rings[c],rings[d]);
                AddFace(vertices,uvs,indices,rings[a],rings[d],rings[b]);
            }
            Vector3 bottom=new(0,bottomY,0),apex=new(0,apexY,0);
            int topRing=(tiers-1)*sides;
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;
                AddFace(vertices,uvs,indices,bottom,rings[i],rings[j]);
                AddFace(vertices,uvs,indices,apex,rings[topRing+j],rings[topRing+i]);
            }
            Mesh mesh=new(){name=name};
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uvs);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddFace(List<Vector3> v,List<Vector2> uv,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {
            int idx=v.Count;
            v.Add(a);v.Add(b);v.Add(c);
            uv.Add(new Vector2(a.x*.5f+.5f,a.y*.5f+.5f));
            uv.Add(new Vector2(b.x*.5f+.5f,b.y*.5f+.5f));
            uv.Add(new Vector2(c.x*.5f+.5f,c.y*.5f+.5f));
            indices.Add(idx);indices.Add(idx+1);indices.Add(idx+2);
        }

        void Batch(string name,List<CombineInstance> instances,Material material)
        {
            instances.RemoveAll(item=>clearance.Blocks(item.mesh,transform.localToWorldMatrix*item.transform));
            if(instances.Count==0)return;
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
