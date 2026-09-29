using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Replaces the undersized Reality Core capsule with a smooth oval movement collider.
    /// The footprint is measured from the actual lower station shell, while the decorative
    /// outer base deck remains walkable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BaseStationOvalCollision : MonoBehaviour
    {
        private const string ColliderMeshName="DF_BaseStationOvalCollider";
        private const string CoreVisualRootName="DF_RealityCoreProduction";
        private const float ScanInterval=.5f;
        private const float FootprintHeight=1.38f;
        private const float HorizontalPadding=.10f;
        private const float VerticalPadding=.08f;
        private const int EllipseSegments=40;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(FindAnyObjectByType<BaseStationOvalCollision>()!=null)return;
            new GameObject("DF_BaseStationOvalCollisionRuntime").AddComponent<BaseStationOvalCollision>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+ScanInterval;

            RealityCore[] cores=FindObjectsByType<RealityCore>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            for(int i=0;i<cores.Length;i++)Configure(cores[i]);
        }

        private static void Configure(RealityCore core)
        {
            if(core==null)return;
            GameObject station=core.gameObject;
            if(HasOvalCollider(station))return;

            Transform shell=station.transform.Find(CoreVisualRootName);
            if(shell==null)return;
            Renderer[] renderers=shell.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)return;

            bool found=false;
            Vector3 min=new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
            Vector3 max=new(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);

            // Only the lower structural mass defines the blocking footprint. Upper chrono rings,
            // halos and other overhanging details remain visual and do not make the deck feel wider.
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer renderer=renderers[i];
                if(renderer==null||!renderer.enabled)continue;
                Bounds bounds=renderer.bounds;
                Vector3 localCenter=station.transform.InverseTransformPoint(bounds.center);
                if(localCenter.y>FootprintHeight)continue;

                Vector3 extents=bounds.extents;
                for(int sx=-1;sx<=1;sx+=2)
                for(int sy=-1;sy<=1;sy+=2)
                for(int sz=-1;sz<=1;sz+=2)
                {
                    Vector3 worldCorner=bounds.center+new Vector3(extents.x*sx,extents.y*sy,extents.z*sz);
                    Vector3 localCorner=station.transform.InverseTransformPoint(worldCorner);
                    min=Vector3.Min(min,localCorner);
                    max=Vector3.Max(max,localCorner);
                    found=true;
                }
            }
            if(!found)return;

            float centerX=(min.x+max.x)*.5f;
            float centerZ=(min.z+max.z)*.5f;
            float radiusX=Mathf.Max(.25f,(max.x-min.x)*.5f+HorizontalPadding);
            float radiusZ=Mathf.Max(.25f,(max.z-min.z)*.5f+HorizontalPadding);

            // Keep the blocker rooted at ground level and tall enough for every hero controller.
            float bottomY=Mathf.Min(-.04f,min.y-VerticalPadding);
            float topY=Mathf.Max(2.65f,max.y+VerticalPadding);
            float height=Mathf.Max(.5f,topY-bottomY);
            Vector3 center=new(centerX,bottomY+height*.5f,centerZ);

            Mesh mesh=CreateEllipsePrism(center,radiusX,radiusZ,height,EllipseSegments);
            MeshCollider collider=station.AddComponent<MeshCollider>();
            collider.sharedMesh=mesh;
            collider.convex=true;
            collider.isTrigger=false;

            CapsuleCollider[] oldCapsules=station.GetComponents<CapsuleCollider>();
            for(int i=0;i<oldCapsules.Length;i++)
            {
                if(oldCapsules[i]==null)continue;
                oldCapsules[i].enabled=false;
                Destroy(oldCapsules[i]);
            }

            GameplayCollisionMeshOwner owner=station.GetComponent<GameplayCollisionMeshOwner>()??station.AddComponent<GameplayCollisionMeshOwner>();
            owner.Own(mesh);
        }

        private static bool HasOvalCollider(GameObject station)
        {
            MeshCollider[] colliders=station.GetComponents<MeshCollider>();
            for(int i=0;i<colliders.Length;i++)
            {
                Mesh mesh=colliders[i].sharedMesh;
                if(mesh!=null&&mesh.name==ColliderMeshName)return true;
            }
            return false;
        }

        private static Mesh CreateEllipsePrism(Vector3 center,float radiusX,float radiusZ,float height,int segments)
        {
            segments=Mathf.Clamp(segments,16,48);
            int perimeter=segments;
            Vector3[] vertices=new Vector3[perimeter*2+2];
            int[] triangles=new int[perimeter*12];
            float bottomY=center.y-height*.5f;
            float topY=center.y+height*.5f;

            for(int i=0;i<perimeter;i++)
            {
                float angle=Mathf.PI*2f*i/perimeter;
                float x=center.x+Mathf.Cos(angle)*radiusX;
                float z=center.z+Mathf.Sin(angle)*radiusZ;
                vertices[i]=new Vector3(x,bottomY,z);
                vertices[i+perimeter]=new Vector3(x,topY,z);
            }

            int bottomCenter=perimeter*2;
            int topCenter=bottomCenter+1;
            vertices[bottomCenter]=new Vector3(center.x,bottomY,center.z);
            vertices[topCenter]=new Vector3(center.x,topY,center.z);

            int t=0;
            for(int i=0;i<perimeter;i++)
            {
                int next=(i+1)%perimeter;
                int bottom=i,bottomNext=next,top=i+perimeter,topNext=next+perimeter;

                triangles[t++]=bottom;triangles[t++]=topNext;triangles[t++]=bottomNext;
                triangles[t++]=bottom;triangles[t++]=top;triangles[t++]=topNext;
                triangles[t++]=bottomCenter;triangles[t++]=bottomNext;triangles[t++]=bottom;
                triangles[t++]=topCenter;triangles[t++]=top;triangles[t++]=topNext;
            }

            Mesh mesh=new(){name=ColliderMeshName};
            mesh.vertices=vertices;
            mesh.triangles=triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
