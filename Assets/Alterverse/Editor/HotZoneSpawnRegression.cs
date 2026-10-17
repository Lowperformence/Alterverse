#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class HotZoneSpawnRegression
    {
        private static void Check(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("Hot Zone spawn regression: "+message);
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Hot Zone central spawn band")]
        public static void Run()
        {
            // Every arena shares the enlarged floor dimensions.
            Bounds current=new Bounds(Vector3.zero,new Vector3(ArenaLayout.HalfLength*2f,1f,ArenaLayout.HalfBreadth*2f));
            Vector2 limits=HotZoneDirector.SpawnCenterHalfExtents(current);
            Check(Mathf.Abs(limits.x-(ArenaLayout.HalfLength*.5f-HotZoneDirector.Radius))<.001f,"capture circle must stop at the resized central-band boundary");
            Check(!HotZoneDirector.IsInsideSpawnBand(new Vector3(-ArenaLayout.HealingX,0f,0f),current),"ZERO spawn pad admitted");
            Check(!HotZoneDirector.IsInsideSpawnBand(new Vector3(ArenaLayout.HealingX,0f,0f),current),"LUMEN spawn pad admitted");
            Check(!HotZoneDirector.IsInsideSpawnBand(new Vector3(30f,0f,24f),current),"former base-side candidate admitted");
            Check(!HotZoneDirector.IsInsideSpawnBand(new Vector3(-30f,0f,-24f),current),"mirrored base-side candidate admitted");

            foreach(Bounds bounds in new[]{current,
                new Bounds(new Vector3(40f,0f,-20f),new Vector3(120f,1f,90f)),
                new Bounds(new Vector3(-10f,0f,15f),new Vector3(48f,1f,40f))})
            {
                limits=HotZoneDirector.SpawnCenterHalfExtents(bounds);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 edge=bounds.center+new Vector3(side*limits.x,0f,side*limits.y);
                    Check(HotZoneDirector.IsInsideSpawnBand(edge,bounds),"legal boundary rejected");
                    Check(!HotZoneDirector.IsInsideSpawnBand(edge+Vector3.right*side*.01f,bounds),"outer quarter admitted");
                    Check(!HotZoneDirector.IsInsideSpawnBand(edge+Vector3.forward*side*.01f,bounds),"floor edge admitted");
                    float circleEdge=edge.x+side*HotZoneDirector.Radius;
                    Check(Mathf.Abs(circleEdge-(bounds.center.x+side*bounds.size.x*.25f))<.001f,
                        "circle crosses quarter boundary on resized/offset map");
                }
            }
            Debug.Log("Hot Zone spawn band passed: bases excluded; full circle remains in central 50% on current, resized and offset floors.");
        }
    }
}
#endif

