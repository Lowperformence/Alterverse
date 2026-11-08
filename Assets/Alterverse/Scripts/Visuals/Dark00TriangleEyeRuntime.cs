using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Replaces the DARK 00 Hot Zone monster's round red eye with a compact emissive triangular eye.
    /// The orb's gameplay, targeting and collision remain untouched; this is visual-only.
    /// </summary>
    public sealed class Dark00TriangleEyeRuntime : MonoBehaviour
    {
        private const string RuntimeObjectName="DF_Dark00TriangleEyeRuntime";
        private const string SourceEyeName="RedEye";
        private const string TriangleEyeName="RedTriangleEye";
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(GameObject.Find(RuntimeObjectName)!=null)return;
            GameObject go=new(RuntimeObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<Dark00TriangleEyeRuntime>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.20f;
            if(!ArenaVariantRuntime.IsDark00)return;

            IReadOnlyList<HotZoneTank> tanks=HotZoneTank.Active;
            for(int t=0;t<tanks.Count;t++)
            {
                HotZoneTank tank=tanks[t];
                if(tank==null)continue;
                Transform[] transforms=tank.GetComponentsInChildren<Transform>(true);
                for(int i=0;i<transforms.Length;i++)
                {
                    Transform sourceEye=transforms[i];
                    if(sourceEye==null||sourceEye.name!=SourceEyeName||sourceEye.parent==null)continue;
                    if(sourceEye.parent.Find(TriangleEyeName)!=null)continue;
                    ReplaceRoundEye(sourceEye);
                }
            }
        }

        private static void ReplaceRoundEye(Transform sourceEye)
        {
            Renderer sourceRenderer=sourceEye.GetComponent<Renderer>();
            if(sourceRenderer==null)return;

            GameObject triangle=new(TriangleEyeName);
            triangle.transform.SetParent(sourceEye.parent,false);
            triangle.transform.localPosition=sourceEye.localPosition;
            triangle.transform.localRotation=sourceEye.localRotation;
            triangle.transform.localScale=Vector3.one;

            Mesh mesh=BuildTrianglePrism();
            MeshFilter filter=triangle.AddComponent<MeshFilter>();
            filter.sharedMesh=mesh;

            MeshRenderer renderer=triangle.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=sourceRenderer.sharedMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;

            Dark00TriangleEyeMeshOwner owner=triangle.AddComponent<Dark00TriangleEyeMeshOwner>();
            owner.Mesh=mesh;

            sourceRenderer.enabled=false;
        }

        private static Mesh BuildTrianglePrism()
        {
            const float halfWidth=.31f;
            const float halfHeight=.22f;
            const float halfDepth=.09f;

            Mesh mesh=new(){name="DF_Dark00RedTriangleEyeMesh"};
            mesh.vertices=new[]
            {
                new Vector3(0f,halfHeight,halfDepth),
                new Vector3(-halfWidth,-halfHeight,halfDepth),
                new Vector3(halfWidth,-halfHeight,halfDepth),
                new Vector3(0f,halfHeight,-halfDepth),
                new Vector3(-halfWidth,-halfHeight,-halfDepth),
                new Vector3(halfWidth,-halfHeight,-halfDepth)
            };
            mesh.triangles=new[]
            {
                0,1,2,
                3,5,4,
                0,3,4, 0,4,1,
                1,4,5, 1,5,2,
                2,5,3, 2,3,0
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    internal sealed class Dark00TriangleEyeMeshOwner : MonoBehaviour
    {
        internal Mesh Mesh;

        private void OnDestroy()
        {
            if(Mesh!=null)Destroy(Mesh);
        }
    }
}
