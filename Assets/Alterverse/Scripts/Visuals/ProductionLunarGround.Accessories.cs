using UnityEngine;

namespace Alterverse.Visuals
{
    // Static, collider-free battlefield storytelling props. Everything is intentionally simple,
    // shared and batch-friendly so the lunar arena gains identity without hurting mobile gameplay.
    public sealed partial class ProductionLunarGround
    {
        Material machineMetal,machineDark;
        Mesh accessoryBox,accessoryCylinder,accessoryRing;

        void BuildBattlefieldAccessories()
        {
            EnsureAccessoryAssets();

            // ZERO side: damaged, asymmetrical field technology distributed through several map bands.
            BuildMaintenanceRobot(new Vector2(-15.2f,29.1f),218f,true,true);
            BuildMaintenanceRobot(new Vector2(-18.7f,-10.4f),116f,true,false);
            BuildScannerAntenna(new Vector2(-30.2f,-27.4f),28f,true,true);
            BuildScannerAntenna(new Vector2(-35.5f,10.2f),74f,true,false);
            BuildFieldGenerator(new Vector2(-24.8f,-7.5f),-18f,true,true);
            BuildFieldGenerator(new Vector2(-11.8f,10.1f),33f,true,false);

            // LUMEN side: cleaner, more ordered equipment, mirrored in density rather than exact layout.
            BuildMaintenanceRobot(new Vector2(16.1f,28.1f),152f,false,false);
            BuildMaintenanceRobot(new Vector2(19.2f,-10.8f),236f,false,true);
            BuildScannerAntenna(new Vector2(29.1f,26.8f),-34f,false,false);
            BuildScannerAntenna(new Vector2(35.1f,-9.7f),-62f,false,true);
            BuildFieldGenerator(new Vector2(25.4f,7.2f),194f,false,false);
            BuildFieldGenerator(new Vector2(12.4f,-9.7f),151f,false,false);

            // Neutral wrecks/probes fill outer and midfield crater pockets while preserving readable free-roam combat space.
            BuildRoverWreck(new Vector2(-33.7f,27.0f),32f,true);
            BuildRoverWreck(new Vector2(-27.4f,-20.8f),118f,true);
            BuildRoverWreck(new Vector2(33.8f,-27.0f),205f,false);
            BuildRoverWreck(new Vector2(27.8f,20.6f),296f,false);
            BuildDimensionalProbe(new Vector2(-7.9f,29.3f),12f,true);
            BuildDimensionalProbe(new Vector2(-8.8f,-22.1f),54f,true);
            BuildDimensionalProbe(new Vector2(8.9f,-29.1f),188f,false);
            BuildDimensionalProbe(new Vector2(9.4f,22.4f),232f,false);

            // Small low-profile caches add detail to empty pockets without becoming navigation obstacles.
            BuildSupplyCache(new Vector2(-31.4f,-9.2f),17f,true);
            BuildSupplyCache(new Vector2(-20.6f,21.3f),81f,true);
            BuildSupplyCache(new Vector2(-5.7f,-12.1f),132f,true);
            BuildSupplyCache(new Vector2(31.2f,9.4f),201f,false);
            BuildSupplyCache(new Vector2(21.2f,-21.7f),266f,false);
            BuildSupplyCache(new Vector2(6.3f,12.3f),318f,false);

            foreach(Vector2 site in new[]{
                new Vector2(-33.7f,27f),new Vector2(33.8f,-27f),new Vector2(-15.2f,29.1f),new Vector2(16.1f,28.1f),
                new Vector2(-27.4f,-20.8f),new Vector2(27.8f,20.6f),new Vector2(-18.7f,-10.4f),new Vector2(19.2f,-10.8f)})
                BuildAccessoryDebris(site,Mathf.RoundToInt((site.x+43f)*7f+(site.y+37f)*11f));
        }

        void EnsureAccessoryAssets()
        {
            if(accessoryBox!=null)return;
            machineMetal=Mat(new Color(.17f,.19f,.22f,1),.58f,.24f);
            machineDark=Mat(new Color(.07f,.08f,.095f,1),.42f,.16f);
            accessoryBox=CreateAccessoryBox();
            accessoryCylinder=ProductionMeshFactory.Disc(.5f,.5f,8);
            accessoryRing=ProductionMeshFactory.Ring(.55f,.065f,16,6);
            meshes.Add(accessoryBox);meshes.Add(accessoryCylinder);meshes.Add(accessoryRing);
        }

        void BuildMaintenanceRobot(Vector2 site,float yaw,bool zero,bool damaged)
        {
            Vector3 anchor=LunarSurfaceField.Surface(site.x,site.y);Quaternion facing=Quaternion.Euler(0,yaw,0);
            Material energy=zero?zeroEnergy:lumenEnergy;
            Part("DF_MaintenanceBot_Body",accessoryBox,machineMetal,anchor,facing,new Vector3(0,.52f,0),new Vector3(.72f,.5f,.58f));
            Part("DF_MaintenanceBot_Head",accessoryBox,machineDark,anchor,facing,new Vector3(0,1.02f,.08f),new Vector3(.48f,.28f,.42f));
            Part("DF_MaintenanceBot_Sensor",accessoryCylinder,energy,anchor,facing*Quaternion.Euler(90,0,0),new Vector3(.18f,1.08f,.31f),new Vector3(.13f,.1f,.13f));
            Part("DF_MaintenanceBot_TrackL",accessoryBox,machineDark,anchor,facing,new Vector3(-.48f,.28f,0),new Vector3(.2f,.27f,.76f));
            Part("DF_MaintenanceBot_TrackR",accessoryBox,machineDark,anchor,facing,new Vector3(.48f,.28f,0),new Vector3(.2f,.27f,.76f));
            float armTilt=damaged?34f:-18f;
            Part("DF_MaintenanceBot_Arm",accessoryCylinder,machineMetal,anchor,facing*Quaternion.Euler(0,0,armTilt),new Vector3(.63f,.72f,.03f),new Vector3(.12f,.62f,.12f));
            if(damaged)
                Part("DF_MaintenanceBot_DroppedPanel",accessoryBox,machineDark,anchor,facing*Quaternion.Euler(11,24,7),new Vector3(-.95f,.09f,.62f),new Vector3(.5f,.07f,.34f));
        }

        void BuildScannerAntenna(Vector2 site,float yaw,bool zero,bool damaged)
        {
            Vector3 anchor=LunarSurfaceField.Surface(site.x,site.y);Quaternion facing=Quaternion.Euler(0,yaw,0);
            Material energy=zero?zeroEnergy:lumenEnergy;
            Part("DF_Scanner_Base",accessoryCylinder,machineDark,anchor,facing,new Vector3(0,.14f,0),new Vector3(.72f,.22f,.72f));
            Part("DF_Scanner_Mast",accessoryCylinder,machineMetal,anchor,facing,new Vector3(0,1.35f,0),new Vector3(.11f,2.5f,.11f));
            Part("DF_Scanner_Array",accessoryRing,machineMetal,anchor,facing*Quaternion.Euler(90,damaged?18f:0,0),new Vector3(0,2.45f,0),new Vector3(1.15f,1.15f,1.15f));
            Part("DF_Scanner_Core",accessoryCylinder,energy,anchor,facing,new Vector3(0,2.45f,0),new Vector3(.2f,.18f,.2f));
            Part("DF_Scanner_Panel",accessoryBox,machineDark,anchor,facing*Quaternion.Euler(damaged?26f:-12f,0,0),new Vector3(.82f,.78f,.08f),new Vector3(.7f,.07f,.46f));
        }

        void BuildFieldGenerator(Vector2 site,float yaw,bool zero,bool damaged)
        {
            Vector3 anchor=LunarSurfaceField.Surface(site.x,site.y);Quaternion facing=Quaternion.Euler(0,yaw,0);
            Material energy=zero?zeroEnergy:lumenEnergy;
            Part("DF_FieldGenerator_Base",accessoryBox,machineDark,anchor,facing,new Vector3(0,.18f,0),new Vector3(1.2f,.22f,.9f));
            Part("DF_FieldGenerator_Housing",accessoryBox,machineMetal,anchor,facing,new Vector3(0,.62f,0),new Vector3(.82f,.48f,.68f));
            Part("DF_FieldGenerator_CoilL",accessoryCylinder,machineDark,anchor,facing*Quaternion.Euler(90,0,0),new Vector3(-.56f,.72f,0),new Vector3(.24f,.56f,.24f));
            Part("DF_FieldGenerator_CoilR",accessoryCylinder,machineDark,anchor,facing*Quaternion.Euler(90,0,0),new Vector3(.56f,.72f,0),new Vector3(.24f,.56f,.24f));
            Part("DF_FieldGenerator_Core",accessoryCylinder,energy,anchor,facing,new Vector3(0,1.02f,0),new Vector3(.24f,.18f,.24f));
            if(damaged)
                Part("DF_FieldGenerator_BrokenCover",accessoryBox,machineMetal,anchor,facing*Quaternion.Euler(15,-22,12),new Vector3(1.08f,.11f,.45f),new Vector3(.62f,.06f,.42f));
        }

        void BuildRoverWreck(Vector2 site,float yaw,bool zero)
        {
            Vector3 anchor=LunarSurfaceField.Surface(site.x,site.y);Quaternion facing=Quaternion.Euler(7f,yaw,-6f);
            Material energy=zero?zeroEnergy:lumenEnergy;
            Part("DF_RoverWreck_Chassis",accessoryBox,machineMetal,anchor,facing,new Vector3(0,.34f,0),new Vector3(1.28f,.3f,.78f));
            Part("DF_RoverWreck_Cabin",accessoryBox,machineDark,anchor,facing,new Vector3(.12f,.78f,-.08f),new Vector3(.62f,.42f,.56f));
            for(int side=-1;side<=1;side+=2)
            for(int axle=-1;axle<=1;axle+=2)
                Part("DF_RoverWreck_Wheel",accessoryCylinder,machineDark,anchor,facing*Quaternion.Euler(0,0,90),
                    new Vector3(side*.82f,.22f,axle*.53f),new Vector3(.32f,.18f,.32f));
            Part("DF_RoverWreck_Mast",accessoryCylinder,machineMetal,anchor,facing*Quaternion.Euler(0,0,42),new Vector3(-.42f,1.12f,.08f),new Vector3(.08f,.9f,.08f));
            Part("DF_RoverWreck_Beacon",accessoryCylinder,energy,anchor,facing,new Vector3(-.72f,1.42f,.08f),new Vector3(.13f,.1f,.13f));
        }

        void BuildSupplyCache(Vector2 site,float yaw,bool zero)
        {
            Vector3 anchor=LunarSurfaceField.Surface(site.x,site.y);Quaternion facing=Quaternion.Euler(0,yaw,0);
            Material energy=zero?zeroEnergy:lumenEnergy;
            Part("DF_SupplyCache_Base",accessoryBox,machineDark,anchor,facing,new Vector3(0,.14f,0),new Vector3(.92f,.18f,.66f));
            Part("DF_SupplyCache_CrateA",accessoryBox,machineMetal,anchor,facing,new Vector3(-.34f,.43f,.05f),new Vector3(.46f,.32f,.42f));
            Part("DF_SupplyCache_CrateB",accessoryBox,machineMetal,anchor,facing*Quaternion.Euler(0,18f,0),new Vector3(.3f,.35f,-.12f),new Vector3(.4f,.25f,.36f));
            Part("DF_SupplyCache_Beacon",accessoryCylinder,energy,anchor,facing,new Vector3(.58f,.48f,.18f),new Vector3(.1f,.32f,.1f));
        }

        void BuildDimensionalProbe(Vector2 site,float yaw,bool zero)
        {
            Vector3 anchor=LunarSurfaceField.Surface(site.x,site.y);Quaternion facing=Quaternion.Euler(0,yaw,0);
            Material energy=zero?zeroEnergy:lumenEnergy;
            Part("DF_DimensionProbe_Base",accessoryCylinder,machineDark,anchor,facing,new Vector3(0,.15f,0),new Vector3(.62f,.2f,.62f));
            Part("DF_DimensionProbe_Column",accessoryCylinder,machineMetal,anchor,facing,new Vector3(0,.88f,0),new Vector3(.13f,1.35f,.13f));
            Part("DF_DimensionProbe_Ring",accessoryRing,energy,anchor,facing,new Vector3(0,1.45f,0),new Vector3(.9f,.9f,.9f));
            Part("DF_DimensionProbe_Core",accessoryCylinder,energy,anchor,facing,new Vector3(0,1.45f,0),new Vector3(.18f,.32f,.18f));
            for(int i=0;i<3;i++)
            {
                float angle=(yaw+i*120f)*Mathf.Deg2Rad;
                Vector3 foot=new Vector3(Mathf.Cos(angle)*.62f,.2f,Mathf.Sin(angle)*.62f);
                Quaternion leg=Quaternion.FromToRotation(Vector3.up,(foot-new Vector3(0,.78f,0)).normalized);
                Part("DF_DimensionProbe_Leg",accessoryCylinder,machineMetal,anchor,leg,new Vector3(foot.x*.5f,.48f,foot.z*.5f),new Vector3(.075f,.78f,.075f));
            }
        }

        void BuildAccessoryDebris(Vector2 site,int seed)
        {
            for(int i=0;i<5;i++)
            {
                float a=(seed+i*73f)*Mathf.Deg2Rad;
                float radius=1.45f+(i%3)*.55f;
                Vector2 p=site+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                float size=.18f+(i%3)*.12f;
                LandmarkRock("DF_AccessoryRock",p,new Vector3(size,.16f+(i%2)*.12f,size*.82f),seed+i*37,0,i%2==0?rimMat:darkRegolith);
            }
            Vector3 anchor=LunarSurfaceField.Surface(site.x,site.y);
            Part("DF_ScrapPlate",accessoryBox,machineDark,anchor,Quaternion.Euler(8,seed%360,5),new Vector3(1.35f,.08f,-.74f),new Vector3(.42f,.045f,.25f));
        }

        void Part(string name,Mesh mesh,Material material,Vector3 anchor,Quaternion rotation,Vector3 offset,Vector3 scale)
        {
            Vector3 position=anchor+rotation*offset;
            GameObject g=MeshObject(name,mesh,material,position);
            g.transform.localRotation=rotation;g.transform.localScale=scale;
        }

        Mesh CreateAccessoryBox()
        {
            // Rounded, shared box mesh gives the arena machinery real edge highlights and
            // softer silhouettes while keeping the same colliders, placements and materials.
            const int segments=4;
            const float bevel=.12f;
            Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            Vector3[] axisU={
                new(0,0,-1),new(0,0,1),Vector3.right,Vector3.right,Vector3.right,Vector3.left
            };
            Vector3[] axisV={
                Vector3.up,Vector3.up,new(0,0,-1),new(0,0,1),Vector3.up,Vector3.up
            };
            int faceVertices=(segments+1)*(segments+1);
            Vector3[] vertices=new Vector3[faceVertices*6];
            Vector3[] vertexNormals=new Vector3[vertices.Length];
            Vector2[] uv=new Vector2[vertices.Length];
            int[] triangles=new int[segments*segments*6*6];
            int ti=0;
            for(int face=0;face<6;face++)
            {
                int offset=face*faceVertices;
                for(int y=0;y<=segments;y++)
                for(int x=0;x<=segments;x++)
                {
                    float u=x/(float)segments-.5f,v=y/(float)segments-.5f;
                    Vector3 p=normals[face]*.5f+axisU[face]*u+axisV[face]*v;
                    Vector3 core=new(
                        Mathf.Clamp(p.x,-.5f+bevel,.5f-bevel),
                        Mathf.Clamp(p.y,-.5f+bevel,.5f-bevel),
                        Mathf.Clamp(p.z,-.5f+bevel,.5f-bevel));
                    Vector3 outward=(p-core).normalized;
                    int index=offset+y*(segments+1)+x;
                    vertices[index]=core+outward*bevel;
                    vertexNormals[index]=outward;
                    uv[index]=new Vector2(x/(float)segments,y/(float)segments);
                }
                for(int y=0;y<segments;y++)
                for(int x=0;x<segments;x++)
                {
                    int a=offset+y*(segments+1)+x;
                    int b=a+1,c=a+segments+1,d=c+1;
                    triangles[ti++]=a;triangles[ti++]=b;triangles[ti++]=c;
                    triangles[ti++]=b;triangles[ti++]=d;triangles[ti++]=c;
                }
            }
            Mesh mesh=new(){name="DF_SoftBevelAccessoryBox",vertices=vertices,normals=vertexNormals,uv=uv,triangles=triangles};
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
