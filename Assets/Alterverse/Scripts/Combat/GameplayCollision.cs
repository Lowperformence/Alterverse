using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>Shared map dimensions and deterministic team formation for every arena and peer.</summary>
    public static class ArenaLayout
    {
        // Two successive 10% extensions along the axis between the base centres.
        public const float LengthScale=1.10f*1.10f;
        public const float HalfLength=42f*LengthScale;
        public const float HalfBreadth=36f;
        // Shared team-end anchors. Healing and spawn positions can move independently of defenses.
        public const float CoreX=36f*LengthScale;
        // Keep the enlarged pad visibly separate from the outer base deck.
        public const float HealingX=CoreX-10.6f;
        public const float SpawnX=HealingX-1f;
        public const float HealingPlateScale=1.30f;
        public const float HealingPlateRadius=2.15f*HealingPlateScale;
        public const float HealingRadius=4.5f*HealingPlateScale;
        public const float HealingGuardOffset=HealingPlateRadius+1.25f;
        // Identical defense anchors on every map, including the frontier planets.
        public const float BunkerX=CoreX-3.6f,BunkerZ=21.5f;
        public const float TrenchX=CoreX-2.6f,TrenchZ=10.75f;
        public const float SpawnSpacing=1.4f;
        public static Vector3 Stretch(Vector3 point)
        {point.x*=LengthScale;return point;}
        public static Vector3 HealingPosition(int team,float y=.14f)
            =>new Vector3(team==0?-HealingX:HealingX,y,0f);
        public static Vector3 DefensePosition(int team,bool bunker,int flank)
        {
            float side=team==0?-1f:1f;
            float lateral=(team==0?1f:-1f)*(flank==0?1f:-1f);
            return new Vector3(side*(bunker?BunkerX:TrenchX),0f,lateral*(bunker?BunkerZ:TrenchZ));
        }
        public static bool IsInsideHealingArea(Vector3 point,Vector3 center)
        {
            float dx=point.x-center.x,dz=point.z-center.z;
            return dx*dx+dz*dz<=HealingRadius*HealingRadius;
        }
        public static Vector3 HeroSpawn(int team,int teamSlot,int teamSize)
        {
            int size=Mathf.Max(1,teamSize);
            int slot=Mathf.Clamp(teamSlot,0,size-1);
            float side=team==0?-1f:1f;
            return new Vector3(side*SpawnX,1f,side*(slot-(size-1)*.5f)*SpawnSpacing);
        }
        // Call on a completed terrain root only. Gameplay actors and buildings keep unit scale.
        public static void StretchTerrain(Transform root)
        {root.localScale=new Vector3(LengthScale,1f,1f);}
    }

    // Root hitboxes stay queryable. Mobile units never form movement walls.
    // All three arena variants share these collision rules, so Moon, Mars and Green Planet
    // receive the same rounded navigation treatment.
    public static class GameplayCollision
    {
        public const int Units=8, Structures=9, Ground=10, Decorative=11;
        public const int MovementMask=(1<<0)|(1<<Structures)|(1<<Ground);
        public const int TargetMask=(1<<0)|(1<<Units)|(1<<Structures);
        public const int SightMask=(1<<0)|(1<<Structures);
        public const float ArenaCornerRadius=3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ConfigureUnitCollision()
        {
            // Hero/unit hitboxes stay queryable for combat, but mobile characters must not become
            // hard physics walls for one another. Spacing is handled by movement/AI steering.
            Physics.IgnoreLayerCollision(Units,Units,true);
        }

        public static Vector3 ClampToRoundedArena(Vector3 point,float halfWidth,float halfHeight,float inset=0f)
        {
            // Callers provide authored arena half-widths; point/displacement are already world-space.
            halfWidth=Mathf.Max(.2f,halfWidth*ArenaLayout.LengthScale-Mathf.Max(0f,inset));
            halfHeight=Mathf.Max(.2f,halfHeight-Mathf.Max(0f,inset));
            float radius=Mathf.Min(ArenaCornerRadius,Mathf.Min(halfWidth,halfHeight)-.01f);

            float signX=point.x<0f?-1f:1f,signZ=point.z<0f?-1f:1f;
            float x=Mathf.Min(Mathf.Abs(point.x),halfWidth);
            float z=Mathf.Min(Mathf.Abs(point.z),halfHeight);
            float innerX=halfWidth-radius,innerZ=halfHeight-radius;

            if(x>innerX&&z>innerZ)
            {
                Vector2 cornerDelta=new(x-innerX,z-innerZ);
                float length=cornerDelta.magnitude;
                if(length>radius&&length>.0001f)
                {
                    cornerDelta*=radius/length;
                    x=innerX+cornerDelta.x;
                    z=innerZ+cornerDelta.y;
                }
            }

            point.x=x*signX;
            point.z=z*signZ;
            return point;
        }

        public static bool ContainsRoundedArena(Vector3 point,float halfWidth,float halfHeight,float inset=0f)
        {
            Vector3 clamped=ClampToRoundedArena(point,halfWidth,halfHeight,inset);
            return Mathf.Abs(clamped.x-point.x)<.001f&&Mathf.Abs(clamped.z-point.z)<.001f;
        }

        public static Mesh CreateRoundedPrismMesh(Vector3 center,float width,float height,float depth,float cornerRadius,int arcSegments=6)
        {
            arcSegments=Mathf.Clamp(arcSegments,2,12);
            float halfWidth=width*.5f,halfDepth=depth*.5f;
            float radius=Mathf.Max(.05f,Mathf.Min(cornerRadius,Mathf.Min(halfWidth-.01f,halfDepth-.01f)));

            List<Vector2> footprint=new(4*(arcSegments+1));
            Vector2[] cornerCenters=
            {
                new(halfWidth-radius,-halfDepth+radius),
                new(halfWidth-radius,halfDepth-radius),
                new(-halfWidth+radius,halfDepth-radius),
                new(-halfWidth+radius,-halfDepth+radius)
            };
            float[] startAngles={-90f,0f,90f,180f};

            for(int corner=0;corner<cornerCenters.Length;corner++)
            for(int segment=0;segment<=arcSegments;segment++)
            {
                float angle=(startAngles[corner]+90f*segment/arcSegments)*Mathf.Deg2Rad;
                Vector2 offset=new(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                footprint.Add(cornerCenters[corner]+offset);
            }

            int perimeter=footprint.Count;
            float bottomY=center.y-height*.5f,topY=center.y+height*.5f;
            List<Vector3> vertices=new(perimeter*2+2);
            for(int i=0;i<perimeter;i++)vertices.Add(new Vector3(center.x+footprint[i].x,bottomY,center.z+footprint[i].y));
            for(int i=0;i<perimeter;i++)vertices.Add(new Vector3(center.x+footprint[i].x,topY,center.z+footprint[i].y));

            int bottomCenter=vertices.Count;vertices.Add(new Vector3(center.x,bottomY,center.z));
            int topCenter=vertices.Count;vertices.Add(new Vector3(center.x,topY,center.z));
            List<int> triangles=new(perimeter*12);

            for(int i=0;i<perimeter;i++)
            {
                int next=(i+1)%perimeter,bottom=i,bottomNext=next,top=i+perimeter,topNext=next+perimeter;
                triangles.Add(bottom);triangles.Add(topNext);triangles.Add(bottomNext);
                triangles.Add(bottom);triangles.Add(top);triangles.Add(topNext);
                triangles.Add(bottomCenter);triangles.Add(bottom);triangles.Add(bottomNext);
                triangles.Add(topCenter);triangles.Add(topNext);triangles.Add(top);
            }

            Mesh mesh=new(){name="DF_RoundedGameplayCollider"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GameplayCollisionMeshOwner : MonoBehaviour
    {
        private Mesh ownedMesh;
        public void Own(Mesh mesh)=>ownedMesh=mesh;
        private void OnDestroy(){if(ownedMesh!=null)Destroy(ownedMesh);}
    }
}

