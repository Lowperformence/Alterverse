#if UNITY_EDITOR
using System;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class SoldierSpawnRegression
    {
        private static void Check(bool value,string reason)
        {if(!value)throw new InvalidOperationException("Soldier spawn regression: "+reason);}

        [MenuItem("DIMENSIONS FRONT/Checks/Soldier spawns in front of trenches")]
        public static void Run()
        {
            const float trenchX=25.25f;
            const float trenchHalfWidth=7.25f*.5f;
            float centreSideEdge=trenchX-trenchHalfWidth;

            foreach(int team in new[]{0,1})
            foreach(float laneZ in new[]{16f,-16f})
            for(int i=0;i<3;i++)
            {
                Vector3 spawn=LaneWaveSpawner.SoldierSpawnPoint(team,laneZ,i);
                Check(Mathf.Abs(spawn.x)<centreSideEdge,
                    $"team {team} soldier {i} is not fully in front of trench line: x={spawn.x}");
                Check(Mathf.Abs(spawn.z-laneZ)<.001f,
                    $"team {team} soldier {i} changed lane Z");
            }

            Debug.Log("DIMENSIONS FRONT: soldier spawn regression passed; all regular wave soldiers start on the arena-centre side of the trenches.");
        }
    }
}
#endif
