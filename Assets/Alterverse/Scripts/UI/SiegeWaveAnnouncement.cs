using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.UI
{
    public sealed class SiegeWaveAnnouncement:MonoBehaviour
    {
        private string message="";
        private static float until;
        private FrontEndTheme theme;
        private GUIStyle style;
        public static bool IsVisible=>Time.time<until;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>until=0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate(){if(FindAnyObjectByType<SiegeWaveAnnouncement>()==null)new GameObject("DF_SiegeWaveAnnouncement").AddComponent<SiegeWaveAnnouncement>();}
        private void OnEnable(){LaneWaveSpawner.SiegeWaveWarning+=OnWarning;LaneWaveSpawner.SiegeWaveSpawned+=OnSpawned;}
        private void OnDisable(){LaneWaveSpawner.SiegeWaveWarning-=OnWarning;LaneWaveSpawner.SiegeWaveSpawned-=OnSpawned;until=0;}
        private void OnWarning(float seconds){message=$"SIEGE WAVE INCOMING · {Mathf.CeilToInt(seconds)} SECONDS";until=Time.time+3.5f;}
        private void OnSpawned(){message="SIEGE MECHS DEPLOYED · BOTH LANES";until=Time.time+4;}
        private void OnGUI()
        {
            if(!IsVisible||Time.timeScale<=0||MainMenuController.IsVisible)return;
            theme??=new FrontEndTheme();style??=new GUIStyle(theme.Small){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            CombatHudLayout layout=new CombatHudLayout(Screen.safeArea);Matrix4x4 previous=GUI.matrix;
            try{GUI.matrix=layout.Matrix;Rect r=new Rect((layout.Width-440)*.5f,86,440,52);theme.Panel(r);FrontEndTheme.Fill(new Rect(r.x,r.y,r.width,2),new Color(.9f,.73f,.42f));theme.Label(new Rect(r.x+12,r.y+4,r.width-24,r.height-8),message,style);}
            finally{GUI.matrix=previous;}
        }
    }
}
