using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    public sealed partial class ProductionLunarGround
    {
        void BuildSmallLandmarks()
        {
            // Three authored outer sites, away from lanes (z +/-16), objectives and the central collision.
            // Static, collider-free fragments join the existing material/spatial batches.
            Vector2 impact=new(-23,27);
            for(int i=0;i<7;i++)
            {
                float angle=(i*47+18)*Mathf.Deg2Rad,radius=2.1f+(i%3)*.42f;
                Vector2 p=impact+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                LandmarkRock("DF_AncientImpactEjecta",p,new Vector3(.42f+i%3*.19f,.24f+i%2*.12f,.32f+i%3*.13f),i*53,0,i%2==0?rimMat:darkRegolith);
            }
            for(int i=0;i<3;i++)
            {
                Vector2 a=impact+new Vector2(-1.2f+i*.9f,-.4f+i*.26f);
                LandmarkSeam(a,a+new Vector2(.8f,.55f),.12f,darkRegolith);
            }
            // A fractured stone held just above an old void seam; no particles or moving object loop.
            Vector2 fracture=new(-18,-26);
            LandmarkSeam(fracture+new Vector2(-1.1f,-.5f),fracture+new Vector2(.15f,.1f),.14f,darkRegolith);
            LandmarkSeam(fracture+new Vector2(.15f,.1f),fracture+new Vector2(1.2f,.45f),.09f,darkRegolith);
            LandmarkSeam(fracture+new Vector2(-.32f,-.05f),fracture+new Vector2(.24f,.12f),.035f,zeroEnergy);
            for(int i=0;i<3;i++)LandmarkRock("DF_SuspendedVoidFragment",fracture+new Vector2((i-1)*.63f,(i%2)*.3f),
                new Vector3(.38f,.3f+i*.07f,.3f),31+i*67,.55f+i*.16f,darkRegolith);
            // Restrained stabilization splinters in neutral lunar stone; only a short vein emits light.
            Vector2 lumen=new(23,26);
            for(int i=0;i<3;i++)LandmarkRock("DF_LumenStabilizationRemnant",lumen+new Vector2((i-1)*.46f,i*.18f),
                new Vector3(.2f,.48f+i*.16f,.23f),i*41,0,rimMat);
            LandmarkSeam(lumen+new Vector2(-.7f,-.23f),lumen+new Vector2(.35f,-.08f),.035f,lumenEnergy);
        }
        void LandmarkRock(string name,Vector2 site,Vector3 scale,float yaw,float lift,Material material)
        {
            Vector3 position=LunarSurfaceField.Surface(site.x,site.y);position.y+=lift;
            GameObject g=MeshObject(name,rockMeshB,material,position);
            g.transform.localScale=scale;g.transform.localRotation=Quaternion.Euler(9,yaw,-12);
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
            // Shared rim positions close the top surface exactly. Lower rings pull inward into a massive undercut.
            Vector3[] upper=perimeter;
            float[] levels={-1.7f,-5.2f,-9.8f};float[] inset={1.025f,.96f,.77f};
            for(int layer=0;layer<3;layer++)
            {
                Vector3[] lower=new Vector3[perimeter.Length];
                for(int i=0;i<lower.Length;i++)
                {
                    Vector3 p=perimeter[i];float n=Mathf.PerlinNoise(p.x*.24f+31+layer*4,p.z*.24f+18);
                    float scale=inset[layer]+(n-.5f)*.055f;
                    lower[i]=new Vector3(p.x*scale,levels[layer]-(n-.5f)*(layer+1)*1.4f,p.z*scale);
                }
                var vertices=new List<Vector3>(upper.Length*6);var indices=new List<int>(upper.Length*6);
                for(int i=0;i<upper.Length;i++)
                {
                    int j=(i+1)%upper.Length;
                    AddFace(vertices,indices,upper[i],upper[j],lower[i]);AddFace(vertices,indices,upper[j],lower[j],lower[i]);
                }
                Mesh mesh=new(){name="DF_MoonStratum_"+layer};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                Material mat=layer==0?rimMat:layer==1?regolith:darkRegolith;
                // The surface atlas is not stretched vertically across a cliff: strata use their own neutral rock material.
                if(layer==1)mat=Mat(new Color(.125f,.137f,.153f),0,.08f);
                MeshObject("DF_FracturedMoonStratum",mesh,mat,Vector3.zero);
                upper=lower;
            }
            // Close the underside. No visible collision plane is added.
            var bottomVertices=new List<Vector3>(upper.Length+1){new Vector3(0,-11,0)};bottomVertices.AddRange(upper);
            var bottomIndices=new List<int>(upper.Length*3);
            for(int i=0;i<upper.Length;i++){bottomIndices.Add(0);bottomIndices.Add(i+1);bottomIndices.Add((i+1)%upper.Length+1);}
            Mesh bottom=new(){name="DF_MoonUndercut"};bottom.SetVertices(bottomVertices);bottom.SetTriangles(bottomIndices,0);bottom.RecalculateNormals();bottom.RecalculateBounds();meshes.Add(bottom);
            MeshObject("DF_MoonUndercut",bottom,darkRegolith,Vector3.zero);
        }
        static void AddFace(List<Vector3> vertices,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(start);indices.Add(start+1);indices.Add(start+2);}
        void BatchStaticDetails()
        {
            // Partition by material AND spatial cell, preserving useful camera culling.
            var groups=new Dictionary<(Material,int,int),List<CombineInstance>>();
            var sources=new HashSet<Mesh>();
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            foreach(MeshRenderer renderer in renderers)
            {
                MeshFilter filter=renderer.GetComponent<MeshFilter>();
                if(filter==null||filter.sharedMesh==null||renderer.gameObject.name=="DF_LunarRegolith")continue;
                Vector3 center=renderer.bounds.center;
                var key=(renderer.sharedMaterial,Mathf.FloorToInt(center.x/18),Mathf.FloorToInt(center.z/18));
                if(!groups.TryGetValue(key,out var list)){list=new List<CombineInstance>();groups.Add(key,list);}
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                sources.Add(filter.sharedMesh);renderer.enabled=false;Destroy(renderer.gameObject);
            }
            foreach(var group in groups)
            {
                Mesh mesh=new(){name="DF_GeologyBatch"};int count=0;foreach(var item in group.Value)count+=item.mesh.vertexCount;
                if(count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.CombineMeshes(group.Value.ToArray(),true,true);mesh.RecalculateBounds();meshes.Add(mesh);
                MeshObject("DF_StaticGeology",mesh,group.Key.Item1,Vector3.zero);
            }
            foreach(Mesh source in sources)if(meshes.Remove(source))Destroy(source);
        }
    }
}
