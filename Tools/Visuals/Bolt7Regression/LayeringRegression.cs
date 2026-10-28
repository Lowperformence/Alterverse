using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Alterverse.Visuals;
using UnityEngine;

// Production sampler with controlled poses on BOLT-7's authored 19-bone hierarchy.
// No native Unity rendering or original mesh/animation-data validation is implied.
static class LayeringRegression
{
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static bool Same(Vector3 a,Vector3 b)=>Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)+Math.Abs(a.z-b.z)<.00001f;
    static bool Same(Quaternion a,Quaternion b)=>Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)+Math.Abs(a.z-b.z)+Math.Abs(a.w-b.w)<.00001f;
    static void Main()
    {
        string[] names={"Root","Pelvis","Chest","Head","Shoulder.R","UpperArm.R","Weapon.R","GatlingSpin","RamCollar",
            "Shoulder.L","UpperArm.L","Weapon.L","GrenadeDrum","Thigh.R","Shin.R","Foot.R","Thigh.L","Shin.L","Foot.L"};
        int[] parents={-1,0,1,2,2,4,5,6,6,2,9,10,11,1,13,14,1,16,17};
        string directory=Path.Combine(Path.GetTempPath(),"bolt-layer-"+Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(directory,"Bolt7"));Resources.ResourceRoot=directory;
            var bones=names.Select((name,i)=>new {name,parent=parents[i],pose=new float[]{0,0,0,0,0,0,1}}).ToArray();
            var clips=Enumerable.Range(0,15).Select(index=>{
                float[] poses=new float[2*19*7];
                for(int frame=0;frame<2;frame++)for(int i=0;i<19;i++)
                {
                    int p=(frame*19+i)*7;float angle=(index+1)*.025f*(i+1)+frame*.1f;
                    poses[p]=index+frame*.2f;poses[p+1]=i*.01f;poses[p+2]=frame*.03f;
                    poses[p+3]=(float)Math.Sin(angle);poses[p+6]=(float)Math.Cos(angle);
                }
                return new {name=index==0?"Walk":index==1?"Fire_Loop":"Clip"+index,fps=1,frames=2,poses};
            }).ToArray();
            var data=new {version=1,bones,sockets=new[]{new {name="socket_gatling_muzzle",parent=6,pose=new float[]{0,0,0,0,0,0,1}}},
                materials=new[]{"fixture"},vertices=new float[]{0,0,0,1,0,0,0,1,0},normals=new float[]{0,0,1,0,0,1,0,0,1},
                uvs=new float[]{0,0,1,0,0,1},boneIndices=new[]{0,1,2},submeshes=new[]{new {triangles=new[]{0,1,2}}},clips};
            using(var file=File.Create(Path.Combine(directory,"Bolt7/Model.bytes")))
            using(var zip=new GZipStream(file,CompressionMode.Compress))JsonSerializer.Serialize(zip,data);
            var actor=new GameObject("sampling actor");var model=Bolt7Model.Create(actor.transform);
            Check(model!=null,"Production model loader accepts controlled rig data");
            var transforms=model.Root.GetComponentsInChildren<Transform>(true);
            var chain=names.Select(name=>transforms.Single(t=>t.name==name)).ToArray();
            var walk=model.Clips.Single(c=>c.name=="Walk");var fire=model.Clips.Single(c=>c.name=="Fire_Loop");
            foreach(float time in new[]{0f,.37f,1f})
            {
                model.Sample(walk,time);var walkP=chain.Select(b=>b.localPosition).ToArray();var walkQ=chain.Select(b=>b.localRotation).ToArray();
                model.Sample(fire,time);var fireP=chain.Select(b=>b.localPosition).ToArray();var fireQ=chain.Select(b=>b.localRotation).ToArray();
                model.Sample(walk,time);model.Sample(fire,time,true);
                for(int i=0;i<19;i++)
                {
                    bool upper=i>=2&&i<=12;
                    Check(Same(chain[i].localPosition,upper?fireP[i]:walkP[i]),"Layered local position: "+names[i]);
                    Check(Same(chain[i].localRotation,upper?fireQ[i]:walkQ[i]),"Layered local rotation: "+names[i]);
                }
                Check(Same(actor.transform.position,Vector3.zero),"Layering never moves the authoritative actor");
                model.Sample(fire,time);
                for(int i=0;i<19;i++)Check(Same(chain[i].localPosition,fireP[i])&&Same(chain[i].localRotation,fireQ[i]),"Full-body sampling restores "+names[i]);
            }
            Console.WriteLine("PASS: "+checks+" BOLT-7 production sampler/layer mask checks (controlled poses, authored hierarchy)");
        }
        finally{Directory.Delete(directory,true);}
    }
}
