using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>A violet helmet strip fitted to the assassin head, including menu previews.</summary>
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
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(skin.sharedMesh==null||skin.name!="DF_Anatomy")continue;
                int index=System.Array.IndexOf(skin.bones,head);
                if(index<0)continue;
                var baked=new Mesh();skin.BakeMesh(baked);
                var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;
                for(int i=0;i<vertices.Length&&i<weights.Length;i++)
                {
                    var w=weights[i];
                    float influence=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+
                        (w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
                    if(influence<.5f)continue;
                    Vector3 p=model.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
                    if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                }
                Object.Destroy(baked);
            }
            if(!found||bounds.size.y<=0)return;
            model.gameObject.AddComponent<AssassinVioletHeadStripe>().Build(model,head,bounds);
        }

        void Build(Transform model,Transform head,Bounds bounds)
        {
            Color violet=new(.63f,.055f,1f,1f);
            crownMaterial=RuntimeShaderLibrary.Create(violet,false,.15f,.5f,violet*2.1f);
            if(crownMaterial==null)return;
            crownMaterial.name="DF_AssassinVioletHeadStripe";crownMaterial.EnableKeyword("_EMISSION");
            var vertices=new List<Vector3>();var triangles=new List<int>();
            // A narrow raised strip follows the helmet's crown from brow to rear.
            const int segments=24;
            for(int i=0;i<=segments;i++)
            {
                float angle=Mathf.Lerp(-1.15f,1.15f,i/(float)segments);
                float y=bounds.center.y+(bounds.extents.y+.007f)*Mathf.Cos(angle);
                float z=bounds.center.z+(bounds.extents.z+.007f)*Mathf.Sin(angle);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 p=new(bounds.center.x+side*bounds.size.x*.045f,y,z);
                    vertices.Add(head.InverseTransformPoint(model.TransformPoint(p)));
                }
                if(i==segments)continue;
                int n=i*2;
                triangles.Add(n);triangles.Add(n+2);triangles.Add(n+1);
                triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n+3);
            }
            crownMesh=new Mesh{name="DF_AssassinHeadStripe"};
            crownMesh.SetVertices(vertices);crownMesh.SetTriangles(triangles,0);crownMesh.RecalculateNormals();crownMesh.RecalculateBounds();
            var stripe=new GameObject("DF_AssassinVioletHeadStripe");stripe.layer=model.gameObject.layer;
            stripe.transform.SetParent(head,false);
            stripe.AddComponent<MeshFilter>().sharedMesh=crownMesh;
            stripe.AddComponent<MeshRenderer>().sharedMaterial=crownMaterial;
        }

        void OnDestroy(){if(crownMesh!=null)Destroy(crownMesh);if(crownMaterial!=null)Destroy(crownMaterial);}
    }
}
