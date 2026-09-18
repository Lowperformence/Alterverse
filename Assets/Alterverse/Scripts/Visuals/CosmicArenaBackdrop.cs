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

        private void Start()
        {
            BuildVoidFloor();
            BuildStars();
            BuildDistantPlanets();
            BuildDistantMoons();
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
            stars=new GameObject("DF_StarField").transform;stars.SetParent(transform,false);
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

        private void BuildDistantPlanets()
        {
            distantBodies=new GameObject("DF_DistantBodies").transform;distantBodies.SetParent(transform,false);
            Planet(new Vector3(-96,-12,66),13f,new Color(.075f,.085f,.12f,1));
            Planet(new Vector3(112,-15,-64),18f,new Color(.055f,.07f,.105f,1));
            Planet(new Vector3(72,-13,92),6f,new Color(.085f,.09f,.115f,1));
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
            g.transform.position=p;g.transform.localScale=Vector3.one*size;
            Collider c=g.GetComponent<Collider>();if(c!=null)c.enabled=false;
            Renderer r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        private void Update()
        {
            if(stars!=null)stars.Rotate(0,.035f*Time.deltaTime,0,Space.World);
            if(distantBodies!=null)distantBodies.Rotate(0,.006f*Time.deltaTime,0,Space.World);
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
