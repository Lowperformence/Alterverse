using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Build-time exclusion footprints, including interiors and a rounded two-metre apron.</summary>
    public sealed class SceneryBuildingClearance
    {
        public const float Margin = 2f;
        readonly List<Bounds> footprints = new();

        readonly float candidateLengthScale;
        public SceneryBuildingClearance(float candidateLengthScale=1f)
        {
            this.candidateLengthScale=candidateLengthScale;
            // Layout fallback also protects builds that run before the structure visuals exist.
            for(int team=0;team<2;team++)
            {
                float side=team==0?-1f:1f;
                Add(new Vector3(side*ArenaLayout.CoreX,0,0),new Vector3(11f,1,11f));
                float diameter=ArenaLayout.HealingRadius*2f;
                Add(ArenaLayout.HealingPosition(team,0f),new Vector3(diameter,1f,diameter));
                for(int flank=0;flank<2;flank++)
                {
                    // Use the very same world-space anchors as the structure builder.
                    Add(ArenaLayout.DefensePosition(team,true,flank),new Vector3(10f,1f,9f));
                    Add(ArenaLayout.DefensePosition(team,false,flank),new Vector3(7f,1f,9f));
                }
            }
            foreach (var building in Object.FindObjectsByType<DefenseBunker>(FindObjectsSortMode.None)) AddBuilding(building.transform);
            foreach (var building in Object.FindObjectsByType<DefenseTrench>(FindObjectsSortMode.None)) AddBuilding(building.transform);
            foreach (string name in new[]{"DF_ZeroRealityCore","DF_LumenRealityCore"})
            {
                var core=GameObject.Find(name);
                if(core!=null)AddBuilding(core.transform);
            }
        }

        void Add(Vector3 center,Vector3 size) => footprints.Add(new Bounds(center,size));
        void AddBuilding(Transform building)
        {
            var renderers=building.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0)return;
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            footprints.Add(bounds);
        }

        // Horizontal distance between full bounds, not pivots. Height never permits rocks inside a roof.
        public static bool Overlaps(Bounds candidate,Bounds building,float margin=Margin)
        {
            float dx=Mathf.Max(0f,Mathf.Abs(candidate.center.x-building.center.x)-candidate.extents.x-building.extents.x);
            float dz=Mathf.Max(0f,Mathf.Abs(candidate.center.z-building.center.z)-candidate.extents.z-building.extents.z);
            return dx*dx+dz*dz<=margin*margin;
        }

        public bool Blocks(Bounds worldBounds)
        {
            Vector3 center=worldBounds.center,size=worldBounds.size;
            center.x*=candidateLengthScale;size.x*=candidateLengthScale;
            worldBounds=new Bounds(center,size);
            foreach(var footprint in footprints)if(Overlaps(worldBounds,footprint))return true;
            return false;
        }

        public bool Blocks(Mesh mesh,Matrix4x4 localToWorld)
            => footprints.Count>0 && Blocks(TransformBounds(mesh.bounds,localToWorld));

        public static Bounds TransformBounds(Bounds local,Matrix4x4 matrix)
        {
            Bounds result=new(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));
            return result;
        }

        public bool RemoveIfBlocked(GameObject decoration)
        {
            if(footprints.Count==0)return false;
            var renderers=decoration.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0)return false;
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            if(!Blocks(bounds))return false;
            // Destroy is deferred; hide now so batching cannot resurrect a rejected decoration.
            decoration.SetActive(false);
            Object.Destroy(decoration);
            return true;
        }
    }
}

