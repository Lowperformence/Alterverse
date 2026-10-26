using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // BOLT-7 uses one body renderer. Keep the overlay on Weapon.L/R and their
    // animated children, retaining the original skinning and all native materials.
    internal static class Bolt7WeaponColorwayMask
    {
        internal static bool IsWeapon(Transform bone)
        {
            for(Transform current=bone;current!=null;current=current.parent)
                if(current.name=="Weapon.L"||current.name=="Weapon.R")return true;
            return false;
        }

        internal static int[] FilterTriangles(int[] triangles,BoneWeight[] weights,bool[] weaponBones)
        {
            var kept=new List<int>();
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                bool weapon=true;
                for(int j=0;j<3;j++)
                {
                    int vertex=triangles[i+j];
                    if(vertex<0||vertex>=weights.Length){weapon=false;break;}
                    BoneWeight weight=weights[vertex];
                    if(weight.weight0<.999f||weight.boneIndex0<0||weight.boneIndex0>=weaponBones.Length||!weaponBones[weight.boneIndex0])
                    {weapon=false;break;}
                }
                if(weapon){kept.Add(triangles[i]);kept.Add(triangles[i+1]);kept.Add(triangles[i+2]);}
            }
            return kept.ToArray();
        }

        internal static void KeepWeapons(Mesh overlay,Transform[] bones)
        {
            var mask=new bool[bones.Length];
            for(int i=0;i<bones.Length;i++)mask[i]=IsWeapon(bones[i]);
            BoneWeight[] weights=overlay.boneWeights;
            Vector3[] vertices=overlay.vertices;
            bool started=false;Bounds bounds=default;
            for(int submesh=0;submesh<overlay.subMeshCount;submesh++)
            {
                int[] kept=FilterTriangles(overlay.GetTriangles(submesh),weights,mask);
                overlay.SetTriangles(kept,submesh,false);
                foreach(int index in kept)
                {
                    if(!started){bounds=new Bounds(vertices[index],Vector3.zero);started=true;}
                    else bounds.Encapsulate(vertices[index]);
                }
            }
            if(started)overlay.bounds=bounds;
        }
    }
}

