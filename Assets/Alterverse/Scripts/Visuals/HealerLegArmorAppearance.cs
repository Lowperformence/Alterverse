using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Preserves Tahla's anatomy and replaces rigid hanging vestments with articulated light armor.</summary>
    [DisallowMultipleComponent]
    public sealed class HealerLegArmorAppearance : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        public static void Apply(Transform model)
        {
            if (model == null || model.GetComponent<HealerLegArmorAppearance>() != null) return;
            SkinnedMeshRenderer anatomy = null;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == "DF_Anatomy") { anatomy = r; break; }
            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            Transform hips = Array.Find(bones, b => b.name == "Hips");
            if (anatomy == null || anatomy.sharedMesh == null || hips == null) return;
            foreach (string side in new[] { "L", "R" })
                foreach (string part in new[] { "Thigh", "Shin", "Foot" })
                    if (Array.Find(bones, b => b.name == part + side) == null) return;
            var owner = model.gameObject.AddComponent<HealerLegArmorAppearance>();
            owner.RemoveVestments(anatomy, hips, model);
            Material ivory = RuntimeShaderLibrary.Create(new Color(.67f,.73f,.69f),false,.38f,.40f,Color.black);
            Material gold = RuntimeShaderLibrary.Create(new Color(.56f,.39f,.16f),false,.70f,.35f,Color.black);
            owner.materials.Add(ivory); owner.materials.Add(gold);
            foreach (string side in new[] { "L", "R" })
            {
                Transform thigh = Array.Find(bones,b=>b.name=="Thigh"+side);
                Transform shin = Array.Find(bones,b=>b.name=="Shin"+side);
                Transform foot = Array.Find(bones,b=>b.name=="Foot"+side);
                owner.Plate("DF_HealerThighPlate"+side,thigh,shin.position,model.forward,.10f,.80f,.19f,ivory);
                owner.Plate("DF_HealerKneeGuard"+side,shin,foot.position,model.forward,-.07f,.17f,.17f,gold);
                owner.Plate("DF_HealerShinPlate"+side,shin,foot.position,model.forward,.25f,.90f,.15f,ivory);
            }
        }
        static int Find(int[] parent,int i)
        { while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i; }
        void RemoveVestments(SkinnedMeshRenderer renderer,Transform hips,Transform model)
        {
            Mesh source=renderer.sharedMesh;
            if (!source.isReadable) { Debug.LogError("DF: Healer anatomy must be readable for vestment replacement.",this); return; }
            int n=source.vertexCount; var parent=new int[n];for(int i=0;i<n;i++)parent[i]=i;
            var triangles=new int[source.subMeshCount][];
            for(int s=0;s<triangles.Length;s++)
            {
                triangles[s]=source.GetTriangles(s);
                for(int i=0;i<triangles[s].Length;i+=3)
                {
                    int a=Find(parent,triangles[s][i]);
                    parent[Find(parent,triangles[s][i+1])]=a;parent[Find(parent,triangles[s][i+2])]=a;
                }
            }
            var baked=new Mesh();renderer.BakeMesh(baked);
            Vector3[] points=baked.vertices;BoneWeight[] weights=source.boneWeights;
            var rigid=new bool[n];var below=new bool[n];for(int i=0;i<n;i++)rigid[i]=true;
            Transform head=Array.Find(renderer.bones,b=>b!=null&&b.name=="Head");
            if(head==null || weights.Length!=n || points.Length!=n){Destroy(baked);return;}
            float unit=Vector3.Distance(hips.position,head.position);
            for(int i=0;i<n;i++)
            {
                int group=Find(parent,i);BoneWeight w=weights[i];
                string name=renderer.bones[w.boneIndex0].name;
                if(w.weight0<.999f || (name!="Hips"&&name!="Chest"&&name!="Spine"))rigid[group]=false;
                Vector3 world=renderer.transform.TransformPoint(points[i]);
                if(Vector3.Dot(world-hips.position,model.up)<-unit*.15f)below[group]=true;
            }
            Destroy(baked);
            Mesh copy=Instantiate(source);copy.name="DF_HealerWithoutLongVestments";meshes.Add(copy);
            for(int s=0;s<triangles.Length;s++)
            {
                var keep=new List<int>();int[] ids=triangles[s];
                for(int i=0;i<ids.Length;i+=3)
                {int group=Find(parent,ids[i]);if(rigid[group]&&below[group])continue;keep.Add(ids[i]);keep.Add(ids[i+1]);keep.Add(ids[i+2]);}
                copy.SetTriangles(keep,s,false);
            }
            renderer.sharedMesh=copy;
        }
        void Plate(string name,Transform bone,Vector3 end,Vector3 forward,float start,float finish,float radius,Material material)
        {
            Vector3 axis=end-bone.position;float length=axis.magnitude;Vector3 down=axis.normalized;
            Vector3 front=Vector3.ProjectOnPlane(forward,down).normalized;Vector3 right=Vector3.Cross(down,front).normalized;
            var v=new List<Vector3>();var tri=new List<int>();const int columns=8;
            for(int layer=0;layer<2;layer++)for(int row=0;row<2;row++)for(int j=0;j<=columns;j++)
            {
                float angle=Mathf.Lerp(-1.22f,1.22f,j/(float)columns);
                float r=length*(radius-(layer==0?0:.024f))*(row==0?1:.86f);
                Vector3 p=bone.position+axis*(row==0?start:finish)+(front*Mathf.Cos(angle)+right*Mathf.Sin(angle))*r;
                v.Add(bone.InverseTransformPoint(p));
            }
            int ring=columns+1,layerSize=ring*2;
            for(int j=0;j<columns;j++)
            {
                Quad(tri,j,j+1,j+ring+1,j+ring);Quad(tri,layerSize+j+ring,layerSize+j+ring+1,layerSize+j+1,layerSize+j);
                Quad(tri,j+1,j,layerSize+j,layerSize+j+1);
                Quad(tri,j+ring,j+ring+1,layerSize+j+ring+1,layerSize+j+ring);
            }
            Quad(tri,0,ring,layerSize+ring,layerSize);Quad(tri,columns+ring,columns,layerSize+columns,layerSize+columns+ring);
            var mesh=new Mesh { name=name };mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(bone,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        static void Quad(List<int> t,int a,int b,int c,int d){t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(d);}
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);foreach(var m in materials)if(m!=null)Destroy(m);}
    }
}
