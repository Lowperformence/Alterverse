using System;
using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.Visuals
{
    [DisallowMultipleComponent]
    public sealed class BrakkWrestlingMask : MonoBehaviour
    {
        Mesh mesh; Material[] materials;
        public static void Apply(Transform model)
        {
            if(model.GetComponent<BrakkWrestlingMask>()!=null)return;
            SkinnedMeshRenderer skin=null;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.name=="DF_Anatomy"){skin=r;break;}
            if(skin==null||!skin.sharedMesh.isReadable)return;
            int index=Array.FindIndex(skin.bones,b=>b!=null&&b.name=="Head");if(index<0)return;
            var baked=new Mesh();skin.BakeMesh(baked);Vector3[] points=baked.vertices;BoneWeight[] weights=skin.sharedMesh.boneWeights;
            if(points.Length!=weights.Length){Destroy(baked);return;}
            Vector3 low=Vector3.one*float.MaxValue,high=Vector3.one*float.MinValue;
            for(int i=0;i<points.Length;i++)
            {
                var w=weights[i];float weight=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                if(weight<.8f)continue;
                Vector3 p=model.InverseTransformPoint(skin.transform.TransformPoint(points[i]));low=Vector3.Min(low,p);high=Vector3.Max(high,p);
            }
            Destroy(baked);if(low.x>high.x)return;
            // Exclude the neck. Fit the mask to the complete existing head, including its jaw armor.
            Vector3 neck=model.InverseTransformPoint(skin.bones[index].position);
            low.y=Mathf.Max(low.y,neck.y+(high.y-neck.y)*.19f);
            Vector3 center=(low+high)*.5f,radius=Vector3.Scale(high-low,new Vector3(.54f,.52f,.54f));
            var owner=model.gameObject.AddComponent<BrakkWrestlingMask>();owner.Build(model,skin.bones[index],center,radius);
        }
        void Build(Transform model,Transform head,Vector3 center,Vector3 radius)
        {
            var v=new List<Vector3>();var bronze=new List<int>();var trim=new List<int>();var crest=new List<int>();
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
            // Continuous sagittal red horsehair crest; individual tapered blades create the fringe.
            for(int j=0;j<48;j++)
            {
                float angle=Mathf.Lerp(-1.18f,1.48f,j/47f);float z=Mathf.Sin(angle),y=Mathf.Cos(angle);
                float length=.45f+.15f*Mathf.Sin(j*.83f)*Mathf.Sin(j*.83f);
                float width=.045f;
                int start=v.Count;
                v.Add(P(-width,y*.98f,z*.98f));v.Add(P(width,y*.98f,z*.98f));
                v.Add(P(width*.35f,y*(1.0f+length)+.25f,z*(1.0f+length)));v.Add(P(-width*.35f,y*(1.0f+length)+.25f,z*(1.0f+length)));
                Quad(crest,start,start+1,start+2,start+3);Quad(crest,start+3,start+2,start+1,start);
            }
            mesh=new Mesh{name="DF_BrakkBronzeCrestHelmet"};mesh.SetVertices(v);mesh.subMeshCount=3;
            Vector3 localCenter=P(0,0,0);
            foreach(var ids in new[]{bronze,trim})for(int i=0;i<ids.Count;i+=3)
            {Vector3 a=v[ids[i]],b=v[ids[i+1]],c=v[ids[i+2]];if(Vector3.Dot(Vector3.Cross(b-a,c-a),(a+b+c)/3-localCenter)<0){int t=ids[i+1];ids[i+1]=ids[i+2];ids[i+2]=t;}}
            mesh.SetTriangles(bronze,0);mesh.SetTriangles(trim,1);mesh.SetTriangles(crest,2);mesh.RecalculateNormals();mesh.RecalculateBounds();
            materials=new[]{RuntimeShaderLibrary.Create(new Color(.47f,.31f,.12f),false,.72f,.37f,Color.black),RuntimeShaderLibrary.Create(new Color(.65f,.47f,.23f),false,.75f,.42f,Color.black),RuntimeShaderLibrary.Create(new Color(.56f,.015f,.025f),false,.02f,.20f,Color.black)};
            var go=new GameObject("DF_BrakkBronzeCrestHelmet");go.transform.SetParent(head,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
        }
        static void Quad(List<int> t,int a,int b,int c,int d){t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d);}
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(materials!=null)foreach(var m in materials)if(m!=null)Destroy(m);}
    }
}

