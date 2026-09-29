using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Removes the dark basalt border geometry around the runtime Pyraxis lava pools.
    /// The orange/black volcanic ground remains unchanged; only the explicit black pool rims
    /// are hidden so the molten shapes blend directly into the terrain.
    /// </summary>
    [DefaultExecutionOrder(2600)]
    internal sealed class PyraxisLavaRimCleanup : MonoBehaviour
    {
        private static PyraxisLavaRimCleanup instance;
        private Transform cleanedRoot;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_PyraxisLavaRimCleanup");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<PyraxisLavaRimCleanup>();
        }

        private void Update()
        {
            if(Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.20f;

            if(!ArenaVariantRuntime.IsPyraxis)
            {
                cleanedRoot=null;
                return;
            }

            GameObject poolObject=GameObject.Find("DF_PyraxisCentralLavaPools");
            if(poolObject==null)return;
            Transform root=poolObject.transform;
            if(root==cleanedRoot)return;

            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(t==null||t==root)continue;
                if(t.name!="CentralLavaPoolRim"&&t.name!="CentralLavaPoolRimLobe")continue;

                MeshRenderer renderer=t.GetComponent<MeshRenderer>();
                if(renderer!=null)renderer.enabled=false;
                Destroy(t.gameObject);
            }

            cleanedRoot=root;
        }
    }
}
