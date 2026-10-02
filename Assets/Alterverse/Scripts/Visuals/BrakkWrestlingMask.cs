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
            var vertices=new List<Vector3>();var red=new List<int>();var silver=new List<int>();const int rows=48,cols=96;
            Vector3 Position(float x,float y,float z,float lift=1f)=>head.InverseTransformPoint(model.TransformPoint(center+Vector3.Scale(radius,new Vector3(x,y,z))*lift));
            for(int row=0;row<=rows;row++)for(int col=0;col<=cols;col++)
            {
                float lat=Mathf.PI*row/rows,lon=2*Mathf.PI*col/cols;
                vertices.Add(Position(Mathf.Sin(lat)*Mathf.Sin(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Cos(lon)));
            }
            for(int row=0;row<rows;row++)for(int col=0;col<cols;col++)
            {
                float lat=Mathf.PI*(row+.5f)/rows,lon=2*Mathf.PI*(col+.5f)/cols;
                float x=Mathf.Sin(lat)*Mathf.Sin(lon),y=Mathf.Cos(lat),z=Mathf.Sin(lat)*Mathf.Cos(lon);
                bool eye=Mathf.Pow((Mathf.Abs(x)-.38f)/.23f,2)+Mathf.Pow((y-.10f)/.17f,2)<1;
                bool mouth=Mathf.Pow(x/.25f,2)+Mathf.Pow((y+.51f)/.15f,2)<1;
                if(z>.35f&&(eye||mouth))continue;
                int a=row*(cols+1)+col;Quad(red,a,a+1,a+cols+2,a+cols+1);
            }
            void Band(float cx,float cy,float rx,float ry)
            {
                int start=vertices.Count;const int segments=64;
                for(int i=0;i<=segments;i++)for(int edge=0;edge<2;edge++)
                {
                    float angle=i*Mathf.PI*2/segments;float factor=edge==0?1f:1.25f;
                    float x=cx+Mathf.Cos(angle)*rx*factor,y=cy+Mathf.Sin(angle)*ry*factor;
                    vertices.Add(Position(x,y,Mathf.Sqrt(Mathf.Max(.005f,1-x*x-y*y)),1.009f));
                }
                for(int i=0;i<segments;i++){int a=start+i*2;Quad(silver,a,a+2,a+3,a+1);}
            }
            Band(-.38f,.10f,.23f,.17f);Band(.38f,.10f,.23f,.17f);Band(0,-.51f,.25f,.15f);
            void Stripe(Vector2[] outline)
            {
                int start=vertices.Count;
                foreach(var p in outline)vertices.Add(Position(p.x,p.y,Mathf.Sqrt(Mathf.Max(.005f,1-p.sqrMagnitude)),1.012f));
                for(int i=1;i<outline.Length-1;i++){silver.Add(start);silver.Add(start+i+1);silver.Add(start+i);}
            }
            foreach(float s in new[]{-1f,1f})
            {
                Stripe(new[]{new Vector2(s*.08f,.80f),new Vector2(s*.24f,.65f),new Vector2(s*.19f,.36f),new Vector2(s*.07f,.21f),new Vector2(s*.12f,.48f)});
                Stripe(new[]{new Vector2(s*.64f,-.12f),new Vector2(s*.78f,-.08f),new Vector2(s*.52f,-.69f),new Vector2(s*.44f,-.77f),new Vector2(s*.47f,-.36f)});
            }
            mesh=new Mesh {name="DF_BrakkRedSilverWrestlingMask"};mesh.SetVertices(vertices);mesh.subMeshCount=2;
            // Make front/back winding independent of mirrored decorative outlines.
            void Outward(List<int> ids)
            {
                Vector3 c=head.InverseTransformPoint(model.TransformPoint(center));
                for(int i=0;i<ids.Count;i+=3){Vector3 a=vertices[ids[i]],b=vertices[ids[i+1]],d=vertices[ids[i+2]];if(Vector3.Dot(Vector3.Cross(b-a,d-a),(a+b+d)/3-c)<0){int t=ids[i+1];ids[i+1]=ids[i+2];ids[i+2]=t;}}
            }
            Outward(red);Outward(silver);mesh.SetTriangles(red,0);mesh.SetTriangles(silver,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            materials=new[]{RuntimeShaderLibrary.Create(new Color(.55f,.018f,.028f),false,.05f,.24f,Color.black),RuntimeShaderLibrary.Create(new Color(.79f,.81f,.80f),false,.55f,.40f,Color.black)};
            var go=new GameObject("DF_BrakkWrestlingMask");go.transform.SetParent(head,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
        }
        static void Quad(List<int> t,int a,int b,int c,int d){t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d);}
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(materials!=null)foreach(var m in materials)if(m!=null)Destroy(m);}
    }
}
