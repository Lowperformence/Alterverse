using System.Collections.Generic;
using UnityEngine;
using Alterverse.Match;

namespace Alterverse.Visuals
{
    public sealed partial class ProductionLunarGround
    {
        bool orbitalDebrisBuilt;

        // ProductionLunarGround builds every arena variant in Start(). Build this tiny shared detail
        // one frame later so it automatically appears on Moon, Mars, Green Planet, Noxara and Kryon
        // without touching gameplay collision, pathing, spawns or arena rules.
        void LateUpdate()
        {
            if(orbitalDebrisBuilt||root==null)return;
            orbitalDebrisBuilt=true;
            BuildOrbitalDebris();
        }

        void BuildOrbitalDebris()
        {
            Color metalColor=ArenaVariantRuntime.IsMars?new Color(.19f,.125f,.095f,1):
                ArenaVariantRuntime.IsGreenPlanet?new Color(.105f,.135f,.105f,1):
                ArenaVariantRuntime.IsNoxara?new Color(.075f,.095f,.145f,1):
                ArenaVariantRuntime.IsKryon?new Color(.17f,.215f,.235f,1):
                new Color(.145f,.155f,.17f,1);
            Color panelColor=ArenaVariantRuntime.IsMars?new Color(.085f,.07f,.065f,1):
                ArenaVariantRuntime.IsGreenPlanet?new Color(.055f,.075f,.06f,1):
                ArenaVariantRuntime.IsNoxara?new Color(.035f,.05f,.095f,1):
                ArenaVariantRuntime.IsKryon?new Color(.095f,.135f,.155f,1):
                new Color(.055f,.07f,.095f,1);

            Material metal=Mat(metalColor,.58f,.13f);
            Material panel=Mat(panelColor,.32f,.18f);

            var metalVerts=new List<Vector3>(240);
            var metalTris=new List<int>(360);
            var panelVerts=new List<Vector3>(160);
            var panelTris=new List<int>(240);

            // Deliberately sparse authored sites: enough to suggest old satellite wreckage, never enough
            // to read as gameplay cover. All pieces stay low to the ground and are purely decorative.
            Vector2[] sites={
                new(-34,21),new(-29,-18),new(-22,25),new(-18,-23),
                new(-10,17),new(-4,-24),new(7,20),new(13,-22),
                new(21,25),new(26,-18),new(33,17),new(36,-24)
            };

            for(int i=0;i<sites.Length;i++)
            {
                Vector2 site=sites[i];
                Vector3 basePos=LunarSurfaceField.Surface(site.x,site.y)+Vector3.up*.025f;
                float yaw=(i*47+13)%360;

                // Main tiny hull shard / casing fragment.
                Vector3 hullSize=new(.16f+(i%3)*.045f,.028f+(i%2)*.012f,.075f+(i%4)*.018f);
                AddDebrisBox(metalVerts,metalTris,basePos,hullSize,Quaternion.Euler(7+(i%3)*5,yaw,(i%2==0?8:-7)));

                // Short antenna/strut fragment next to selected hull pieces.
                if(i%2==0)
                {
                    Vector3 strutPos=basePos+new Vector3(((i%3)-1)*.12f,.012f,(i%2==0?.11f:-.11f));
                    Vector3 strutSize=new(.025f,.022f,.14f+(i%3)*.035f);
                    AddDebrisBox(metalVerts,metalTris,strutPos,strutSize,Quaternion.Euler(3,yaw+61,12));
                }

                // A thin broken solar-panel tile on roughly two thirds of the sites.
                if(i%3!=1)
                {
                    Vector3 panelPos=basePos+new Vector3(i%2==0?.16f:-.15f,.006f,i%4<2?-.08f:.09f);
                    Vector3 panelSize=new(.14f+(i%3)*.035f,.012f,.075f+(i%2)*.018f);
                    AddDebrisBox(panelVerts,panelTris,panelPos,panelSize,Quaternion.Euler(2+(i%2)*4,yaw-28,(i%3-1)*6));
                }
            }

            CreateDebrisObject("DF_OrbitalMetalScrap",metalVerts,metalTris,metal);
            CreateDebrisObject("DF_BrokenSatellitePanels",panelVerts,panelTris,panel);
        }

        void CreateDebrisObject(string name,List<Vector3> vertices,List<int> triangles,Material material)
        {
            if(vertices.Count==0)return;
            Mesh mesh=new(){name=name+"Mesh"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            meshes.Add(mesh);
            GameObject g=MeshObject(name,mesh,material,Vector3.zero);
            g.isStatic=true;
            // Intentionally no Collider component: heroes, soldiers, bots and dashes pass through freely.
        }

        static void AddDebrisBox(List<Vector3> vertices,List<int> triangles,Vector3 center,Vector3 size,Quaternion rotation)
        {
            Vector3 h=size*.5f;
            Vector3[] local={
                new(-h.x,-h.y,-h.z),new(h.x,-h.y,-h.z),new(h.x,h.y,-h.z),new(-h.x,h.y,-h.z),
                new(-h.x,-h.y,h.z),new(h.x,-h.y,h.z),new(h.x,h.y,h.z),new(-h.x,h.y,h.z)
            };
            int start=vertices.Count;
            for(int i=0;i<local.Length;i++)vertices.Add(center+rotation*local[i]);
            int[] boxTris={
                0,2,1,0,3,2, 4,5,6,4,6,7,
                0,1,5,0,5,4, 3,7,6,3,6,2,
                0,4,7,0,7,3, 1,2,6,1,6,5
            };
            for(int i=0;i<boxTris.Length;i++)triangles.Add(start+boxTris[i]);
        }
    }
}
