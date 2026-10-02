#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Alterverse.EditorTools
{
    internal static class TravelerOrbImportPass
    {
        internal static void Apply(GameObject root)
        {
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin.name=="DF_WeaponR"){RemoveOrbHandle(skin);CenterOrbMuzzle(root,skin);}
        }
        static int Bone(SkinnedMeshRenderer skin,string name)=>Array.FindIndex(skin.bones,b=>b!=null&&b.name==name);
        private static void CenterOrbMuzzle(GameObject root, SkinnedMeshRenderer skin)
        {
            Mesh mesh=skin.sharedMesh;
            int grip=Bone(skin,"WeaponGripR");
            if(mesh==null||grip<0)return;
            Transform muzzle=null;
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                if(t.name=="DF_Muzzle") { muzzle=t;break; }
            if(muzzle==null)return;
            Material[] materials=skin.sharedMaterials;
            Vector3 center=Vector3.zero;int count=0;
            Vector3[] points=mesh.vertices;
            for(int sub=0;sub<Mathf.Min(mesh.subMeshCount,materials.Length);sub++)
            {
                if(materials[sub]==null||!materials[sub].name.ToLowerInvariant().Contains("chronolight"))continue;
                var unique=new HashSet<int>(mesh.GetTriangles(sub));
                foreach(int index in unique) { center+=points[index];count++; }
            }
            if(count==0)return;
            muzzle.SetParent(skin.bones[grip],false);
            muzzle.localPosition=mesh.bindposes[grip].MultiplyPoint3x4(center/count);
        }

        internal static void RemoveOrbHandle(SkinnedMeshRenderer skin)
        {
            Mesh mesh = skin.sharedMesh;
            if (mesh == null) return;
            Vector3[] vertices = mesh.vertices;
            var parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            void Join(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
            // Weld duplicated UV/normal seam vertices for component classification only.
            float epsilon = Mathf.Max(mesh.bounds.size.magnitude * 1e-6f, 1e-8f);
            var welded = new Dictionary<Vector3Int,int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i] / epsilon;
                var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                if (welded.TryGetValue(key, out int other)) Join(i, other); else welded.Add(key, i);
            }
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] indices = mesh.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3) { Join(indices[i],indices[i+1]); Join(indices[i],indices[i+2]); }
            }
            var groups = new Dictionary<int,List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            { int key=Find(i); if(!groups.TryGetValue(key,out var list))groups.Add(key,list=new List<int>());list.Add(i); }
            var remove = new HashSet<int>();
            foreach (var group in groups)
            {
                // The known handle is the only isolated straight tube. The orb and its
                // three closed orbit rings have two similarly large principal extents.
                // Principal-axis measurement is independent of the FBX rest rotation.
                var ids=group.Value;
                if(ids.Count<12)continue;
                Vector3 center=Vector3.zero;
                foreach(int i in ids)center+=vertices[i];
                center/=ids.Count;
                // Principal axis, not a diagonal through the cylinder caps.
                Vector3 axis=Vector3.one.normalized;
                for(int iteration=0;iteration<24;iteration++)
                {
                    Vector3 next=Vector3.zero;
                    foreach(int i in ids)
                    { Vector3 p=vertices[i]-center;next+=p*Vector3.Dot(p,axis); }
                    if(next.sqrMagnitude<1e-20f)break;
                    axis=next.normalized;
                }
                float low=float.MaxValue,high=float.MinValue,radial=0;
                foreach(int i in ids)
                {
                    Vector3 p=vertices[i]-center;float t=Vector3.Dot(p,axis);
                    low=Mathf.Min(low,t);high=Mathf.Max(high,t);
                    radial=Mathf.Max(radial,Vector3.ProjectOnPlane(p,axis).magnitude);
                }
                float length=high-low;
                if(length>radial*7f)remove.Add(group.Key);
            }
            if(remove.Count==0)return; // Already rebuilt without a handle.
            if(remove.Count!=1) { Debug.LogError("DF: Traveler handle topology is ambiguous; no geometry removed."); return; }
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                int[] indices=mesh.GetTriangles(sub);var kept=new List<int>();
                for(int i=0;i<indices.Length;i+=3)
                    if(!remove.Contains(Find(indices[i]))) { kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]); }
                mesh.SetTriangles(kept,sub,false);
            }
            mesh.RecalculateBounds();
        }
    }
}
#endif
