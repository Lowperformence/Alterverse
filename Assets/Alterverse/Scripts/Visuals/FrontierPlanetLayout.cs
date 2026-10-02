using Alterverse.Match;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Independent, deterministic geology and defensive layouts for the two exoplanets.</summary>
    public static class FrontierPlanetLayout
    {
        public static bool Active=>ArenaVariantRuntime.IsGJ504b||ArenaVariantRuntime.IsDiamondCore;
        public static Vector2 BunkerPosition=>ArenaVariantRuntime.IsGJ504b?new Vector2(24f,23.5f):new Vector2(22f,25f);
        public static Vector2 TrenchPosition=>ArenaVariantRuntime.IsGJ504b?new Vector2(21.5f,11.5f):new Vector2(23.5f,13f);

        public static int ObstacleCount=>ArenaVariantRuntime.IsGJ504b?8:10;
        public static Vector3 ObstaclePosition(int index)
        {
            int perSide=ObstacleCount/2;
            int side=index<perSide?-1:1,i=index%perSide;
            if(ArenaVariantRuntime.IsGJ504b)
            {
                Vector2[] mesas={new(35,30),new(18,31),new(39,15),new(8,33)};
                return new Vector3(mesas[i].x*side,1f,mesas[i].y*side);
            }
            return new Vector3(side*(8f+i*6.4f),.60f,side*(29f+(i%2)*2.3f));
        }

        // Envelope of all mesa layers and their gravel fan; shared by visual and collision builders.
        public static Bounds ObstacleSceneryBounds(int index)=>new Bounds(ObstaclePosition(index),ArenaVariantRuntime.IsGJ504b?new Vector3(12f,8f,10f):new Vector3(9f,7f,9f));

        public static void BuildObstacles(Transform parent)
        {
            var clearance=new SceneryBuildingClearance();
            for(int i=0;i<ObstacleCount;i++)
            {
                if(clearance.Blocks(ObstacleSceneryBounds(i)))continue;
                var go=new GameObject(ArenaVariantRuntime.IsGJ504b?"DF_GJ504bMesaCollision":"DF_PSRJ1719TerraceCollision");
                go.transform.SetParent(parent,false);go.transform.localPosition=ObstaclePosition(i);
                go.layer=GameplayCollision.Structures;
                var box=go.AddComponent<BoxCollider>();
                box.size=ArenaVariantRuntime.IsGJ504b?new Vector3(3.5f,2.6f,2.7f):new Vector3(4.6f,1.8f,2.4f);
            }
        }

        public static void Sample(float x,float z,out float relief,out float tone,out float dust)
        {
            // Rotational symmetry gives both teams the same approach and lighting-value range.
            float ax=Mathf.Abs(x),az=Mathf.Abs(z);
            float court=Falloff(5f,11f,new Vector2(x,z).magnitude);
            float bases=Falloff(3f,7f,new Vector2(ax-30f,z).magnitude);
            float centerRoute=Falloff(3f,7f,az);
            float clear=Mathf.Max(court,Mathf.Max(bases,centerRoute));
            if(ArenaVariantRuntime.IsGJ504b)
            {
                float warp=Mathf.Sin(ax*.13f)*.7f+Mathf.Cos(az*.17f)*.4f;
                float dunes=Mathf.Sin(az*.43f+ax*.09f+warp);
                float sand=.5f+.5f*Mathf.Sin(ax*.16f-az*.11f);
                relief=(dunes*.10f+Mathf.Sin(ax*.31f+az*.23f)*.026f)*(1f-clear*.9f);
                tone=Mathf.Clamp(.50f+dunes*.075f+sand*.055f,.34f,.68f);
                dust=.30f+sand*.34f;
            }
            else
            {
                // Broad facets and angular fault shelves, with no Moon impact field or sand dunes.
                float facet=Mathf.Abs(Mathf.Sin(ax*.18f+az*.13f));
                float fault=Mathf.Exp(-Mathf.Pow((az-(18f+Mathf.Sin(ax*.12f)*2f))/.8f,2f));
                relief=(facet*.075f-fault*.13f)*(1f-clear*.85f);
                tone=Mathf.Clamp(.45f+facet*.15f-fault*.08f,.30f,.68f);
                dust=0f;
            }
        }

        public static Vector3 Surface(float x,float z)
        {
            float fracture=ShatteredArenaEdge.FractureDepth(x,z);
            Vector2 edge=ShatteredArenaEdge.Warp(x,z);x=edge.x;z=edge.y;
            Sample(x,z,out float height,out _,out _);
            return new Vector3(x,.015f+height-fracture,z);
        }

        static float Falloff(float inner,float outer,float distance)
            =>1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(inner,outer,distance));
    }
}


