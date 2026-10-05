#if UNITY_EDITOR
using System;
using Alterverse.Match;
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
            // All current arena variants share this 84 x 72 gameplay floor.
            Bounds current=new Bounds(Vector3.zero,new Vector3(84f,1f,72f));
            Vector2 limits=HotZoneDirector.SpawnCenterHalfExtents(current);
            Check(Mathf.Abs(limits.x-4.25f)<.001f,"capture circle must stop at X = +/-14");
            Check(!HotZoneDirector.IsInsideSpawnBand(new Vector3(-27.5f,0f,0f),current),"ZERO spawn pad admitted");
            Check(!HotZoneDirector.IsInsideSpawnBand(new Vector3(27.5f,0f,0f),current),"LUMEN spawn pad admitted");
            Check(!HotZoneDirector.IsInsideSpawnBand(new Vector3(30f,0f,24f),current),"former base-side candidate admitted");
            Check(!HotZoneDirector.IsInsideSpawnBand(new Vector3(-30f,0f,-24f),current),"mirrored base-side candidate admitted");

            foreach(Bounds bounds in new[]{current,
                new Bounds(new Vector3(40f,0f,-20f),new Vector3(120f,1f,90f)),
                new Bounds(new Vector3(-10f,0f,15f),new Vector3(72f,1f,40f))})
            {
                limits=HotZoneDirector.SpawnCenterHalfExtents(bounds);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 edge=bounds.center+new Vector3(side*limits.x,0f,side*limits.y);
                    Check(HotZoneDirector.IsInsideSpawnBand(edge,bounds),"legal boundary rejected");
                    Check(!HotZoneDirector.IsInsideSpawnBand(edge+Vector3.right*side*.01f,bounds),"outer third admitted");
                    Check(!HotZoneDirector.IsInsideSpawnBand(edge+Vector3.forward*side*.01f,bounds),"floor edge admitted");
                    float circleEdge=edge.x+side*HotZoneDirector.Radius;
                    Check(Mathf.Abs(circleEdge-(bounds.center.x+side*bounds.size.x/6f))<.001f,
                        "circle crosses third boundary on resized/offset map");
                }
            }
            Debug.Log("Hot Zone spawn band passed: bases excluded; full circle remains in central third on current, resized and offset floors.");
        }
    }
}
#endif
