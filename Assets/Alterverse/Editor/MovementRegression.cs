#if UNITY_EDITOR
using Alterverse.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class MovementRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Movement collision and dash (Play mode)")]
        private static void RunChecks()
        {
            if(!Application.isPlaying||Time.timeScale<=0)
            {Debug.LogWarning("Start a running match before the movement checks.");return;}
            var host=new GameObject("DF_MovementRegression");
            host.AddComponent<MovementRegressionRunner>().Begin();
        }
    }
}
#endif
