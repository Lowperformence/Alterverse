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
        private string cachedNextLabel,cachedNextText,cachedBuffName,cachedBuffText;
        private int cachedNextSec=-1,cachedBuffSec=-1;
        private void Update()
        {
            if(playerBuff!=null||Time.unscaledTime<nextFind)return;nextFind=Time.unscaledTime+.5f;
            var player=Alterverse.Characters.LocalHeroContext.Find();if(player!=null)playerBuff=player.GetComponent<PlanetBuffReceiver>();
        }
        private void OnGUI()
        {
            if(Time.timeScale<=0||MainMenuController.IsVisible||(Event.current!=null&&Event.current.type!=EventType.Repaint))return;
            theme??=new FrontEndTheme();notice??=new GUIStyle(theme.Small){alignment=TextAnchor.MiddleCenter};small??=new GUIStyle(theme.Small){fontSize=12};
            CombatHudLayout layout=new CombatHudLayout(Screen.safeArea);Matrix4x4 before=GUI.matrix;
            try
            {
                GUI.matrix=layout.Matrix;
                if(Time.unscaledTime<DimensionPlanetDirector.AnnouncementUntil&&!string.IsNullOrEmpty(DimensionPlanetDirector.Announcement))
                {
                    Rect r=new Rect((layout.Width-440)*.5f,86,440,52);theme.HudPanel(r,.50f);
                    theme.Label(new Rect(r.x+12,r.y+4,r.width-24,r.height-8),DimensionPlanetDirector.Announcement,notice);
                }
                if(!string.IsNullOrEmpty(DimensionPlanetDirector.NextEventLabel))
                {
                    Rect next=new Rect(16,98,218,24);theme.HudPanel(next);
                    int sec=Mathf.CeilToInt(DimensionPlanetDirector.NextEventRemaining);
                    string lbl=DimensionPlanetDirector.NextEventLabel;
                    if(sec!=cachedNextSec||lbl!=cachedNextLabel||cachedNextText==null)
                    {
                        cachedNextSec=sec;cachedNextLabel=lbl;
                        cachedNextText=$"NEXT EVENT  •  {lbl}  {sec}s";
                    }
                    theme.Label(next,cachedNextText,small,true);
                }
                if(playerBuff!=null&&playerBuff.ActiveRemaining>0)
                {
                    Rect r=new Rect(16,126,218,82);theme.Panel(r);
                    string effect=playerBuff.FluxActive&&playerBuff.GuardActive?"+15% DAMAGE\n-20% DAMAGE TAKEN":playerBuff.FluxActive?"+15% DAMAGE":"-20% DAMAGE TAKEN";
                    string name=playerBuff.FluxActive&&playerBuff.GuardActive?"FLUX + GUARD":playerBuff.FluxActive?"FLUX":"GUARD";
                    int buffSec=Mathf.CeilToInt(playerBuff.ActiveRemaining);
                    if(buffSec!=cachedBuffSec||name!=cachedBuffName||cachedBuffText==null)
                    {
                        cachedBuffSec=buffSec;cachedBuffName=name;
                        cachedBuffText=$"{name}   {buffSec}s";
                    }
                    theme.Label(new Rect(r.x+10,r.y+4,198,20),cachedBuffText,small);
                    theme.Label(new Rect(r.x+10,r.y+25,198,54),effect,small,true);
                }
            }
            finally{GUI.matrix=before;}
        }
    }
}
