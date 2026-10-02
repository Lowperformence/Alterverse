using System;
using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
    [DisallowMultipleComponent]
    public sealed class BrakkWrestlingMask : MonoBehaviour
    {
        Mesh mesh; Material[] materials;
        readonly List<Mesh> detailMeshes=new List<Mesh>();
        public static void Apply(Transform model)
        {
            if(model.GetComponent<BrakkWrestlingMask>()!=null)return;
            Transform head=null,chest=null;
            foreach(var bone in model.GetComponentsInChildren<Transform>(true))
            {if(bone.name=="Head")head=bone;if(bone.name=="Chest")chest=bone;}
            if(head==null){Debug.LogWarning("DF: Brakk helmet requires Head bone.",model);return;}
            float unit=chest!=null ? Vector3.Distance(model.InverseTransformPoint(head.position),model.InverseTransformPoint(chest.position))/.24f : 1f;
            unit=Mathf.Max(unit,.001f);
            Vector3 neck=model.InverseTransformPoint(head.position);
            Vector3 center=neck+Vector3.up*(.16f*unit),radius=new Vector3(.12f,.155f,.125f)*unit;
            SkinnedMeshRenderer skin=null;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.name=="DF_Anatomy"){skin=r;break;}
            // Old imports can have Read/Write disabled. A visible bone-fitted helmet must still be built.
            if(skin!=null && skin.sharedMesh!=null && skin.sharedMesh.isReadable)
            {
                int index=Array.FindIndex(skin.bones,b=>b==head);
                if(index>=0)
                {
                    // Read head points in bone space; this is independent of BakeMesh timing and renderer scale.
                    Vector3[] points=skin.sharedMesh.vertices;BoneWeight[] weights=skin.sharedMesh.boneWeights;
                    Matrix4x4 headBind=skin.sharedMesh.bindposes[index];
                    Vector3 HeadPoint(int vertex)=>model.InverseTransformPoint(head.TransformPoint(headBind.MultiplyPoint3x4(points[vertex])));
                    Vector3 low=Vector3.one*float.MaxValue,high=Vector3.one*float.MinValue;
                    if(points.Length==weights.Length)for(int i=0;i<points.Length;i++)
                    {
                        var w=weights[i];float weight=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                        if(weight<.5f)continue;
                        Vector3 q=HeadPoint(i);low=Vector3.Min(low,q);high=Vector3.Max(high,q);
                    }
                    if(low.x<=high.x)
                    {
                        low.y=Mathf.Max(low.y,neck.y+(high.y-neck.y)*.19f);
                        center=(low+high)*.5f;
                        radius=Vector3.Max(Vector3.Scale(high-low,new Vector3(.60f,.57f,.60f)),new Vector3(.10f,.13f,.10f)*unit);
                        // An ellipsoid fitted only to box extents cuts through the box corners.
                        // Fit the enclosing surface to the actual head, including hair and jaw armor.
                        float enclosure=1f;
                        for(int i=0;i<points.Length;i++)
                        {
                            var w=weights[i];
                            float weight=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                            if(weight<.5f)continue;
                            Vector3 q=HeadPoint(i);
                            if(q.y<low.y)continue;
                            Vector3 d=q-center;
                            float normalizedRadius=new Vector3(d.x/radius.x,d.y/radius.y,d.z/radius.z).magnitude;
                            enclosure=Mathf.Max(enclosure,normalizedRadius*1.06f);
                        }
                        radius*=enclosure;
                    }
                }
            }
            var owner=model.gameObject.AddComponent<BrakkWrestlingMask>();
            owner.Build(model,head,center,radius);
            owner.MergeHelmetIntoBody(model,skin,head,center.y-radius.y*.84f);
            owner.BuildArmorDetails(model,unit);
        }
        void Build(Transform model,Transform head,Vector3 center,Vector3 radius)
        {
            var v=new List<Vector3>();var bronze=new List<int>();var trim=new List<int>();
            Vector3 P(float x,float y,float z)=>head.InverseTransformPoint(model.TransformPoint(center+Vector3.Scale(radius,new Vector3(x,y,z))));
            const int rows=40,cols=72;
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {float a=y*Mathf.PI/rows,b=x*Mathf.PI*2/cols;v.Add(P(Mathf.Sin(a)*Mathf.Sin(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Cos(b)));}
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
            {
                float a=(y+.5f)*Mathf.PI/rows,b=(x+.5f)*Mathf.PI*2/cols;
                float px=Mathf.Sin(a)*Mathf.Sin(b),py=Mathf.Cos(a),pz=Mathf.Sin(a)*Mathf.Cos(b);
                // Open eye slit and narrow breathing opening; enclosed bronze cheek guards.
                if(pz>.48f && ((py>-.11f&&py<.16f&&Mathf.Abs(px)>.13f) || (py<-.26f&&Mathf.Abs(px)<.14f)))continue;
                int i=y*(cols+1)+x;Quad(bronze,i,i+1,i+cols+2,i+cols+1);
            }
            void Plate(Vector3[] points,List<int> faces,float thickness)
            {
                int start=v.Count;foreach(var q in points)v.Add(P(q.x,q.y,q.z));foreach(var q in points)v.Add(P(q.x,q.y,q.z-thickness));
                for(int j=1;j<points.Length-1;j++){faces.Add(start);faces.Add(start+j);faces.Add(start+j+1);faces.Add(start+points.Length);faces.Add(start+points.Length+j+1);faces.Add(start+points.Length+j);}
                for(int j=0;j<points.Length;j++){int k=(j+1)%points.Length;Quad(faces,start+j,start+k,start+points.Length+k,start+points.Length+j);}
            }
            // Projecting brow, nose guard and swept cheek plates evoke the bronze helmet reference.
            foreach(float sign in new[]{-1f,1f})
            {
                Plate(new[]{new Vector3(sign*.10f,.20f,1.03f),new Vector3(sign*.82f,.29f,.66f),new Vector3(sign*.80f,.15f,.81f),new Vector3(sign*.12f,.09f,1.09f)},trim,.055f);
                Plate(new[]{new Vector3(sign*.19f,-.16f,1.00f),new Vector3(sign*.73f,-.11f,.78f),new Vector3(sign*.79f,-.91f,.62f),new Vector3(sign*.36f,-1.15f,.76f),new Vector3(sign*.18f,-.62f,1.01f)},bronze,.08f);
            }
            Plate(new[]{new Vector3(-.13f,.24f,1.04f),new Vector3(.13f,.24f,1.04f),new Vector3(.085f,-.45f,1.16f),new Vector3(0,-.57f,1.19f),new Vector3(-.085f,-.45f,1.16f)},bronze,.08f);
            mesh=new Mesh{name="DF_BrakkBronzeHelmet"};mesh.SetVertices(v);mesh.subMeshCount=2;
            Vector3 localCenter=P(0,0,0);
            foreach(var ids in new[]{bronze,trim})for(int i=0;i<ids.Count;i+=3)
            {Vector3 a=v[ids[i]],b=v[ids[i+1]],c=v[ids[i+2]];if(Vector3.Dot(Vector3.Cross(b-a,c-a),(a+b+c)/3-localCenter)<0){int t=ids[i+1];ids[i+1]=ids[i+2];ids[i+2]=t;}}
            mesh.SetTriangles(bronze,0);mesh.SetTriangles(trim,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            materials=new[]{RuntimeShaderLibrary.Create(new Color(.47f,.31f,.12f),false,.72f,.37f,Color.black),RuntimeShaderLibrary.Create(new Color(.65f,.47f,.23f),false,.75f,.42f,Color.black)};
            var go=new GameObject("DF_BrakkBronzeHelmet");go.transform.SetParent(head,false);go.layer=head.gameObject.layer;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
        }
        void MergeHelmetIntoBody(Transform model,SkinnedMeshRenderer skin,Transform head,float neckCut)
        {
            if(skin==null||skin.sharedMesh==null||!skin.sharedMesh.isReadable)return;
            Mesh original=skin.sharedMesh;
            int headIndex=Array.FindIndex(skin.bones,b=>b==head);
            if(headIndex<0||headIndex>=original.bindposes.Length)return;
            var vertices=new List<Vector3>(original.vertices);
            var normals=new List<Vector3>(original.normals);
            if(normals.Count!=vertices.Count){normals.Clear();for(int i=0;i<vertices.Count;i++)normals.Add(Vector3.up);}
            var weights=new List<BoneWeight>(original.boneWeights);
            var colors=new List<Color>(original.colors);
            if(colors.Count!=vertices.Count){colors.Clear();for(int i=0;i<vertices.Count;i++)colors.Add(Color.white);}
            var uv=new List<Vector2>();original.GetUVs(0,uv);
            if(uv.Count!=vertices.Count){uv.Clear();for(int i=0;i<vertices.Count;i++)uv.Add(Vector2.zero);}
            var finish=new List<Vector2>();original.GetUVs(1,finish);
            if(finish.Count!=vertices.Count){finish.Clear();for(int i=0;i<vertices.Count;i++)finish.Add(Vector2.zero);}
            int offset=vertices.Count;
            Matrix4x4 headToMesh=original.bindposes[headIndex].inverse;
            Matrix4x4 normalMatrix=headToMesh.inverse.transpose;
            Vector3[] helmetVertices=mesh.vertices,helmetNormals=mesh.normals;
            for(int i=0;i<helmetVertices.Length;i++)
            {
                vertices.Add(headToMesh.MultiplyPoint3x4(helmetVertices[i]));
                normals.Add(normalMatrix.MultiplyVector(helmetNormals[i]).normalized);
                weights.Add(new BoneWeight{boneIndex0=headIndex,weight0=1});
                colors.Add(Color.white);uv.Add(Vector2.zero);finish.Add(new Vector2(.72f,.4f));
            }
            bool OldHead(int i)
            {
                BoneWeight w=weights[i];
                float amount=(w.boneIndex0==headIndex?w.weight0:0)+(w.boneIndex1==headIndex?w.weight1:0)+(w.boneIndex2==headIndex?w.weight2:0)+(w.boneIndex3==headIndex?w.weight3:0);
                Vector3 point=model.InverseTransformPoint(head.TransformPoint(original.bindposes[headIndex].MultiplyPoint3x4(vertices[i])));
                return amount>=.5f&&point.y>neckCut;
            }
            var merged=new Mesh{name="DF_BrakkBodyWithBronzeHelmet"};
            merged.indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
            merged.SetVertices(vertices);merged.SetNormals(normals);merged.SetColors(colors);
            merged.SetUVs(0,uv);merged.SetUVs(1,finish);merged.boneWeights=weights.ToArray();merged.bindposes=original.bindposes;
            merged.subMeshCount=original.subMeshCount+2;
            for(int m=0;m<original.subMeshCount;m++)
            {
                int[] source=original.GetTriangles(m);var kept=new List<int>();
                for(int i=0;i<source.Length;i+=3)
                {
                    if(OldHead(source[i])&&OldHead(source[i+1])&&OldHead(source[i+2]))continue;
                    kept.Add(source[i]);kept.Add(source[i+1]);kept.Add(source[i+2]);
                }
                merged.SetTriangles(kept,m);
            }
            for(int m=0;m<2;m++)
            {
                int[] ids=mesh.GetTriangles(m);for(int i=0;i<ids.Length;i++)ids[i]+=offset;
                merged.SetTriangles(ids,original.subMeshCount+m);
            }
            var combined=new List<Material>(skin.sharedMaterials);
            // Keep one material per original submesh, then append helmet bronze and trim.
            while(combined.Count<original.subMeshCount)combined.Add(materials[0]);
            if(combined.Count>original.subMeshCount)combined.RemoveRange(original.subMeshCount,combined.Count-original.subMeshCount);
            combined.Add(materials[0]);combined.Add(materials[1]);
            merged.RecalculateBounds();skin.sharedMesh=merged;skin.sharedMaterials=combined.ToArray();
            Bounds bounds=skin.localBounds;bounds.Encapsulate(merged.bounds);skin.localBounds=bounds;
            detailMeshes.Add(merged);
            Transform separate=head.Find("DF_BrakkBronzeHelmet");
            if(separate!=null){separate.gameObject.SetActive(false);Destroy(separate.gameObject);}
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
        void OnDestroy(){foreach(var detail in detailMeshes)if(detail!=null)Destroy(detail);if(mesh!=null)Destroy(mesh);if(materials!=null)foreach(var m in materials)if(m!=null)Destroy(m);}
    }
}

