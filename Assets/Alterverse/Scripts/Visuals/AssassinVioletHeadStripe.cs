using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Violet roof lighting: central helmet strip and two raised top rails, including menu previews.</summary>
    [DisallowMultipleComponent]
    public sealed class AssassinVioletHeadStripe : MonoBehaviour
    {
        Mesh crownMesh;
        Material crownMaterial;

        public static void Apply(Transform model)
        {
            if(model==null||model.GetComponent<AssassinVioletHeadStripe>()!=null)return;
            Transform head=null;
            foreach(var bone in model.GetComponentsInChildren<Transform>(true))
                if(bone.name=="Head"){head=bone;break;}
            if(head==null)return;
            Bounds bounds=default;bool found=false;
            var headPoints=new List<Vector3>();
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(skin.sharedMesh==null||!skin.sharedMesh.isReadable||skin.name!="DF_Anatomy")continue;
                int index=System.Array.IndexOf(skin.bones,head);
                if(index<0)continue;
                var vertices=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;
                Matrix4x4 headBind=skin.sharedMesh.bindposes[index];
                for(int i=0;i<vertices.Length&&i<weights.Length;i++)
                {
                    var w=weights[i];
                    float influence=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+
                        (w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                    if(influence<.5f)continue;
                    Vector3 p=model.InverseTransformPoint(head.TransformPoint(headBind.MultiplyPoint3x4(vertices[i])));
                    headPoints.Add(p);
                    if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                }
            }
            if(!found||bounds.size.y<=0)
            {
                Transform chest=null;
                foreach(var bone in model.GetComponentsInChildren<Transform>(true))if(bone.name=="Chest"){chest=bone;break;}
                float unit=chest!=null?Vector3.Distance(model.InverseTransformPoint(head.position),model.InverseTransformPoint(chest.position))/.24f:1f;
                bounds=new Bounds(model.InverseTransformPoint(head.position)+Vector3.up*.16f*unit,new Vector3(.24f,.32f,.24f)*unit);
            }
            model.gameObject.AddComponent<AssassinVioletHeadStripe>().Build(model,head,bounds,headPoints);
        }

        void Build(Transform model,Transform head,Bounds bounds,List<Vector3> headPoints)
        {
            Color violet=new(.66f,.08f,1f,1f);
            crownMaterial=RuntimeShaderLibrary.Create(violet,false,.15f,.5f,violet*3f);
            if(crownMaterial==null)return;
            crownMaterial.name="DF_AssassinVioletHelmetRoofLights";crownMaterial.EnableKeyword("_EMISSION");
            var vertices=new List<Vector3>();var triangles=new List<int>();
            // Follow the highest actual helmet points, so boxy head geometry cannot bury the strip.
            const int segments=24;
            float clearance=Mathf.Max(.008f,bounds.size.y*.03f);
            float halfWidth=Mathf.Max(.009f,bounds.size.x*.065f);
            var levels=new float[segments+1];
            var zs=new float[segments+1];
            for(int i=0;i<=segments;i++)
            {
                float z=Mathf.Lerp(bounds.center.z-bounds.extents.z*.72f,bounds.center.z+bounds.extents.z*.72f,i/(float)segments);
                float top=float.MinValue;
                foreach(Vector3 p in headPoints)
                    if(Mathf.Abs(p.z-z)<bounds.extents.z*.25f&&Mathf.Abs(p.x-bounds.center.x)<bounds.extents.x*.6f)
                        top=Mathf.Max(top,p.y);
                if(top==float.MinValue)top=bounds.max.y;
                levels[i]=top+clearance;zs[i]=z;
            }
            for(int i=0;i<segments;i++)
            {
                Vector3 a=Point(i,-1,0),b=Point(i,1,0),c=Point(i+1,1,0),d=Point(i+1,-1,0);
                Vector3 ab=Point(i,-1,-clearance*.65f),bb=Point(i,1,-clearance*.65f);
                Vector3 cb=Point(i+1,1,-clearance*.65f),db=Point(i+1,-1,-clearance*.65f);
                Quad(a,d,c,b);Quad(ab,bb,cb,db);Quad(a,ab,db,d);Quad(b,c,cb,bb);
                if(i==0)Quad(a,b,bb,ab);
                if(i==segments-1)Quad(d,db,cb,c);
            }
            Vector3 Point(int i,int side,float depth)
                =>head.InverseTransformPoint(model.TransformPoint(new Vector3(bounds.center.x+side*halfWidth,levels[i]+depth,zs[i])));
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            // Two compact lights sit above the helmet roof, alongside the central strip.
            foreach(float side in new[]{-1f,1f})
            {
                float x=bounds.center.x+side*bounds.extents.x*.48f;
                float y=bounds.max.y+clearance*1.25f;
                float z=bounds.center.z+bounds.extents.z*.05f;
                float w=bounds.size.x*.047f;
                float length=bounds.size.z*.24f;
                float thickness=clearance*.55f;
                Vector3 P(float dx,float dy,float dz)
                    =>head.InverseTransformPoint(model.TransformPoint(new Vector3(x+dx,y+dy,z+dz)));
                Vector3 a=P(-w,0,-length),b=P(w,0,-length),c=P(w,0,length),d=P(-w,0,length);
                Vector3 e=P(-w,-thickness,-length),f=P(w,-thickness,-length),g=P(w,-thickness,length),h=P(-w,-thickness,length);
                Quad(a,d,c,b);Quad(e,f,g,h);
                Quad(a,b,f,e);Quad(d,h,g,c);Quad(a,e,h,d);Quad(b,c,g,f);
            }
            crownMesh=new Mesh{name="DF_AssassinHeadStripe"};
            crownMesh.SetVertices(vertices);crownMesh.SetTriangles(triangles,0);crownMesh.RecalculateNormals();crownMesh.RecalculateBounds();
            if(HeadAdornmentSkin.Merge(model,head,crownMesh,crownMaterial))return;
            var stripe=new GameObject("DF_AssassinVioletHeadStripe");stripe.layer=model.gameObject.layer;
            stripe.transform.SetParent(head,false);
            stripe.AddComponent<MeshFilter>().sharedMesh=crownMesh;
            stripe.AddComponent<MeshRenderer>().sharedMaterial=crownMaterial;
        }

        void OnDestroy(){if(crownMesh!=null)Destroy(crownMesh);if(crownMaterial!=null)Destroy(crownMaterial);}
    }
}

