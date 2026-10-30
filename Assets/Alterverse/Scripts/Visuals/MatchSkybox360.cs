using System;
using System.Collections.Generic;
using Alterverse.CameraSystem;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Alterverse.Visuals
{
    /// <summary>Automatically loaded client-only sky and distant geometry for regular matches.</summary>
    public sealed class MatchSkybox360 : IDisposable
    {
        public const string ResourceRoot="MatchSkybox360/Generated/";
#if !UNITY_SERVER
        private static readonly string[] PlanetNames={"Azure","Copper","Ice","Violet","Moon"};
        private static readonly Vector3[] Centers={new(-205,-118,650),new(200,85,750),new(-100,170,800),new(130,-200,720),new(30,120,550)};
        private static readonly float[] Radii={86,60,44,62,21};
        private readonly List<Object> owned=new();
        private readonly List<Skybox> addedSkyboxes=new();
        private Transform owner;
        private GameObject root;
        private Camera camera;
        private Skybox skybox;
        private Material skyMaterial,previousMaterial,ringTemplate;
        private readonly Material[] planetTemplates=new Material[5];
        private CameraClearFlags previousFlags;
        private bool previousEnabled,geometryBuilt,reportedFailure;

        public bool Load(Transform matchOwner)
        {
            Dispose();
            if(Application.isBatchMode||matchOwner==null)return false;
            owner=matchOwner;
            try
            {
                Material sky=RequireMaterial("Sky");
                if(sky.shader.name!="Skybox/Panoramic"||sky.passCount!=1)throw new InvalidOperationException("Skybox must use the single-pass Unity panoramic shader.");
                ringTemplate=RequireMaterial("Rings");
                for(int i=0;i<PlanetNames.Length;i++)planetTemplates[i]=RequireMaterial("Planet_"+PlanetNames[i]);
                skyMaterial=Own(new Material(sky){name="DF_MatchSkybox360"});
                // Each arena looks into a different part of the same continuous 360-degree universe.
                skyMaterial.SetFloat("_Rotation",((int)ArenaVariantRuntime.Current*29.5f)%360f);
                RefreshCamera();
                return true;
            }
            catch(Exception error)
            {
                Dispose();
                Debug.LogWarning("DIMENSIONS FRONT: 360 sky unavailable, keeping the previous backdrop. "+error.Message);
                return false;
            }
        }

        private static Material RequireMaterial(string name)
        {
            var material=Resources.Load<Material>(ResourceRoot+name);
            if(material==null||material.shader==null||!material.shader.isSupported)throw new InvalidOperationException("Missing/unsupported sky material: "+name);
            return material;
        }

        private T Own<T>(T value) where T : Object
        {
            value.hideFlags=HideFlags.DontSave;owned.Add(value);return value;
        }

        public void RefreshCamera()
        {
            if(skyMaterial==null||owner==null)return;
            Camera current=Camera.main;
            if(current==camera&&skybox!=null)
            {
                // Do not fight a transition curtain or a later menu/camera owner.
                if(root!=null)root.SetActive(skybox.material==skyMaterial&&skybox.enabled&&camera.enabled&&camera.targetTexture==null&&camera.clearFlags==CameraClearFlags.Skybox);
                return;
            }
            RestoreCamera();
            if(current==null||current.targetTexture!=null||current.GetComponent<MobaCamera>()==null)return;
            camera=current;previousFlags=camera.clearFlags;
            skybox=camera.GetComponent<Skybox>();
            if(skybox==null)
            {
                skybox=camera.gameObject.AddComponent<Skybox>();addedSkyboxes.Add(skybox);
                // A previously absent component is restored as an inactive, empty override.
                previousMaterial=null;previousEnabled=false;
            }
            else {previousMaterial=skybox.material;previousEnabled=skybox.enabled;}
            skybox.material=skyMaterial;skybox.enabled=true;camera.clearFlags=CameraClearFlags.Skybox;
            try
            {
                if(!geometryBuilt)BuildPlanets();
                if(root!=null)
                {
                    root.transform.rotation=camera.transform.rotation;
                    root.transform.localScale=Vector3.one*Mathf.Min(1f,camera.farClipPlane/1100f);
                    root.SetActive(!camera.orthographic&&camera.farClipPlane>=400);
                }
            }
            catch(Exception error)
            {
                if(root!=null){root.SetActive(false);Object.Destroy(root);root=null;}
                // Keep the already-valid sky, release a partial planet build, and avoid retrying per camera.
                for(int i=owned.Count-1;i>=0;i--)
                    if(owned[i]!=skyMaterial){if(owned[i]!=null)Object.Destroy(owned[i]);owned.RemoveAt(i);}
                geometryBuilt=true;
                if(!reportedFailure){reportedFailure=true;Debug.LogWarning("DIMENSIONS FRONT: distant planets could not be prepared; 360-degree star sky remains available. "+error.Message);}
            }
        }

        private void BuildPlanets()
        {
            // Geometry is genuinely behind the world and uses normal depth testing.
            // No fullscreen overlay, blit, extra camera, collider, light or global lighting change.
            root=new GameObject("DF_Skybox360");root.transform.SetParent(owner,false);
            root.transform.position=owner.position;
            Mesh sphere=Sphere(64,32);
            for(int i=0;i<PlanetNames.Length;i++)
            {
                Material material=Own(new Material(planetTemplates[i]){name="DF_Skybox360_"+PlanetNames[i]});
                Part("Planet_"+PlanetNames[i],sphere,material,Centers[i],Vector3.one*Radii[i]);
            }
            AddRings();geometryBuilt=true;
        }

        private GameObject Part(string name,Mesh mesh,Material material,Vector3 center,Vector3 scale)
        {
            var part=new GameObject(name);part.transform.SetParent(root.transform,false);
            part.transform.localPosition=center;part.transform.localScale=scale;
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return part;
        }

        private static void SetTexture(Material material,Texture2D texture)
        {
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
            if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",texture);
        }

        public void RestoreCamera()
        {
            if(root!=null)root.SetActive(false);
            if(skybox!=null&&skybox.material==skyMaterial)
            {
                skybox.material=previousMaterial;skybox.enabled=previousEnabled;
                if(camera!=null&&camera.clearFlags==CameraClearFlags.Skybox)camera.clearFlags=previousFlags;
            }
            // Keep added inactive components until disposal: Destroy is deferred in player builds.
            camera=null;skybox=null;previousMaterial=null;
        }

        public void Dispose()
        {
            RestoreCamera();
            if(root!=null)Object.Destroy(root);root=null;
            for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.Destroy(owned[i]);
            owned.Clear();
            foreach(var added in addedSkyboxes)if(added!=null&&!added.enabled&&added.material==null)Object.Destroy(added);
            addedSkyboxes.Clear();
            for(int i=0;i<planetTemplates.Length;i++)planetTemplates[i]=null;
            skyMaterial=null;ringTemplate=null;owner=null;geometryBuilt=false;reportedFailure=false;
        }
        private Mesh Sphere(int width,int height)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var faces=new List<int>();
            for(int y=0;y<=height;y++)for(int x=0;x<=width;x++)
            {
                float u=x/(float)width,v=y/(float)height,lon=(u-.5f)*Mathf.PI*2,lat=(v-.5f)*Mathf.PI;
                Vector3 n=new(Mathf.Sin(lon)*Mathf.Cos(lat),Mathf.Sin(lat),Mathf.Cos(lon)*Mathf.Cos(lat));
                vertices.Add(n);normals.Add(n);uv.Add(new Vector2(u,v));
            }
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int a=y*(width+1)+x,b=a+width+1;
                if(y>0)faces.AddRange(new[]{a,a+1,b});
                if(y<height-1)faces.AddRange(new[]{a+1,b+1,b});
            }
            var mesh=Own(new Mesh{name="DF_Skybox360PlanetSphere"});mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(faces,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }

        private void AddRings()
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var faces=new List<int>();
            var palette=Own(new Texture2D(1,10,TextureFormat.RGBA32,false){name="DF_Skybox360RingPalette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp});
            for(int band=0;band<10;band++)
            {
                float inner=(1.34f+band*.061f)*Radii[0],outer=inner+.061f*.79f*Radii[0];
                palette.SetPixel(0,band,Color.Lerp(new Color(.11f,.13f,.20f),new Color(.40f,.40f,.48f),(band*37%11)/11f).gamma);
                for(int i=0;i<128;i++)
                {
                    float a=i*Mathf.PI*2/128,b=(i+1)*Mathf.PI*2/128;int start=vertices.Count;
                    vertices.Add(new Vector3(Mathf.Cos(a)*inner,0,Mathf.Sin(a)*inner));vertices.Add(new Vector3(Mathf.Cos(a)*outer,0,Mathf.Sin(a)*outer));
                    vertices.Add(new Vector3(Mathf.Cos(b)*outer,0,Mathf.Sin(b)*outer));vertices.Add(new Vector3(Mathf.Cos(b)*inner,0,Mathf.Sin(b)*inner));
                    for(int j=0;j<4;j++)uv.Add(new Vector2(.5f,(band+.5f)/10));
                    faces.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
                }
            }
            palette.Apply(false,false);
            var material=Own(new Material(ringTemplate){name="DF_Skybox360Rings"});SetTexture(material,palette);material.SetInt("_Cull",0);
            var mesh=Own(new Mesh{name="DF_Skybox360PlanetRings"});mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var ring=Part("Azure_Rings",mesh,material,Centers[0],Vector3.one);
            ring.transform.localRotation=Quaternion.FromToRotation(Vector3.up,new Vector3(.14f,.88f,-.45f).normalized);
        }
#else
        public bool Load(Transform owner)=>false;
        public void RefreshCamera() { }
        public void RestoreCamera() { }
        public void Dispose() { }
#endif
    }
}
