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
        // Retain the component name for existing presentation hooks; Brakk has no helmet.
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
                RuntimeShaderLibrary.Create(new Color(.65f,.47f,.23f),false,.75f,.42f,Color.black)
            };
            owner.RemoveScalpHair(model,head,unit);
            owner.BuildArmorDetails(model,unit);
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

