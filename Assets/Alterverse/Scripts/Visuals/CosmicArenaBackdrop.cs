using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    // Lightweight procedural space backdrop visible beyond the arena edge.
    // Kept deliberately dark so gameplay silhouettes and faction colors remain dominant.
    public sealed class CosmicArenaBackdrop : MonoBehaviour
    {
        private PhotoArenaPanorama photoPanorama;
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Mesh> meshes=new List<Mesh>();
        private Transform stars;
        private Material starMaterial;
        private Transform distantBodies;
        private float nextBackdropUpdate;
        private Quaternion starsStartRotation;
        private Material planetRingMaterial;

        private void Start()
        {
            if(GetComponent<PlanetLandscapeDetail>()==null)gameObject.AddComponent<PlanetLandscapeDetail>();
            photoPanorama=new PhotoArenaPanorama();
            if(photoPanorama.Load())return;
            photoPanorama=null;
            BuildVoidFloor();
            BuildStars();
            BuildBrightStars();
            BuildFarStarLayer();
            BuildDenseStarLayer();
            BuildStarClusters();
            BuildDistantGalaxies();
            BuildDistantPlanets();
            BuildDistantMoons();
            BuildSmallDistantPlanets();
            BuildPlanetRings();
            BuildPlanetShadows();
            BuildEdgeFragments();
            ConfigureCameraBackdrop();
        }

        private Material Mat(Color color,bool unlit=true)
        {
            // These visuals exist only at runtime, so use the project-owned Resources shaders
            // instead of Shader.Find. This guarantees an Android build dependency and avoids
            // falling back to Built-in Standard/Unlit shaders under URP (magenta materials).
            Material m=RuntimeShaderLibrary.Create(color,unlit,unlit?0f:.02f,unlit?0f:.18f);
            if(m==null)
            {
                Debug.LogError("DIMENSIONS FRONT: failed to create build-safe cosmic backdrop material.");
                return null;
            }
            m.name=unlit?"DF_CosmicRuntimeUnlit":"DF_CosmicRuntimeLit";
            materials.Add(m);
            return m;
        }

        private void BuildVoidFloor()
        {
            Material m=Mat(new Color(.004f,.006f,.014f,1));
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name="DF_CosmicVoid";
            g.transform.SetParent(transform,false);g.transform.position=new Vector3(0,-48f,0);
            g.transform.rotation=Quaternion.Euler(90,0,0);g.transform.localScale=new Vector3(260,180,1);
            Collider c=g.GetComponent<Collider>();if(c!=null){c.enabled=false;Object.Destroy(c);}
            Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        private void BuildStars()
        {
            Material m=Mat(new Color(.78f,.84f,.94f,1));
            if(m==null)return;starMaterial=m;
            stars=new GameObject("DF_StarField").transform;stars.SetParent(transform,false);starsStartRotation=stars.rotation;
            const int count=440;var vertices=new Vector3[count];var indices=new int[count];
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

        private void BuildDenseStarLayer()
        {
            Material m=Mat(new Color(.29f,.34f,.44f,1));if(m==null)return;
            const int count=1120;var vertices=new Vector3[count];var indices=new int[count];
            for(int i=0;i<count;i++)
            {
                float a=i*2.173821f+.31f;float radius=58f+(i*47%154);
                float x=Mathf.Cos(a)*radius,z=Mathf.Sin(a)*radius*.74f;
                if(Mathf.Abs(x)<48f&&Mathf.Abs(z)<41f)
                {
                    radius+=48f;x=Mathf.Cos(a)*radius;z=Mathf.Sin(a)*radius*.74f;
                }
                vertices[i]=new Vector3(x,-2.76f+(i%7)*.006f,z);indices[i]=i;
            }
            Mesh mesh=new Mesh{name="DF_DenseStars"};mesh.vertices=vertices;mesh.SetIndices(indices,MeshTopology.Points,0);mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject g=new GameObject("DF_DenseStars");g.transform.SetParent(stars,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer r=g.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.sortingOrder=-102;
        }

        private void BuildStarClusters()
        {
            Material m=Mat(new Color(.55f,.59f,.68f,1));if(m==null)return;
            Vector3[] centers={new(-118,-2.6f,42),new(126,-2.6f,18),new(-58,-2.6f,-102)};
            const int per=18;var vertices=new Vector3[centers.Length*per];var indices=new int[vertices.Length];int k=0;
            for(int c=0;c<centers.Length;c++)for(int i=0;i<per;i++,k++)
            {
                float a=i*2.18f+c*.7f,radius=2.2f+(i%6)*.9f;vertices[k]=centers[c]+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);indices[k]=k;
            }
            Mesh mesh=new Mesh{name="DF_StarClusters"};mesh.vertices=vertices;mesh.SetIndices(indices,MeshTopology.Points,0);mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject g=new GameObject("DF_StarClusters");g.transform.SetParent(stars,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer r=g.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.sortingOrder=-100;
        }

        private void BuildFarStarLayer()
        {
            Material m=Mat(new Color(.38f,.42f,.5f,1));if(m==null)return;
            const int count=240;var vertices=new Vector3[count];var indices=new int[count];
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
            const int count=64;var vertices=new Vector3[count];var indices=new int[count];
            for(int i=0;i<count;i++)
            {
                float a=(i*4.236f+.7f);float radius=63f+(i*29%103);
                vertices[i]=new Vector3(Mathf.Cos(a)*radius,-2.58f,Mathf.Sin(a)*radius*.72f);indices[i]=i;
            }
            Mesh mesh=new Mesh{name="DF_BrightStars"};mesh.vertices=vertices;mesh.SetIndices(indices,MeshTopology.Points,0);mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject g=new GameObject("DF_BrightStars");g.transform.SetParent(stars,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer r=g.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.sortingOrder=-99;
        }

        private void BuildDistantGalaxies()
        {
            BuildGalaxy(new Vector3(-142,-2.79f,-88),18f,.43f,.15f,new Color(.42f,.32f,.58f,1),17);
            BuildGalaxy(new Vector3(148,-2.80f,70),16f,.48f,1.05f,new Color(.25f,.48f,.60f,1),29);
            BuildGalaxy(new Vector3(-18,-2.81f,164),20f,.38f,2.10f,new Color(.39f,.43f,.66f,1),43);
        }

        private void BuildGalaxy(Vector3 center,float radius,float flatten,float rotation,Color color,int seed)
        {
            Material m=Mat(color);if(m==null)return;
            const int armCount=3;
            const int armPoints=150;
            const int corePoints=42;
            var vertices=new Vector3[armPoints+corePoints];var indices=new int[vertices.Length];

            for(int i=0;i<armPoints;i++)
            {
                int arm=i%armCount;int step=i/armCount;
                float t=step/49f;
                float jitter=((i*seed)%19-9)*.018f;
                float a=rotation+arm*Mathf.PI*2f/armCount+t*Mathf.PI*2.75f+jitter;
                float r=1.1f+t*radius+((i*(seed+7))%13)*.035f;
                vertices[i]=center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*flatten);indices[i]=i;
            }

            for(int i=0;i<corePoints;i++)
            {
                float a=rotation+i*2.39996323f;
                float r=Mathf.Sqrt((i+.5f)/corePoints)*radius*.24f;
                int index=armPoints+i;
                vertices[index]=center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*flatten*.9f);indices[index]=index;
            }

            Mesh mesh=new Mesh{name="DF_DistantGalaxy"};mesh.vertices=vertices;mesh.SetIndices(indices,MeshTopology.Points,0);mesh.RecalculateBounds();meshes.Add(mesh);
            GameObject g=new GameObject("DF_DistantGalaxy");g.transform.SetParent(stars,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=m;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortingOrder=-103;
        }

        private void BuildDistantPlanets()
        {
            distantBodies=new GameObject("DF_DistantBodies").transform;distantBodies.SetParent(transform,false);
            Planet(new Vector3(-70,-17,49),17f,new Color(.08f,.06f,.115f,1));
            Planet(new Vector3(76,-21,-51),21f,new Color(.055f,.09f,.12f,1));
            Planet(new Vector3(72,-13,92),6f,new Color(.085f,.09f,.115f,1));
        }

        private void ConfigureCameraBackdrop()
        {
            Camera[] cameras=FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            Color voidColor=new Color(.004f,.006f,.014f,1);
            foreach(Camera camera in cameras)
            {
                if(camera==null||camera.cameraType!=CameraType.Game)continue;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=voidColor;
            }
        }

        private void BuildEdgeFragments()
        {
            Material m=Mat(new Color(.10f,.115f,.135f,1),false);if(m==null)return;
            Mesh fragment=LunarFragmentGeometry.Create(43);meshes.Add(fragment);
            Vector3[] sites={new(-52,-2.4f,28),new(-48,-3.2f,-35),new(-16,-3.5f,43),new(18,-2.9f,-45),new(50,-3.1f,30),new(53,-2.6f,-25)};
            for(int i=0;i<sites.Length;i++)
            {
                GameObject g=new GameObject("DF_FloatingMoonFragment");g.AddComponent<MeshFilter>().sharedMesh=fragment;g.AddComponent<MeshRenderer>();
                g.transform.SetParent(transform,false);g.transform.position=sites[i];float scale=1.7f+(i%3)*.85f;
                g.transform.localScale=new Vector3(scale*1.4f,scale*.7f,scale);g.transform.rotation=Quaternion.Euler(i*17,31+i*49,12+i*9);
                Collider c=g.GetComponent<Collider>();if(c!=null){c.enabled=false;Object.Destroy(c);}
                Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }

        private void BuildPlanetShadows()
        {
            if(distantBodies==null)return;Material m=Mat(new Color(.008f,.01f,.017f,1),true);if(m==null)return;
            ShadowDisc(new Vector3(-75,-22,44),13.5f,m);ShadowDisc(new Vector3(69,-27,-57),16.5f,m);ShadowDisc(new Vector3(70,-15,90),4.6f,m);
        }

        private void ShadowDisc(Vector3 p,float size,Material m)
        {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="DF_PlanetNightSide";g.transform.SetParent(distantBodies,false);g.transform.position=p;g.transform.localScale=Vector3.one*size;
            Collider c=g.GetComponent<Collider>();if(c!=null){c.enabled=false;Object.Destroy(c);}Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        private void BuildPlanetRings()
        {
            Material zero=Mat(new Color(.115f,.055f,.17f,1),true),lumen=Mat(new Color(.045f,.135f,.17f,1),true);if(zero==null||lumen==null||distantBodies==null)return;
            Ring(new Vector3(-70,-17,49),20f,17.8f,zero,18f);
            Ring(new Vector3(76,-21,-51),27f,22.5f,lumen,-12f);
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

        private void BuildSmallDistantPlanets()
        {
            // Extra background bodies only: the original planets above remain untouched.
            Planet(new Vector3(-132,-12,96),2.4f,new Color(.075f,.11f,.16f,1));
            Planet(new Vector3(136,-16,118),3.1f,new Color(.12f,.075f,.145f,1));
            Planet(new Vector3(-154,-18,-74),2.0f,new Color(.11f,.095f,.07f,1));
            Planet(new Vector3(148,-11,-108),2.7f,new Color(.055f,.12f,.13f,1));
            Planet(new Vector3(-34,-9,172),1.8f,new Color(.105f,.11f,.16f,1));
            Planet(new Vector3(42,-14,-166),2.3f,new Color(.13f,.07f,.09f,1));
        }

        private void Planet(Vector3 p,float size,Color color)
        {
            Material m=Mat(color,false);if(m==null)return;
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="DF_DistantPlanet";g.transform.SetParent(distantBodies!=null?distantBodies:transform,false);
            g.transform.position=p;g.transform.localScale=Vector3.one*size;g.isStatic=true;
            Collider c=g.GetComponent<Collider>();if(c!=null){c.enabled=false;Object.Destroy(c);}
            Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        private void Update()
        {
            if(photoPanorama!=null){photoPanorama.RefreshCamera();return;}
            if(Time.time<nextBackdropUpdate)return;nextBackdropUpdate=Time.time+.1f;
            if(stars!=null){float drift=Mathf.Sin(Time.time*.018f)*.35f;stars.rotation=starsStartRotation*Quaternion.Euler(0,drift,0);}
            if(distantBodies!=null)distantBodies.rotation=Quaternion.Euler(0,Mathf.Sin(Time.time*.006f)*.12f,0);
            if(starMaterial!=null)
            {
                float pulse=.82f+Mathf.Sin(Time.time*.23f)*.05f;
                starMaterial.color=new Color(.78f*pulse,.84f*pulse,.94f*pulse,1);
            }
            if(planetRingMaterial!=null){float shade=.92f+Mathf.Sin(Time.time*.035f)*.025f;planetRingMaterial.color=new Color(.12f*shade,.13f*shade,.16f*shade,1);}
        }

        private void OnDisable(){photoPanorama?.RestoreCamera();}

        private void OnDestroy()
        {
            photoPanorama?.Dispose();
            foreach(Material m in materials)if(m!=null)Destroy(m);
            foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);
        }
    }
}
