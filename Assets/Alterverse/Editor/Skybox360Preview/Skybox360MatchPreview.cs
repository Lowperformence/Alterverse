#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Alterverse.CameraSystem;
using Alterverse.Multiplayer;
using Alterverse.UI;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Camera = UnityEngine.Camera;
using Object = UnityEngine.Object;

namespace Alterverse.EditorTools
{
    /// <summary>Explicit, temporary offline preview. No runtime/bootstrap or saved scene changes.</summary>
    public sealed class Skybox360MatchPreview : EditorWindow
    {
        private Skybox360PreviewSession session;
        private string status;

        [MenuItem("Tools/Dimensions Front/Skybox 360/Offline-Match testen")]
        public static void Open()
        {
            var window=GetWindow<Skybox360MatchPreview>();
            window.titleContent=new GUIContent("Skybox 360 Test");
            window.minSize=new Vector2(390,250);
            window.Show();
        }

        private void OnEnable()
        {
            // Opening/reloading the window never starts a preview automatically.
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload+=Stop;
        }

        private void OnDisable()
        {
            Stop();
            EditorApplication.update-=Tick;
            EditorApplication.playModeStateChanged-=OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload-=Stop;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("360°-Himmel mit echten 3D-Planeten",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Zuerst ein Offline-Match im Play-Modus starten. Der Test gilt nur für dieses Match im Unity-Editor. Fenster schließen oder Play beenden stellt den bisherigen Hintergrund wieder her.",MessageType.Info);
            bool ready=TryGetMatch(out _,out _,out string reason);
            if(session==null)
            {
                using(new EditorGUI.DisabledScope(!ready))
                    if(GUILayout.Button("Neue 3D-Kulisse testen"))StartPreview();
                if(!ready)EditorGUILayout.HelpBox(reason,MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("Test aktiv: 5 Planeten, 1 Ring, Standard-Skybox");
                if(GUILayout.Button("Originalhintergrund wiederherstellen"))Stop();
            }
            if(!string.IsNullOrEmpty(status))EditorGUILayout.HelpBox(status,MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dieser Editor-Test wird nicht mit der AAB ausgeliefert.",EditorStyles.wordWrappedLabel);
        }

        private void StartPreview()
        {
            Stop();
            if(!TryGetMatch(out Camera camera,out GameObject world,out status))return;
            if(!Skybox360PreviewSession.TryCreate(camera,world,out session,out status))
                Debug.LogWarning("DIMENSIONS FRONT: Skybox-360-Test: "+status);
            Repaint();
        }

        private void Tick()
        {
            if(session==null)return;
            if(!TryGetMatch(out Camera camera,out GameObject world,out _)||!session.IsCurrent(camera,world))
            {
                Stop();
                status="Test beendet; der ursprüngliche Kamera-Zustand wurde freigegeben.";
                Repaint();
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingPlayMode||state==PlayModeStateChange.ExitingEditMode)Stop();
        }

        private void Stop()
        {
            session?.Dispose();session=null;
            status=null;
            Repaint();
        }

        private static bool TryGetMatch(out Camera camera,out GameObject world,out string reason)
        {
            camera=null;world=null;reason="Bitte zuerst ein Offline-Match im Play-Modus starten.";
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||MainMenuController.IsVisible)return false;
            if(CoopAuthority.Active||(NetworkManager.Singleton!=null&&NetworkManager.Singleton.IsListening))
            {reason="Der Vorschautest ist auf Offline-Matches begrenzt.";return false;}
            camera=Camera.main;world=GameObject.Find("DF_MatchWorld");
            if(world==null||!world.activeInHierarchy||GameObject.Find("AV_Player")==null||camera==null||!camera.enabled||camera.targetTexture!=null||camera.GetComponent<MobaCamera>()==null||camera.cullingMask==0)return false;
            if(camera.orthographic){reason="Die Vorschau benötigt die perspektivische Match-Kamera.";return false;}
            if(camera.farClipPlane<400){reason="Die Sichtweite dieser Kamera ist für die Vorschau zu klein. Die Kamera bleibt unverändert.";return false;}
            reason=null;return true;
        }
    }

    /// <summary>Owns only temporary objects and the local Skybox override; never touches RenderSettings.</summary>
    internal sealed class Skybox360PreviewSession : IDisposable
    {
        internal const string TextureRoot="Assets/Alterverse/Editor/Skybox360Preview/Textures/";
        private static readonly string[] PlanetNames={"Azure","Copper","Ice","Violet","Moon"};
        private static readonly Vector3[] Centers={new(-205,-118,650),new(200,85,750),new(-100,170,800),new(130,-200,720),new(30,120,550)};
        private static readonly float[] Radii={86,60,44,62,21};
        private readonly List<Object> owned=new();
        private Camera camera;
        private GameObject world,root;
        private Skybox skybox;
        private Material skyMaterial,previousMaterial;
        private CameraClearFlags previousFlags;
        private bool addedSkybox,previousEnabled;

        internal static bool TryCreate(Camera camera,GameObject world,out Skybox360PreviewSession result,out string error)
        {
            result=null;error=null;
            var candidate=new Skybox360PreviewSession();
            try {candidate.Build(camera,world);result=candidate;return true;}
            catch(Exception exception){candidate.Dispose();error=exception.Message;return false;}
        }

        private T Own<T>(T value) where T : Object
        {
            value.hideFlags=HideFlags.HideAndDontSave;owned.Add(value);return value;
        }

        private void Build(Camera target,GameObject matchWorld)
        {
            if(target==null||matchWorld==null)throw new InvalidOperationException("Match-Kamera oder Match fehlt.");
            Shader skyShader=Shader.Find("Skybox/Panoramic");
            bool urp=GraphicsSettings.currentRenderPipeline!=null;
            Shader surface=Shader.Find(urp?"Universal Render Pipeline/Lit":"Standard");
            Shader unlit=Shader.Find(urp?"Universal Render Pipeline/Unlit":"Unlit/Texture");
            if(skyShader==null||!skyShader.isSupported||surface==null||!surface.isSupported||unlit==null||!unlit.isSupported)
                throw new InvalidOperationException("Ein unterstützter Unity-Standard-Shader fehlt. Der bisherige Hintergrund bleibt aktiv.");
            var panorama=Load("Sky360_StarsNebula.png");
            var planetTextures=new Texture2D[PlanetNames.Length];
            for(int i=0;i<PlanetNames.Length;i++)planetTextures[i]=Load("Planet_"+PlanetNames[i]+".png");

            skyMaterial=Own(new Material(skyShader){name="DF_Skybox360_PreviewOnly"});
            skyMaterial.SetTexture("_MainTex",panorama);skyMaterial.SetFloat("_Mapping",1);skyMaterial.SetFloat("_ImageType",0);
            skyMaterial.SetFloat("_Exposure",.8f);skyMaterial.SetColor("_Tint",new Color(.5f,.5f,.5f));
            if(skyMaterial.passCount!=1)throw new InvalidOperationException("Unerwartete Skybox-Passanzahl; Vorschau wurde abgebrochen.");
            Mesh sphere=Sphere(64,32);
            root=new GameObject("DF_Skybox360_EditorPreview"){hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild};
            root.transform.SetParent(matchWorld.transform,false);
            root.transform.position=matchWorld.transform.position;
            // Orient the distant layout behind the actual downward-looking MOBA camera.
            // Real geometry gives parallax without changing camera projection/transform or adding a camera.
            root.transform.rotation=target.transform.rotation;
            float scale=Mathf.Min(1f,target.farClipPlane/1100f);root.transform.localScale=Vector3.one*scale;
            for(int i=0;i<PlanetNames.Length;i++)
            {
                var material=Own(new Material(surface){name="DF_PreviewPlanet_"+PlanetNames[i]});
                SetTexture(material,planetTextures[i]);
                material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.18f);
                Part("Planet_"+PlanetNames[i],sphere,material,Centers[i],Vector3.one*Radii[i]);
            }
            AddRings(unlit);

            // Commit only after every texture/material/mesh has been prepared successfully.
            camera=target;world=matchWorld;previousFlags=camera.clearFlags;
            skybox=camera.GetComponent<Skybox>();addedSkybox=skybox==null;
            if(addedSkybox)skybox=camera.gameObject.AddComponent<Skybox>();
            previousMaterial=skybox.material;previousEnabled=skybox.enabled;
            skybox.material=skyMaterial;skybox.enabled=true;camera.clearFlags=CameraClearFlags.Skybox;
        }

        private static Texture2D Load(string name)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot+name);
            if(texture==null)throw new InvalidOperationException("Vorschau-Textur fehlt: "+name);
            return texture;
        }

        private static void SetTexture(Material material,Texture2D texture)
        {
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
            if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",texture);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
            if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
        }

        private GameObject Part(string name,Mesh mesh,Material material,Vector3 center,Vector3 scale)
        {
            var part=new GameObject(name){hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild};
            part.transform.SetParent(root.transform,false);part.transform.localPosition=center;part.transform.localScale=scale;
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return part;
        }

        internal bool IsCurrent(Camera current,GameObject matchWorld)
            =>camera!=null&&current==camera&&world!=null&&world==matchWorld&&world.activeInHierarchy&&root!=null&&skybox!=null&&skybox.material==skyMaterial&&skybox.enabled&&camera.clearFlags==CameraClearFlags.Skybox;

        public void Dispose()
        {
            // Restore only state still owned by this preview; never overwrite a subsequent menu/camera owner.
            if(skybox!=null&&skybox.material==skyMaterial)
            {
                skybox.material=previousMaterial;skybox.enabled=previousEnabled;
                if(camera!=null&&camera.clearFlags==CameraClearFlags.Skybox)camera.clearFlags=previousFlags;
                if(addedSkybox)Object.DestroyImmediate(skybox);
            }
            if(root!=null)Object.DestroyImmediate(root);
            for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);
            owned.Clear();camera=null;world=null;root=null;skybox=null;skyMaterial=null;previousMaterial=null;
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
            var mesh=Own(new Mesh{name="DF_PreviewPlanetSphere"});mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(faces,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }

        private void AddRings(Shader shader)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var faces=new List<int>();
            var palette=Own(new Texture2D(1,10,TextureFormat.RGBA32,false){name="DF_PreviewRingPalette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp});
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
            var material=Own(new Material(shader){name="DF_PreviewRings"});SetTexture(material,palette);material.SetInt("_Cull",0);
            var mesh=Own(new Mesh{name="DF_PreviewPlanetRings"});mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var ring=Part("Azure_Rings",mesh,material,Centers[0],Vector3.one);
            ring.transform.localRotation=Quaternion.FromToRotation(Vector3.up,new Vector3(.14f,.88f,-.45f).normalized);
        }
    }

    public sealed class Skybox360PreviewTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Skybox360PreviewSession.TextureRoot,StringComparison.Ordinal)||!assetPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
            importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.None;importer.isReadable=false;
            importer.mipmapEnabled=true;importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;
            importer.maxTextureSize=assetPath.EndsWith("Sky360_StarsNebula.png",StringComparison.Ordinal)?2048:1024;
            importer.textureCompression=TextureImporterCompression.Compressed;importer.compressionQuality=70;
        }
    }
}
#endif
