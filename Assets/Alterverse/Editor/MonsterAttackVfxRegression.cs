#if UNITY_EDITOR
using System;
using System.Reflection;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class MonsterAttackVfxRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Individual monster attack effects")]
        public static void Run()
        {
            ArenaVariant saved=ArenaVariantRuntime.Current;
            var monster=new GameObject("Monster VFX regression");
            var guard=new GameObject("Guard VFX regression");
            guard.AddComponent<HealingGuardMech>();
            try
            {
                var expected=new[]{MonsterAttackStyle.Mech,MonsterAttackStyle.Skeleton,MonsterAttackStyle.Alien,
                    MonsterAttackStyle.Poison,MonsterAttackStyle.Frost,MonsterAttackStyle.Mech,MonsterAttackStyle.Scorpion,
                    MonsterAttackStyle.Tentacle,MonsterAttackStyle.Orb,MonsterAttackStyle.Diamond,MonsterAttackStyle.Stone,
                    MonsterAttackStyle.Iron,MonsterAttackStyle.Bio};
                Array variants=Enum.GetValues(typeof(ArenaVariant));
                Require(variants.Length==expected.Length,"A new arena needs an explicit monster attack style");
                foreach(ArenaVariant variant in variants)
                {
                    ArenaVariantRuntime.BeginCoopMatch(variant);
                    Require(MonsterAttackVfx.StyleFor(monster)==expected[(int)variant],"Wrong attack style on "+variant);
                    MonsterAttackStyle guardStyle=variant==ArenaVariant.Mars||variant==ArenaVariant.GJ504b?MonsterAttackStyle.Stone:
                        variant==ArenaVariant.DiamondCore?MonsterAttackStyle.Diamond:
                        variant==ArenaVariant.GreenPlanet?MonsterAttackStyle.Alien:MonsterAttackStyle.Mech;
                    Require(MonsterAttackVfx.StyleFor(guard)==guardStyle,"Wrong guard projectile on "+variant);
                }
                var build=typeof(HeroVfxPool).GetMethod("BuildShape",BindingFlags.NonPublic|BindingFlags.Static);
                Require(build!=null,"Shared mesh factory missing");
                Type shapeType=build.GetParameters()[0].ParameterType;
                foreach(string name in new[]{"MonsterRock","MonsterDiamond","MonsterSlime","MonsterBeam"})
                {
                    Mesh mesh=(Mesh)build.Invoke(null,new[]{Enum.Parse(shapeType,name),(object)true});
                    try
                    {
                        Require(mesh!=null&&mesh.vertexCount>0&&mesh.vertexCount<1024,"Invalid projectile mesh: "+name);
                        foreach(var point in mesh.vertices)Require(Finite(point.x)&&Finite(point.y)&&Finite(point.z),"Non-finite mesh: "+name);
                        foreach(var normal in mesh.normals)Require(Finite(normal.x)&&normal.sqrMagnitude>.8f,"Invalid normal: "+name);
                        int[] triangles=mesh.triangles;Vector3[] vertices=mesh.vertices;
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                            Vector3 face=Vector3.Cross(b-a,c-a);
                            Require(face.sqrMagnitude>.000000001f,"Degenerate projectile triangle: "+name);
                            if(name!="MonsterBeam")Require(Vector3.Dot(face,(a+b+c)/3f)>0,"Inside-out projectile face: "+name);
                        }
                        if(name=="MonsterBeam")
                            Require(Mathf.Abs(mesh.bounds.min.z)<.0001f&&Mathf.Abs(mesh.bounds.max.z-1)<.0001f,"Laser endpoint mismatch");
                    }
                    finally{if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);}
                }
                foreach(string name in new[]{"Box","Cylinder"})
                {
                    var factory=typeof(MechMachineGunVisual).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static);
                    Require(factory!=null,"Gatling mesh factory missing: "+name);
                    Mesh mesh=(Mesh)factory.Invoke(null,null);
                    try
                    {
                        Require(mesh.vertexCount<256,"Gatling part exceeds mobile mesh budget");
                        var points=mesh.vertices;var indices=mesh.triangles;
                        for(int i=0;i<indices.Length;i+=3)
                        {
                            Vector3 a=points[indices[i]],b=points[indices[i+1]],c=points[indices[i+2]];
                            Require(Vector3.Dot(Vector3.Cross(b-a,c-a),(a+b+c)/3)>0,"Inside-out Gatling face: "+name);
                        }
                    }
                    finally{UnityEngine.Object.DestroyImmediate(mesh);}
                }
                Debug.Log("DF: all 13 arena and guardian attack styles, four projectile meshes and two Gatling part meshes passed. Run Unity play-mode/online visual checks for timing and framing.");
            }
            finally
            {
                ArenaVariantRuntime.BeginCoopMatch(saved);
                UnityEngine.Object.DestroyImmediate(monster);UnityEngine.Object.DestroyImmediate(guard);
            }
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    }
}
#endif
