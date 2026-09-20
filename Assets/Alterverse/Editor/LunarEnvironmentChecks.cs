#if UNITY_EDITOR
using System.IO;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    public static class LunarEnvironmentChecks
    {
        private static void Check(bool value,string message){if(!value)throw new System.InvalidOperationException("Lunar check: "+message);}
        [MenuItem("DIMENSIONS FRONT/Checks/Lunar geometry and free-roam bounds")]
        public static void Geometry()
        {
            for(int z=-36;z<=36;z++)for(int x=-42;x<=42;x++)
            {
                Vector3 p=LunarSurfaceField.Surface(x,z);
                Check(!float.IsNaN(p.y)&&p.y>=-.116f&&p.y<=.081f,"visual relief outside intended range");
                Check(Mathf.Abs(p.x)<48&&Mathf.Abs(p.z)<42,"surface outside unique atlas");
            }
            // Current PvP layout is free-roam: terrain must not secretly flatten or tint legacy lane corridors.
            for(int x=-28;x<=28;x++)
            {
                Check(Mathf.Approximately(LunarSurfaceField.LaneWeight(x,16),0f),"legacy north-lane terrain weighting returned");
                Check(Mathf.Approximately(LunarSurfaceField.LaneWeight(x,-16),0f),"legacy south-lane terrain weighting returned");
            }
            Mesh disc=ProductionMeshFactory.Disc(1,.2f,24);Mesh fragment=LunarFragmentGeometry.Create(7);
            try
            {
                Vector3[] v=disc.vertices;int[] t=disc.triangles;
                for(int i=0;i<t.Length;i+=3)
                {
                    Vector3 a=v[t[i]],b=v[t[i+1]],c=v[t[i+2]],n=Vector3.Cross(b-a,c-a);
                    if(Mathf.Abs(a.y-.2f)<.001f&&Mathf.Abs(b.y-.2f)<.001f&&Mathf.Abs(c.y-.2f)<.001f)Check(n.y>0,"top cap winding");
                    if(Mathf.Abs(a.y+.2f)<.001f&&Mathf.Abs(b.y+.2f)<.001f&&Mathf.Abs(c.y+.2f)<.001f)Check(n.y<0,"bottom cap winding");
                }
                Check(fragment.vertexCount==162&&fragment.triangles.Length==162,"fragment geometry budget changed");
            }
            finally{Object.DestroyImmediate(disc);Object.DestroyImmediate(fragment);}
            Debug.Log("DIMENSIONS FRONT lunar numeric checks passed. Visual/device acceptance still required.");
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Capture lunar gameplay views (Play mode)")]
        public static void Capture()
        {
            Camera main=Camera.main;HeroRespawn player=Object.FindAnyObjectByType<HeroRespawn>();
            if(!Application.isPlaying||main==null||player==null||Object.FindAnyObjectByType<ProductionLunarGround>()==null)
            {Debug.LogWarning("Launch a match and wait for the lunar surface before capturing.");return;}
            string directory="Temp/DimensionsFront-LunarReview";Directory.CreateDirectory(directory);
            var go=new GameObject("DF_LunarReviewCamera");Camera camera=go.AddComponent<Camera>();camera.CopyFrom(main);camera.enabled=false;
            RenderTexture target=new RenderTexture(1280,720,24);Texture2D pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            RenderTexture previous=RenderTexture.active;
            string[] labels={"zero-base","north-field","center","south-field","lumen-base","broken-rim"};
            Vector3[] points={new(-32,1,0),new(-15,1,16),new(0,1,0),new(15,1,-16),new(32,1,0),new(34,1,-30)};
            Vector3 offset=main.transform.position-player.transform.position;
            try
            {
                camera.targetTexture=target;
                for(int i=0;i<points.Length;i++)
                {
                    camera.transform.SetPositionAndRotation(points[i]+offset,main.transform.rotation);camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,labels[i]+".png"),pixels.EncodeToPNG());
                }
                Debug.Log("DIMENSIONS FRONT: map review captures saved to "+Path.GetFullPath(directory)+". Inspect them; capture success is not visual approval.");
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(pixels);Object.DestroyImmediate(target);Object.DestroyImmediate(go);}
        }
    }
}
#endif
