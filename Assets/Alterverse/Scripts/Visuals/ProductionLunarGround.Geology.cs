using Alterverse.Match;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    public sealed partial class ProductionLunarGround
    {
        void BuildReferenceSilhouettes()
        {
            // Large silhouette groups stay at the broken perimeter, never in the combat corridors.
            Vector2[] sites={new(-32,29),new(30,-30),new(-12,31),new(14,-32),new(-38,-24),new(37,24)};
            Material fractureFace=Mat(Color.Lerp(rimMat.color,Color.white,.16f),.02f,.14f);
            surfaceMaterials.Apply(fractureFace);
            for(int site=0;site<sites.Length;site++)
            {
                bool zero=sites[site].x<0;
                // Three overlapping masses per site read as a layered outcrop, not loose pebbles.
                LandmarkRock("DF_PerimeterRockMass",sites[site]+new Vector2(-.68f,-.28f),
                    new Vector3(1.65f,1.05f,1.28f),site*51f+13f,0,darkRegolith);
                LandmarkRock("DF_PerimeterRockMass",sites[site]+new Vector2(.42f,.22f),
                    new Vector3(1.40f,.83f,1.12f),site*61f+27f,.10f,rimMat);
                LandmarkRock("DF_PerimeterRockCap",sites[site]+new Vector2(-.18f,.54f),
                    new Vector3(.92f,.38f,.72f),site*43f+39f,.59f,fractureFace);
                for(int i=0;i<6;i++)
                {
                    Vector2 p=sites[site]+new Vector2((i%3-1)*1.15f,(i/3)*1.45f);
                    bool spire=i<3;float height=spire?(1.9f+(i%3)*.6f):.35f;
                    LandmarkRock("DF_PerimeterFractureSpire",p,new Vector3(spire?.8f:.45f,height,spire?.7f:.38f),site*39+i*(zero?47:13),0,i%2==0?darkRegolith:rimMat);
                }
                Vector2 a=sites[site]+new Vector2(-1.2f,-.6f),b=sites[site]+new Vector2(.6f,-.1f);
                LandmarkSeam(Vector2.Lerp(a,b,.35f),Vector2.Lerp(a,b,.65f),.035f,zero?zeroEnergy:lumenEnergy);
            }
            // Two small dead crystalline growths. Opaque geometry, neutral stone, no grass cards.
            foreach(Vector2 site in new[]{new Vector2(-29,28),new Vector2(27,-29)})
                for(int i=0;i<5;i++)LandmarkRock("DF_DeadLunarGrowth",site+new Vector2(Mathf.Sin(i*2.4f)*.45f,Mathf.Cos(i*2.4f)*.4f),
                    new Vector3(.08f,.48f+i%3*.16f,.07f),i*71,0,darkRegolith);
        }
        void BuildDistributedCraterDebris()
        {
            // Sparse rim fragments make selected impacts readable from the gameplay camera.
            // Craters themselves remain surface relief; these rocks only punctuate a few rims.
            Vector3[] impacts={
                new(-31,.92f,17),new(-26,.88f,-15),new(-14,.62f,13),new(-13,.82f,-15),
                new(-2,.68f,12),new(-1,.9f,-13),new(11,.64f,12),new(12,.86f,-14),
                new(27,.92f,-16),new(32,.58f,18),new(-20,.74f,-20),new(19,.78f,-21)
            };
            for(int site=0;site<impacts.Length;site++)
            {
                Vector3 impact=impacts[site];
                for(int i=0;i<3;i++)
                {
                    float angle=(site*37+i*121+18)*Mathf.Deg2Rad;
                    float radius=impact.y*(1.05f+i*.16f);
                    Vector2 p=new Vector2(impact.x+Mathf.Cos(angle)*radius,impact.z+Mathf.Sin(angle)*radius);
                    float size=.18f+(site+i)%3*.09f;
                    LandmarkRock("DF_CraterRimFragment",p,new Vector3(size,.12f+(i%2)*.08f,size*.78f),site*53+i*79,0,
                        (site+i)%3==0?darkRegolith:rimMat);
                }
            }
        }

        void BuildDensityClusters()
        {
            // Authored pockets with deliberate negative space: no rocks in core, objective or high-traffic combat corridors.
            Vector2[] centers={new(-24,-8),new(-16,7),new(24,8),new(17,-7),new(-29,26),new(-15,-27),new(28,-26),new(16,28)};
            Vector2[] offsets={new(-1.1f,.25f),new(-.28f,-.5f),new(.6f,.3f),new(1.05f,.9f),new(-.9f,1.2f),new(.25f,1.45f)};
            for(int site=0;site<centers.Length;site++)
            {
                float angle=site*1.73f,cos=Mathf.Cos(angle),sin=Mathf.Sin(angle);
                for(int i=0;i<offsets.Length;i++)
                {
                    Vector2 o=offsets[i]*(1+(site%3)*.22f);
                    Vector2 p=centers[site]+new Vector2(o.x*cos-o.y*sin,o.x*sin+o.y*cos);
                    float size=i==0?.85f:i==1?.6f:.2f+(i%3)*.12f;
                    // Low, partly buried slabs; outer pockets carry more relief than traversable interior.
                    LandmarkRock("DF_RegolithDebrisPocket",p,new Vector3(size,size*(site<4?.3f:.6f),size*.7f),site*37+i*71,0,i%3==0?darkRegolith:rimMat);
                }
            }
        }
        void BuildSmallLandmarks()
        {
            // Three authored outer sites, away from objectives, bases and the central collision.
            // Static, collider-free fragments join the existing material/spatial batches.
            Vector2 impact=new(-23,27);
            for(int i=0;i<7;i++)
            {
                float angle=(i*47+18)*Mathf.Deg2Rad,radius=2.1f+(i%3)*.42f;
                Vector2 p=impact+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                LandmarkRock("DF_AncientImpactEjecta",p,new Vector3(.42f+i%3*.19f,.24f+i%2*.12f,.32f+i%3*.13f),i*53,0,i%2==0?rimMat:darkRegolith);
            }
            // A fractured stone held just above an old void seam; no particles or moving object loop.
            Vector2 fracture=new(-18,-26);
            LandmarkSeam(fracture+new Vector2(-.32f,-.05f),fracture+new Vector2(.24f,.12f),.035f,zeroEnergy);
            for(int i=0;i<3;i++)LandmarkRock("DF_SuspendedVoidFragment",fracture+new Vector2((i-1)*.63f,(i%2)*.3f),
                new Vector3(.38f,.3f+i*.07f,.3f),31+i*67,.55f+i*.16f,darkRegolith);
            // Restrained stabilization splinters in neutral lunar stone; only a short vein emits light.
            Vector2 lumen=new(23,26);
            for(int i=0;i<3;i++)LandmarkRock("DF_LumenStabilizationRemnant",lumen+new Vector2((i-1)*.46f,i*.18f),
                new Vector3(.2f,.48f+i*.16f,.23f),i*41,0,rimMat);
            LandmarkSeam(lumen+new Vector2(-.7f,-.23f),lumen+new Vector2(.35f,-.08f),.035f,lumenEnergy);

            BuildSatelliteDebris();
        }

        void BuildSatelliteDebris()
        {
            // Tiny destroyed-satellite remnants shared by every arena variant. MeshObject adds only a
            // MeshFilter/MeshRenderer, so these pieces are deliberately collider-free and never affect
            // hero movement, bot pathing or navigation. They are also folded into the static batches below.
            Material satelliteMetal=Mat(new Color(.18f,.195f,.215f,1),.68f,.24f);
            Material scorchedMetal=Mat(new Color(.065f,.072f,.082f,1),.52f,.13f);
            Material deadPanel=Mat(new Color(.055f,.075f,.105f,1),.34f,.17f);
            Vector2[] sites={
                new(-35,20),new(-31,-18),new(-24,26),new(-19,-24),
                new(-12,19),new(-8,-28),new(9,27),new(14,-20),
                new(22,24),new(28,-18),new(34,12),new(36,-8),
                new(-30,5),new(30,-5),new(-4,22),new(5,-22)
            };
            for(int i=0;i<sites.Length;i++)
            {
                Vector2 s=sites[i];Vector3 p=LunarSurfaceField.Surface(s.x,s.y);p.y+=.025f;
                Mesh shape=i%2==0?rockMeshB:rockMeshC;
                Material mat=i%3==0?scorchedMetal:satelliteMetal;
                GameObject shard=MeshObject("DF_SatelliteScrap",shape,mat,p);
                float length=.22f+(i%4)*.035f;
                shard.transform.localScale=i%3==0?new Vector3(length,.035f,.11f):new Vector3(.11f,.04f,length);
                shard.transform.localRotation=Quaternion.Euler(6+(i%3)*5,i*61f,4+(i%2)*11f);

                // A few sites get one additional tiny panel or antenna fragment, keeping the motif subtle.
                if(i%3!=1)continue;
                Vector2 o=new Vector2(((i&1)==0?.22f:-.18f),((i%4)<2?.14f:-.16f));
                Vector3 q=LunarSurfaceField.Surface(s.x+o.x,s.y+o.y);q.y+=.018f;
                GameObject panel=MeshObject("DF_SatellitePanelFragment",rockMeshC,deadPanel,q);
                panel.transform.localScale=new Vector3(.16f+(i%2)*.04f,.022f,.105f);
                panel.transform.localRotation=Quaternion.Euler(3,i*47f+23f,i%2==0?7f:-8f);
            }
        }
        void LandmarkRock(string name,Vector2 site,Vector3 scale,float yaw,float lift,Material material)
        {
            Vector3 position=LunarSurfaceField.Surface(site.x,site.y);position.y+=lift;
            int variant=Mathf.Abs(Mathf.RoundToInt(yaw))%3;
            GameObject g=MeshObject(name,variant==0?rockMesh:variant==1?rockMeshB:rockMeshC,material,position);
            g.transform.localScale=scale;
            g.transform.localRotation=Quaternion.Euler(5+variant*5,yaw,-8+variant*7);
        }
        void LandmarkSeam(Vector2 a,Vector2 b,float width,Material material)
        {
            Vector2 direction=(b-a).normalized,normal=new Vector2(-direction.y,direction.x)*width*.5f;
            Vector2[] samples={a-normal,a+normal,b+normal,b-normal};Vector3[] vertices=new Vector3[4];
            for(int i=0;i<4;i++)vertices[i]=LunarSurfaceField.Surface(samples[i].x,samples[i].y)+Vector3.up*.018f;
            Mesh mesh=new(){name="DF_SmallLandmarkSeam",vertices=vertices,triangles=new[]{0,1,2,0,2,3}};
            mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);MeshObject("DF_SmallLandmarkSeam",mesh,material,Vector3.zero);
        }
        void BuildGeologicalSlab()
        {
            if(perimeter==null)return;
            Material middle=Mat(ArenaVariantRuntime.IsMars?new Color(.23f,.085f,.055f):
                ArenaVariantRuntime.IsGreenPlanet?new Color(.11f,.17f,.105f):
                ArenaVariantRuntime.IsNoxara?new Color(.035f,.06f,.15f):
                ArenaVariantRuntime.IsKryon?new Color(.30f,.42f,.47f):new Color(.125f,.137f,.153f),0,.08f);
            surfaceMaterials.Apply(middle);
            Material crust=rimMat,underside=darkRegolith;
            // These worlds share the ground builder but need their own exposed geology.
            Color? tint=ArenaVariantRuntime.Current switch
            {
                ArenaVariant.Earth=>new Color(.24f,.22f,.16f),
                ArenaVariant.Pyraxis=>new Color(.14f,.075f,.055f),
                ArenaVariant.Neptune=>new Color(.07f,.14f,.23f),
                ArenaVariant.Dark00=>new Color(.065f,.070f,.090f),
                ArenaVariant.Wasp76b=>new Color(.34f,.23f,.16f),
                ArenaVariant.Trappist1e=>new Color(.13f,.26f,.27f),
                _=>(Color?)null
            };
            if(tint.HasValue)
            {
                crust=Mat(tint.Value,ArenaVariantRuntime.IsWasp76b?.48f:.02f,ArenaVariantRuntime.IsWasp76b?.34f:.12f);
                middle.color=tint.Value*.70f;middle.SetColor("_BaseColor",middle.color);
                if(ArenaVariantRuntime.IsWasp76b)middle.SetFloat("_Metallic",.40f);
                underside=Mat(tint.Value*.38f,ArenaVariantRuntime.IsWasp76b?.35f:.01f,.08f);
                surfaceMaterials.Apply(crust);surfaceMaterials.Apply(underside);
            }
            ShatteredArenaEdge.Build(root,perimeter,crust,middle,underside,meshes);
        }

        static void AddFace(List<Vector3> vertices,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(start);indices.Add(start+1);indices.Add(start+2);}
        void BatchStaticDetails()
        {
            // Partition by material AND spatial cell, preserving useful camera culling.
            var groups=new Dictionary<(Material,int,int,bool),List<CombineInstance>>();
            var sources=new HashSet<Mesh>();
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            foreach(MeshRenderer renderer in renderers)
            {
                MeshFilter filter=renderer.GetComponent<MeshFilter>();
                if(filter==null||filter.sharedMesh==null||renderer.sharedMaterial==regolith)continue;
                Vector3 center=renderer.bounds.center;
                bool casts=renderer.gameObject.name.Contains("Rock")||
                    renderer.gameObject.name.Contains("Fragment")||
                    renderer.gameObject.name.Contains("FractureSpire")||
                    renderer.gameObject.name.Contains("Cliff");
                var key=(renderer.sharedMaterial,Mathf.FloorToInt(center.x/18),Mathf.FloorToInt(center.z/18),casts);
                if(!groups.TryGetValue(key,out var list)){list=new List<CombineInstance>();groups.Add(key,list);}
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                sources.Add(filter.sharedMesh);renderer.enabled=false;Destroy(renderer.gameObject);
            }
            foreach(var group in groups)
            {
                Mesh mesh=new(){name="DF_GeologyBatch"};int count=0;foreach(var item in group.Value)count+=item.mesh.vertexCount;
                if(count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.CombineMeshes(group.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                GameObject batch=MeshObject("DF_StaticGeology",mesh,group.Key.Item1,Vector3.zero);
                if(group.Key.Item4)batch.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.On;
            }
            foreach(Mesh source in sources)if(meshes.Remove(source))Destroy(source);
        }
    }
}
