using Alterverse.Match;
using UnityEngine;
namespace Alterverse.UI
{
    public sealed class PlanetHUD:MonoBehaviour
    {
        private PlanetBuffReceiver playerBuff;
        private float nextFind;
        private FrontEndTheme theme;
        private GUIStyle notice,small;
        private void Update()
        {
            if(playerBuff!=null||Time.unscaledTime<nextFind)return;nextFind=Time.unscaledTime+.5f;
            var player=FindAnyObjectByType<Alterverse.Characters.HeroRespawn>();if(player!=null)playerBuff=player.GetComponent<PlanetBuffReceiver>();
        }
        private void OnGUI()
        {
            if(Time.timeScale<=0||MainMenuController.IsVisible)return;
            theme??=new FrontEndTheme();notice??=new GUIStyle(theme.Small){alignment=TextAnchor.MiddleCenter};small??=new GUIStyle(theme.Small){fontSize=12};
            CombatHudLayout layout=new CombatHudLayout(Screen.safeArea);Matrix4x4 before=GUI.matrix;
            try
            {
                GUI.matrix=layout.Matrix;
                if(Time.unscaledTime<DimensionPlanetDirector.AnnouncementUntil&&!string.IsNullOrEmpty(DimensionPlanetDirector.Announcement))
                {
                    Rect r=new Rect((layout.Width-440)*.5f,86,440,52);theme.Panel(r);
                    theme.Label(new Rect(r.x+12,r.y+4,r.width-24,r.height-8),DimensionPlanetDirector.Announcement,notice);
                }
                if(!string.IsNullOrEmpty(DimensionPlanetDirector.NextEventLabel))
                {
                    Rect next=new Rect(16,98,218,24);theme.Panel(next);
                    theme.Label(next,$"NEXT EVENT  •  {DimensionPlanetDirector.NextEventLabel}  {Mathf.CeilToInt(DimensionPlanetDirector.NextEventRemaining)}s",small,true);
                }
                if(playerBuff!=null&&playerBuff.ActiveRemaining>0)
                {
                    Rect r=new Rect(16,126,218,64);theme.Panel(r);
                    string effect=playerBuff.FluxActive&&playerBuff.GuardActive?"+15% DAMAGE · +10% MOVE\n+15% MINION DAMAGE":playerBuff.FluxActive?"+15% DAMAGE":"+10% MOVE\n+15% MINION DAMAGE";
                    string name=playerBuff.FluxActive&&playerBuff.GuardActive?"FLUX + GUARD":playerBuff.FluxActive?"FLUX":"GUARD";
                    theme.Label(new Rect(r.x+10,r.y+4,198,20),$"{name}   {Mathf.CeilToInt(playerBuff.ActiveRemaining)}s",small);
                    theme.Label(new Rect(r.x+10,r.y+25,198,36),effect,small,true);
                }
            }
            finally{GUI.matrix=before;}
        }
    }
}

