using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    // Lightweight procedural space backdrop visible beyond the arena edge.
    // Kept deliberately dark so gameplay silhouettes and faction colors remain dominant.
    public sealed class CosmicArenaBackdrop : MonoBehaviour
    {
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Mesh> meshes=new List<Mesh>();
        private Transform stars;
        private Material starMaterial;
        private Transform distantBodies;
        private float nextBackdropUpdate;
        private Quaternion starsStartRotation;

        private void Start()
        {
            BuildVoidFloor();
            BuildStars();
            BuildBrightStars();
            BuildFarStarLayer();
            BuildDistantPlanets();
            BuildDistantMoons();
            BuildPlanetRings();
            BuildArenaUnderside();
            BuildEdgeFragments();
            BuildUndersideFissures();
            BuildRimDropShadows();
            BuildEdgeFalloff();
            ConfigureCameraBackdrop();
        }

        private Material Mat(Color color,bool unlit=true)
        {
            Shader shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find(unlit?"Unlit/Color":"Standard");
            if(shader==null)return null;
            Material m=new Material(shader){color=color};
            materials.Add(m);return m;
        }

        private void BuildVoidFloor()
        {
            Material m=Mat(new Color(.004f,.006f,.014f,1));
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name="DF_CosmicVoid";
            g.transform.SetParent(transform,false);g.transform.position=new Vector3(0,-2.8f,0);
            g.transform.rotation=Quaternion.Euler(90,0,0);g.transform.localScale=new Vector3(260,180,1);
            Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
            Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        private void BuildStars()
        {
            Material m=Mat(new Color(.78f,.84f,.94f,1));
            if(m==null)return;starMaterial=m;
            stars=new GameObject("DF_StarField").transform;stars.SetParent(transform,false);starsStartRotation=stars.rotation;
            const int count=220;var vertices=new Vector3[count];var indices=new int[count];
            for(int i=0;i<count;i++)
            {
                float a=i*2.39996323f;float radius=52f+(i*37%125);
                float x=Mathf.Cos(a)*radius,z=Mathf.Sin(a)*radius*.72f;
                if(Mathf.Abs(x)<49f&&Mathf.Abs(z)<42f){radius+=55f;x=Mathf.Cos(a)*radius;z=Mathf.Sin(a)*radius*.72f;}
                vertices[i]=new Vector3(x,-2.65f+(i%5)*.015f,z);indices[i]=i;
            }
            Mesh mesh=new Mesh{name="DF_Stars"};mesh.vertices=vertices;mesh.SetIndices(indices,MeshTopology.Points,0);mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject g=new GameObject("DF_Stars");g.transform.SetParent(stars,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer r=g.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            r.sortingOrder=-100;
        }



        private void BuildFarStarLayer()
        {
            Material m=Mat(new Color(.38f,.42f,.5f,1));if(m==null)return;
            const int count=120;var vertices=new Vector3[count];var indices=new int[count];
            for(int i=0;i<count;i++)
            {
                float a=i*1.6180339f+.35f;float radius=110f+(i*43%105);
                vertices[i]=new Vector3(Mathf.Cos(a)*radius,-2.72f,Mathf.Sin(a)*radius*.7f);indices[i]=i;
            }
            Mesh mesh=new Mesh{name="DF_FarStars"};mesh.vertices=vertices;mesh.SetIndices(indices,MeshTopology.Points,0);mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject g=new GameObject("DF_FarStars");g.transform.SetParent(stars,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer r=g.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.sortingOrder=-101;
        }

        private void BuildBrightStars()
        {
            Material m=Mat(new Color(.96f,.97f,1f,1));if(m==null)return;
            const int count=32;var vertices=new Vector3[count];var indices=new int[count];
            for(int i=0;i<count;i++)
            {
                float a=(i*4.236f+.7f);float radius=63f+(i*29%103);
                vertices[i]=new Vector3(Mathf.Cos(a)*radius,-2.58f,Mathf.Sin(a)*radius*.72f);indices[i]=i;
            }
            Mesh mesh=new Mesh{name="DF_BrightStars"};mesh.vertices=vertices;mesh.SetIndices(indices,MeshTopology.Points,0);mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject g=new GameObject("DF_BrightStars");g.transform.SetParent(stars,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer r=g.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.sortingOrder=-99;
        }

        private void BuildDistantPlanets()
        {
            distantBodies=new GameObject("DF_DistantBodies").transform;distantBodies.SetParent(transform,false);
            Planet(new Vector3(-96,-12,66),13f,new Color(.075f,.085f,.12f,1));
            Planet(new Vector3(112,-15,-64),18f,new Color(.055f,.07f,.105f,1));
            Planet(new Vector3(72,-13,92),6f,new Color(.085f,.09f,.115f,1));
        }






        private void BuildEdgeFalloff()
        {
            Material m=Mat(new Color(.018f,.022f,.032f,1),true);if(m==null)return;
            Vector3[] p={new(-47,-1.65f,0),new(47,-1.65f,0),new(0,-1.65f,40),new(0,-1.65f,-40)};
            Vector3[] scale={new(5,1.2f,72),new(5,1.2f,72),new(84,1.2f,4),new(84,1.2f,4)};
            for(int i=0;i<p.Length;i++)
            {
                GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_ArenaEdgeFalloff";g.transform.SetParent(transform,false);g.transform.position=p[i];g.transform.localScale=scale[i];
                Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }

        private void BuildRimDropShadows()
        {
            Material m=Mat(new Color(.012f,.015f,.024f,1),true);if(m==null)return;
            Vector3[] centers={new(-45,-1.25f,0),new(45,-1.25f,0),new(0,-1.25f,38.5f),new(0,-1.25f,-38.5f)};
            Vector3[] scales={new(8,1,70),new(8,1,70),new(82,1,7),new(82,1,7)};
            for(int i=0;i<centers.Length;i++)
            {
                GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_RimDepth";
                g.transform.SetParent(transform,false);g.transform.position=centers[i];g.transform.localScale=scales[i];
                Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
                Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }

        private void ConfigureCameraBackdrop()
        {
            Camera[] cameras=FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Color voidColor=new Color(.004f,.006f,.014f,1);
            foreach(Camera camera in cameras)
            {
                if(camera==null||camera.cameraType!=CameraType.Game)continue;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=voidColor;
            }
        }


        private void BuildUndersideFissures()
        {
            Material zero=Mat(Color.Lerp(StylizedArtDirection.ZeroEnergy,new Color(.02f,.02f,.035f,1),.72f),true);
            Material lumen=Mat(Color.Lerp(StylizedArtDirection.LumenEnergy,new Color(.02f,.025f,.035f,1),.76f),true);
            if(zero==null||lumen==null)return;
            for(int side=-1;side<=1;side+=2)
            for(int i=0;i<4;i++)
            {
                float z=-25f+i*16.5f;GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=side<0?"DF_ZeroUndersideFissure":"DF_LumenUndersideFissure";
                g.transform.SetParent(transform,false);g.transform.position=new Vector3(side*43.2f,-2.15f,z);g.transform.localScale=new Vector3(.08f,.12f,2.2f+(i%2)*.7f);
                g.transform.rotation=Quaternion.Euler(0,side*(8+i*5),side*(12+i*3));Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
                Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=side<0?zero:lumen;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }

        private void BuildEdgeFragments()
        {
            Material m=Mat(new Color(.04f,.045f,.06f,1),false);if(m==null)return;
            Vector3[] sites={new(-52,-2.4f,28),new(-48,-3.2f,-35),new(-16,-3.5f,43),new(18,-2.9f,-45),new(50,-3.1f,30),new(53,-2.6f,-25)};
            for(int i=0;i<sites.Length;i++)
            {
                GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_FloatingEdgeFragment";
                g.transform.SetParent(transform,false);g.transform.position=sites[i];float scale=.8f+(i%3)*.45f;
                g.transform.localScale=new Vector3(scale*1.4f,scale*.7f,scale);g.transform.rotation=Quaternion.Euler(i*17,31+i*49,12+i*9);
                Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
                Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }

        private void BuildArenaUnderside()
        {
            Material m=Mat(new Color(.025f,.03f,.045f,1),false);if(m==null)return;
            Vector3[] sites={new(-38,-3.5f,-28),new(-20,-4.4f,33),new(4,-5f,-35),new(28,-4.1f,31),new(40,-3.8f,-22)};
            for(int i=0;i<sites.Length;i++)
            {
                GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="DF_UndersideRock";
                g.transform.SetParent(transform,false);g.transform.position=sites[i];g.transform.localScale=new Vector3(7f+(i%3)*2f,4.5f+(i%2)*2f,6f+(i%4));g.isStatic=true;
                g.transform.rotation=Quaternion.Euler(18+i*7,i*43,11-i*4);
                Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
                Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }


        private void BuildPlanetRings()
        {
            Material m=Mat(new Color(.12f,.13f,.16f,1),true);if(m==null||distantBodies==null)return;
            Ring(new Vector3(-96,-12,66),18f,13.8f,m,18f);
            Ring(new Vector3(112,-15,-64),25f,19.5f,m,-12f);
        }

        private void Ring(Vector3 center,float outer,float inner,Material material,float tilt)
        {
            const int segments=40;var vertices=new Vector3[segments*2];var triangles=new int[segments*6];
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2f/segments;float ca=Mathf.Cos(a),sa=Mathf.Sin(a);
                vertices[i*2]=new Vector3(ca*inner,0,sa*inner);
                vertices[i*2+1]=new Vector3(ca*outer,0,sa*outer);
                int n=(i+1)%segments,ti=i*6;triangles[ti]=i*2;triangles[ti+1]=n*2;triangles[ti+2]=i*2+1;triangles[ti+3]=i*2+1;triangles[ti+4]=n*2;triangles[ti+5]=n*2+1;
            }
            Mesh mesh=new Mesh{name="DF_PlanetRing"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject g=new GameObject("DF_DistantPlanetRing");g.transform.SetParent(distantBodies,false);g.transform.position=center;g.transform.rotation=Quaternion.Euler(tilt,0,8);
            g.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer r=g.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        private void BuildDistantMoons()
        {
            Planet(new Vector3(-82,-10,83),3.2f,new Color(.13f,.14f,.17f,1));
            Planet(new Vector3(96,-13,-86),4.1f,new Color(.10f,.115f,.14f,1));
        }

        private void Planet(Vector3 p,float size,Color color)
        {
            Material m=Mat(color,false);if(m==null)return;
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="DF_DistantPlanet";g.transform.SetParent(distantBodies!=null?distantBodies:transform,false);
            g.transform.position=p;g.transform.localScale=Vector3.one*size;g.isStatic=true;
            Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
            Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        private void Update()
        {
            if(Time.time<nextBackdropUpdate)return;float dt=Mathf.Max(.05f,Time.time-(nextBackdropUpdate-.1f));nextBackdropUpdate=Time.time+.1f;
            if(stars!=null){float drift=Mathf.Sin(Time.time*.018f)*.35f;stars.rotation=starsStartRotation*Quaternion.Euler(0,drift,0);}
            if(distantBodies!=null)distantBodies.Rotate(0,.006f*dt,0,Space.World);
            if(starMaterial!=null)
            {
                float pulse=.82f+Mathf.Sin(Time.time*.23f)*.05f;
                starMaterial.color=new Color(.78f*pulse,.84f*pulse,.94f*pulse,1);
            }
        }

        private void OnDestroy()
        {
            foreach(Material m in materials)if(m!=null)Destroy(m);
            foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);
        }
    }
}
