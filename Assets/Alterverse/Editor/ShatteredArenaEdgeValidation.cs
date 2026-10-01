#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    public static class ShatteredArenaEdgeValidation
    {
        [MenuItem("DIMENSIONS FRONT/Visuals/Validate all shattered map outlines")]
        public static void ValidateAll()
        {
            ShatteredArenaEdge.ValidateProfile();
            foreach(ArenaVariant planet in Enum.GetValues(typeof(ArenaVariant)))
            {
                int xs=planet==ArenaVariant.Mars?192:planet==ArenaVariant.GJ504b?168:planet==ArenaVariant.DiamondCore?84:144;
                int zs=planet==ArenaVariant.Mars?160:planet==ArenaVariant.GJ504b?144:planet==ArenaVariant.DiamondCore?72:128;
                var p=new Vector2[(xs+1)*(zs+1)];
                for(int z=0;z<=zs;z++)for(int x=0;x<=xs;x++)
                {
                    Vector2 original=new(-42f+84f*x/xs,-36f+72f*z/zs);
                    Vector2 point=ShatteredArenaEdge.Warp(original.x,original.y,planet);
                    if(float.IsNaN(point.x)||float.IsNaN(point.y)||Mathf.Abs(point.x)+.00001f<Mathf.Abs(original.x)||Mathf.Abs(point.y)+.00001f<Mathf.Abs(original.y))
                        throw new InvalidOperationException(planet+": invalid or inward outline");
                    if(Mathf.Max(Mathf.Abs(original.x)/42f,Mathf.Abs(original.y)/36f)<=.88f&&(point-original).sqrMagnitude>1e-8f)
                        throw new InvalidOperationException(planet+": interior moved");
                    p[z*(xs+1)+x]=point;
                }
                for(int z=0;z<zs;z++)for(int x=0;x<xs;x++)
                {
                    int i=z*(xs+1)+x;Vector2 a=p[i],b=p[i+1],c=p[i+xs+1],d=p[i+xs+2];
                    if(Cross(b-a,c-a)<=0||Cross(d-b,c-b)<=0)
                        throw new InvalidOperationException(planet+": folded surface triangle");
                }
            }
            Debug.Log("All eleven shattered outlines: outward-only coverage, unchanged interior and valid triangle winding passed.");
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    }
}
#endif
