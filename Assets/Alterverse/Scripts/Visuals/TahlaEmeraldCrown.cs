using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>A head-fitted emerald crown shared by match and authored menu previews.</summary>
    [DisallowMultipleComponent]
    public sealed class TahlaEmeraldCrown : MonoBehaviour
    {
        Mesh crownMesh;
        Material crownMaterial;

        public static void Apply(Transform model)
        {
            if(model==null||model.GetComponent<TahlaEmeraldCrown>()!=null)return;
            Transform head=null;
            foreach(var bone in model.GetComponentsInChildren<Transform>(true))
                if(bone.name=="Head"){head=bone;break;}
            if(head==null)return;
            Bounds bounds=default;bool found=false;
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
            model.gameObject.AddComponent<TahlaEmeraldCrown>().Build(model,head,bounds);
        }

        void Build(Transform model,Transform head,Bounds headBounds)
        {
            Color emerald=new(.07f,1f,.23f,1f);
            crownMaterial=RuntimeShaderLibrary.Create(emerald,false,.35f,.5f,emerald*3f);
            if(crownMaterial==null)return;
            crownMaterial.name="DF_TahlaEmeraldCrown";crownMaterial.EnableKeyword("_EMISSION");
            float unit=headBounds.size.y;
            float rx=headBounds.extents.x*1.08f,rz=headBounds.extents.z*1.08f;
            float thickness=unit*.027f,baseY=headBounds.max.y-unit*.025f;
            const int segments=64;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<segments;i++)
            {
                int next=(i+1)%segments;
                Vector3 ob=Point(i,false,false),ot=Point(i,false,true),ib=Point(i,true,false),it=Point(i,true,true);
                Vector3 nb=Point(next,false,false),nt=Point(next,false,true),jb=Point(next,true,false),jt=Point(next,true,true);
                Quad(ob,ot,nt,nb);Quad(ib,jb,jt,it);Quad(ot,it,jt,nt);Quad(ob,nb,jb,ib);
            }
            crownMesh=new Mesh{name="DF_TahlaEightPointCrown"};
            crownMesh.SetVertices(vertices);crownMesh.SetTriangles(triangles,0);crownMesh.RecalculateNormals();crownMesh.RecalculateBounds();
            if(HeadAdornmentSkin.Merge(model,head,crownMesh,crownMaterial))return;
            var crown=new GameObject("DF_TahlaEmeraldCrown");crown.layer=model.gameObject.layer;
            crown.transform.SetParent(head,false);
            crown.AddComponent<MeshFilter>().sharedMesh=crownMesh;
            crown.AddComponent<MeshRenderer>().sharedMaterial=crownMaterial;

            Vector3 Point(int i,bool inner,bool top)
            {
                float a=i*Mathf.PI*2/segments;
                float peak=Mathf.Max(0,1f-Mathf.Abs(i%8-4)/2f);
                float y=baseY+(top?unit*(.10f+peak*.24f):0);
                Vector3 p=new(headBounds.center.x+Mathf.Sin(a)*(rx-(inner?thickness:0)),y,
                    headBounds.center.z+Mathf.Cos(a)*(rz-(inner?thickness:0)));
                return head.InverseTransformPoint(model.TransformPoint(p));
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                triangles.Add(n);triangles.Add(n+2);triangles.Add(n+1);triangles.Add(n);triangles.Add(n+3);triangles.Add(n+2);
            }
        }

        void OnDestroy(){if(crownMesh!=null)Destroy(crownMesh);if(crownMaterial!=null)Destroy(crownMaterial);}
    }
}

