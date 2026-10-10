using System;
using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
    [DisallowMultipleComponent]
    public sealed class BrakkWrestlingMask : MonoBehaviour
    {
        Material[] materials;
        readonly List<Mesh> detailMeshes=new List<Mesh>();
        // Retain the existing Brakk-only presentation hook: bare scalp, armor details and a comms headset.
        public static void Apply(Transform model)
        {
            if(model==null||model.GetComponent<BrakkWrestlingMask>()!=null)return;
            Transform head=null,chest=null;
            foreach(var bone in model.GetComponentsInChildren<Transform>(true))
            {if(bone.name=="Head")head=bone;if(bone.name=="Chest")chest=bone;}
            if(head==null)return;
            float unit=chest!=null ? Vector3.Distance(model.InverseTransformPoint(head.position),model.InverseTransformPoint(chest.position))/.24f : 1f;
            unit=Mathf.Max(unit,.001f);
            var owner=model.gameObject.AddComponent<BrakkWrestlingMask>();
            owner.materials=new[]{
                RuntimeShaderLibrary.Create(new Color(.47f,.31f,.12f),false,.72f,.37f,Color.black),
                RuntimeShaderLibrary.Create(new Color(.65f,.47f,.23f),false,.75f,.42f,Color.black),
                RuntimeShaderLibrary.Create(new Color(.12f,.15f,.18f),false,.65f,.34f,Color.black),
                RuntimeShaderLibrary.Create(new Color(.055f,.063f,.072f),false,.02f,.16f,Color.black)
            };
            owner.RemoveScalpHair(model,head,unit);
            owner.BuildArmorDetails(model,unit);
            owner.BuildHeadset(model,head,unit);
        }
        void BuildHeadset(Transform model,Transform head,float unit)
        {
            if(materials[1]==null||materials[2]==null||materials[3]==null)return;
            // Fit only active skin triangles, excluding beard, old scalp locks and face armor.
            Bounds skull=default;bool found=false;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh source=skin.sharedMesh;
                if(skin.name!="DF_Anatomy"||source==null||!source.isReadable)continue;
                int bone=Array.IndexOf(skin.bones,head);
                if(bone<0||bone>=source.bindposes.Length)continue;
                var points=source.vertices;var weights=source.boneWeights;var slots=skin.sharedMaterials;
                if(points.Length!=weights.Length)continue;
                Matrix4x4 bind=source.bindposes[bone];
                for(int sub=0;sub<source.subMeshCount&&sub<slots.Length;sub++)
                {
                    if(slots[sub]==null||!slots[sub].name.StartsWith("Skin",StringComparison.OrdinalIgnoreCase))continue;
                    foreach(int i in source.GetTriangles(sub))
                    {
                        BoneWeight w=weights[i];
                        float influence=(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)+
                            (w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
                        if(influence<.9f)continue;
                        Vector3 p=model.InverseTransformPoint(head.TransformPoint(bind.MultiplyPoint3x4(points[i])));
                        if(!found){skull=new Bounds(p,Vector3.zero);found=true;}else skull.Encapsulate(p);
                    }
                }
            }
            if(!found||skull.size.y<.01f*unit)
                skull=new Bounds(model.InverseTransformPoint(head.position)+new Vector3(0,.25f*unit,0),new Vector3(.26f,.30f,.29f)*unit);
            var vertices=new List<Vector3>();var faces=new[]{new List<int>(),new List<int>(),new List<int>()};
            void Tri(Vector3 a,Vector3 b,Vector3 c,int surface,bool flip=false)
            {
                int start=vertices.Count;vertices.Add(a);vertices.Add(flip?c:b);vertices.Add(flip?b:c);
                faces[surface].Add(start);faces[surface].Add(start+1);faces[surface].Add(start+2);
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int surface,bool flip=false)
            {Tri(a,b,c,surface,flip);Tri(a,c,d,surface,flip);}
            void Oval(Vector3 center,float ry,float rz,float depth,float side,int surface)
            {
                const int segments=16;
                Vector3 Ring(int i,float x,float radius)
                {float angle=i*Mathf.PI*2/segments;return center+new Vector3(side*x,Mathf.Cos(angle)*ry*radius,Mathf.Sin(angle)*rz*radius);}
                float[] layers={-.5f,-.30f,.30f,.5f},radii={.86f,1f,1f,.86f};
                for(int i=0;i<segments;i++)
                {
                    for(int j=0;j<3;j++)
                        Quad(Ring(i,layers[j]*depth,radii[j]),Ring(i+1,layers[j]*depth,radii[j]),
                            Ring(i+1,layers[j+1]*depth,radii[j+1]),Ring(i,layers[j+1]*depth,radii[j+1]),surface,side<0);
                    Tri(center-Vector3.right*side*depth*.5f,Ring(i+1,-depth*.5f,.86f),Ring(i,-depth*.5f,.86f),surface,side<0);
                    Tri(center+Vector3.right*side*depth*.5f,Ring(i,depth*.5f,.86f),Ring(i+1,depth*.5f,.86f),surface,side<0);
                }
            }
            void Band(Vector3 center,float rx,float ry,float thickness,float depth,int surface)
            {
                const int segments=16;
                Vector3 Point(int i,bool outer,bool front)
                {
                    float a=i*Mathf.PI/segments,edge=outer?thickness*.5f:-thickness*.5f;
                    return center+new Vector3(Mathf.Cos(a)*(rx+edge),Mathf.Sin(a)*(ry+edge),(front?1:-1)*depth*.5f);
                }
                for(int i=0;i<segments;i++)
                {
                    Quad(Point(i,true,false),Point(i+1,true,false),Point(i+1,true,true),Point(i,true,true),surface);
                    Quad(Point(i,false,false),Point(i,false,true),Point(i+1,false,true),Point(i+1,false,false),surface);
                    Quad(Point(i,false,true),Point(i,true,true),Point(i+1,true,true),Point(i+1,false,true),surface);
                    Quad(Point(i,false,false),Point(i+1,false,false),Point(i+1,true,false),Point(i,true,false),surface);
                }
                Quad(Point(0,false,false),Point(0,true,false),Point(0,true,true),Point(0,false,true),surface);
                Quad(Point(segments,false,false),Point(segments,false,true),Point(segments,true,true),Point(segments,true,false),surface);
            }
            void Tube(Vector3 a,Vector3 b,float radius,int surface)
            {
                const int segments=8;Vector3 forward=(b-a).normalized;
                Vector3 right=Vector3.Cross(forward,Mathf.Abs(forward.y)<.9f?Vector3.up:Vector3.right).normalized;
                Vector3 up=Vector3.Cross(forward,right);
                Vector3 Ring(Vector3 center,int i)
                {float angle=i*Mathf.PI*2/segments;return center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius;}
                for(int i=0;i<segments;i++)
                {
                    Quad(Ring(a,i),Ring(a,i+1),Ring(b,i+1),Ring(b,i),surface);
                    Tri(a,Ring(a,i+1),Ring(a,i),surface);Tri(b,Ring(b,i),Ring(b,i+1),surface);
                }
            }
            float width=skull.extents.x,height=skull.size.y;
            float ry=height*.28f,rz=Mathf.Max(skull.size.z*.27f,.05f*unit),cupDepth=.036f*unit;
            Vector3 ear=new(skull.center.x,skull.max.y-height*.47f,skull.center.z-.012f*unit);
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 center=ear+Vector3.right*side*(width+.010f*unit);
                Oval(center,ry*.96f,rz*.96f,.018f*unit,side,2);
                center+=Vector3.right*side*(.010f*unit+cupDepth*.5f);
                Oval(center,ry,rz,cupDepth,side,0);
                Vector3 outer=center+Vector3.right*side*(cupDepth*.5f+.003f*unit);
                Oval(outer,ry*.81f,rz*.81f,.005f*unit,side,1);
                Oval(outer+Vector3.right*side*.004f*unit,ry*.64f,rz*.64f,.004f*unit,side,0);
                Tube(center+new Vector3(0,ry*.45f,rz*.30f),ear+new Vector3(side*(width+.016f*unit),ry*.65f,0),.010f*unit,1);
            }
            float bandX=width+.018f*unit,bandY=skull.max.y+.020f*unit-ear.y;
            Band(ear,bandX,bandY,.017f*unit,.055f*unit,0);
            Band(ear+Vector3.down*.010f*unit,bandX-.005f*unit,bandY-.008f*unit,.010f*unit,.043f*unit,2);
            Band(ear+Vector3.forward*.025f*unit,bandX,bandY,.006f*unit,.008f*unit,1);
            Vector3 start=ear+new Vector3(-width-.042f*unit,-ry*.22f,rz*.50f);
            Vector3 bend=ear+new Vector3(-width-.040f*unit,-ry*.46f,rz+skull.size.z*.20f);
            Vector3 end=new(skull.center.x-.045f*unit,skull.max.y-height*.72f,skull.max.z+.035f*unit);
            Tube(start,bend,.007f*unit,0);Tube(bend,end,.007f*unit,0);
            Tube(end-Vector3.right*.013f*unit,end+Vector3.right*.013f*unit,.014f*unit,2);
            Tube(end-Vector3.right*.006f*unit,end+Vector3.right*.006f*unit,.015f*unit,1);
            for(int i=0;i<vertices.Count;i++)vertices[i]=head.InverseTransformPoint(model.TransformPoint(vertices[i]));
            var mesh=new Mesh{name="DF_Brakk_CommsHeadset"};mesh.SetVertices(vertices);mesh.subMeshCount=3;
            for(int i=0;i<3;i++)mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();detailMeshes.Add(mesh);
            var go=new GameObject(mesh.name);go.layer=head.gameObject.layer;go.transform.SetParent(head,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials=new[]{materials[2],materials[1],materials[3]};
        }
        void RemoveScalpHair(Transform model,Transform head,float unit)
        {
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(skin.name!="DF_Anatomy"||skin.sharedMesh==null||!skin.sharedMesh.isReadable)continue;
                Mesh original=skin.sharedMesh;
                int headIndex=Array.FindIndex(skin.bones,b=>b==head);
                if(headIndex<0)continue;
                var vertices=original.vertices;var weights=original.boneWeights;
                if(weights.Length!=vertices.Length)continue;
                Matrix4x4 toHead=original.bindposes[headIndex];
                float neckY=model.InverseTransformPoint(head.position).y;
                var parents=new int[vertices.Length];
                for(int i=0;i<parents.Length;i++)parents[i]=i;
                int Root(int i){while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
                void Join(int a,int b){parents[Root(a)]=Root(b);}
                var slots=skin.sharedMaterials;
                for(int m=0;m<original.subMeshCount&&m<slots.Length;m++)
                {
                    if(slots[m]==null||slots[m].name.IndexOf("Hair",StringComparison.OrdinalIgnoreCase)<0)continue;
                    int[] indices=original.GetTriangles(m);
                    foreach(int i in indices)parents[i]=i;
                    for(int i=0;i<indices.Length;i+=3){Join(indices[i],indices[i+1]);Join(indices[i],indices[i+2]);}
                    var low=new Dictionary<int,float>();var high=new Dictionary<int,float>();
                    var headOnly=new Dictionary<int,bool>();
                    foreach(int i in indices)
                    {
                        int component=Root(i);
                        Vector3 p=model.InverseTransformPoint(head.TransformPoint(toHead.MultiplyPoint3x4(vertices[i])));
                        BoneWeight w=weights[i];
                        float amount=(w.boneIndex0==headIndex?w.weight0:0)+(w.boneIndex1==headIndex?w.weight1:0)+
                            (w.boneIndex2==headIndex?w.weight2:0)+(w.boneIndex3==headIndex?w.weight3:0);
                        low[component]=low.TryGetValue(component,out float l)?Mathf.Min(l,p.y):p.y;
                        high[component]=high.TryGetValue(component,out float h)?Mathf.Max(h,p.y):p.y;
                        headOnly[component]=(!headOnly.TryGetValue(component,out bool valid)||valid)&&amount>=.5f;
                    }
                    var kept=new List<int>();
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        int component=Root(indices[i]);
                        // Hair locks are tall connected islands. Small brow/beard strands stay intact.
                        bool scalp=headOnly[component]&&high[component]-low[component]>.06f*unit&&high[component]>neckY+.20f*unit;
                        if(scalp)continue;
                        kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);
                    }
                    if(kept.Count==indices.Length)continue;
                    if(skin.sharedMesh==original)
                    {
                        var trimmed=Instantiate(original);trimmed.name="DF_BrakkBodyWithoutScalpHair";
                        skin.sharedMesh=trimmed;detailMeshes.Add(trimmed);
                    }
                    skin.sharedMesh.SetTriangles(kept,m);
                }
            }
        }
        void BuildArmorDetails(Transform model,float unit)
        {
            Transform[] bones=model.GetComponentsInChildren<Transform>(true);
            foreach(string name in new[]{"Chest","UpperArmL","UpperArmR","ForearmL","ForearmR","ThighL","ThighR","ShinL","ShinR"})
            {
                Transform bone=Array.Find(bones,t=>t.name==name);if(bone==null)continue;
                string next=name=="Chest"?"Head":name.Replace("UpperArm","Forearm").Replace("Forearm","Hand");
                if(name.StartsWith("UpperArm"))next="Forearm"+name[name.Length-1];
                if(name.StartsWith("Thigh"))next="Shin"+name[name.Length-1];
                if(name.StartsWith("Shin"))next="Foot"+name[name.Length-1];
                Transform tip=Array.Find(bones,t=>t.name==next);if(tip==null)continue;
                Vector3 a=model.InverseTransformPoint(bone.position),b=model.InverseTransformPoint(tip.position);
                var verts=new List<Vector3>();var body=new List<int>();var accents=new List<int>();
                void Block(Vector3 c,Vector3 size,List<int> faces)
                {
                    int start=verts.Count;
                    Vector2[] outline={new Vector2(-.75f,-1),new Vector2(.75f,-1),new Vector2(1,-.75f),new Vector2(1,.75f),new Vector2(.75f,1),new Vector2(-.75f,1),new Vector2(-1,.75f),new Vector2(-1,-.75f)};
                    for(int layer=0;layer<2;layer++)foreach(var q in outline)
                        verts.Add(bone.InverseTransformPoint(model.TransformPoint(c+new Vector3(q.x*size.x,q.y*size.y,(layer==0?-1:1)*size.z))));
                    for(int j=1;j<7;j++){faces.Add(start);faces.Add(start+j+1);faces.Add(start+j);faces.Add(start+8);faces.Add(start+8+j);faces.Add(start+9+j);}
                    for(int j=0;j<8;j++)Quad(faces,start+j,start+(j+1)%8,start+8+(j+1)%8,start+8+j);
                }
                bool torso=name=="Chest";
                float width=(torso?.15f:name.StartsWith("Thigh")?.080f:name.StartsWith("UpperArm")?.105f:.070f)*unit;
                float depth=(torso?.17f:name.StartsWith("UpperArm")?.105f:.085f)*unit;
                for(int j=0;j<3;j++)
                {
                    Vector3 c=Vector3.Lerp(a,b,.20f+j*.23f)+Vector3.forward*depth;
                    Block(c,new Vector3(width,.032f*unit,.012f*unit),body);
                    Block(c+Vector3.up*.022f*unit+Vector3.forward*.014f*unit,new Vector3(width*.88f,.004f*unit,.004f*unit),accents);
                    foreach(float side in new[]{-1f,1f})
                        Block(c+new Vector3(side*width*.77f,0,.019f*unit),Vector3.one*(.006f*unit),accents);
                }
                var detail=new Mesh{name="DF_Brakk_ArticulatedArmor_"+name};detail.SetVertices(verts);detail.subMeshCount=2;
                detail.SetTriangles(body,0);detail.SetTriangles(accents,1);detail.RecalculateNormals();detail.RecalculateBounds();detailMeshes.Add(detail);
                var go=new GameObject(detail.name);go.transform.SetParent(bone,false);go.layer=bone.gameObject.layer;go.AddComponent<MeshFilter>().sharedMesh=detail;
                go.AddComponent<MeshRenderer>().sharedMaterials=new[]{materials[0],materials[1]};
            }
        }
        static void Quad(List<int> t,int a,int b,int c,int d){t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d);}
        void OnDestroy(){foreach(var detail in detailMeshes)if(detail!=null)Destroy(detail);if(materials!=null)foreach(var m in materials)if(m!=null)Destroy(m);}
    }
}

