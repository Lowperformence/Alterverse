using Alterverse.UI;
using UnityEngine;
namespace Alterverse.Match
{
    public sealed class DimensionsFrontHUD : MonoBehaviour
    {
        private static DimensionsFrontHUD owner;
        private DimensionsFront front;
        private Alterverse.Combat.BattleItemAirdropSchedule supplies;
        private FrontEndTheme theme;
        private GUIStyle centered;
        private float nextFind;
        private void Awake(){if(owner!=null&&owner!=this){enabled=false;return;}owner=this;}
        private void OnDestroy(){if(owner==this)owner=null;}
        private void Update()
        {
            if(front==null&&Time.unscaledTime>=nextFind)
            {
                nextFind=Time.unscaledTime+.5f;
                front=FindAnyObjectByType<DimensionsFront>();
                supplies=front!=null?front.GetComponent<Alterverse.Combat.BattleItemAirdropSchedule>():null;
            }
        }
        private void OnGUI()
        {
            if(front==null||front.HasEnded||MainMenuController.IsVisible)return;
            Rect safe=Screen.safeArea;if(safe.width<=0||safe.height<=0)return;
            float scale=Mathf.Min(safe.height/720,safe.width/960),width=safe.width/scale;
            Matrix4x4 previous=GUI.matrix;Color color=GUI.color;
            try
            {
                GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,Vector3.one*scale);
                theme??=new FrontEndTheme();centered??=new GUIStyle(theme.Body){alignment=TextAnchor.MiddleCenter,font=FrontEndTypography.Display};
                Rect r=new Rect((width-420)*.5f,16,420,82);theme.HudPanel(r);
                DimensionUiMotif.DrawCornerCuts(r,FrontEndTheme.Pixel,FrontEndTheme.Zero,FrontEndTheme.Accent);
                FrontEndTheme.Fill(new Rect(r.x,r.y+10,3,r.height-20),FrontEndTheme.Zero);
                FrontEndTheme.Fill(new Rect(r.xMax-3,r.y+10,3,r.height-20),FrontEndTheme.Accent);
                FrontEndTheme.Fill(new Rect(r.x+14,r.y+3,196,1),FrontEndTheme.Zero);
                FrontEndTheme.Fill(new Rect(r.x+210,r.y+3,196,1),FrontEndTheme.Accent);
                int zero=MatchScoreboard.Instance!=null?MatchScoreboard.Instance.KillsForTeam(0):0;
                int lumen=MatchScoreboard.Instance!=null?MatchScoreboard.Instance.KillsForTeam(1):0;
                theme.Label(new Rect(r.x+14,r.y+7,110,30),"ZERO  "+zero,theme.Body);
                theme.Label(new Rect(r.xMax-124,r.y+7,110,30),lumen+"  LUMEN",theme.Body);
                int seconds=Mathf.CeilToInt(front.TimeRemaining);
                string clock=front.IsSuddenDeath?"SUDDEN DEATH":$"{seconds/60:00}:{seconds%60:00}";
                GUI.Label(new Rect(r.x+122,r.y+7,176,30),clock,centered);
                float progress=Mathf.Clamp01(front.ZeroControl/100);
                FrontEndTheme.Fill(new Rect(r.x+14,r.y+41,392*progress,7),FrontEndTheme.Zero);
                FrontEndTheme.Fill(new Rect(r.x+14+392*progress,r.y+41,392*(1-progress),7),FrontEndTheme.Accent);
                FrontEndTheme.Fill(new Rect(r.x+14+392*progress-1,r.y+39,2,11),Color.white);
                theme.Label(new Rect(r.x+14,r.y+52,110,22),$"{front.ZeroControl:0.#}%",theme.Small,true);
                theme.Label(new Rect(r.xMax-66,r.y+52,60,22),$"{front.LumenControl:0.#}%",theme.Small,true);
                theme.Center(new Rect(r.x+90,r.y+52,240,22),front.IsOvertime?"OVERTIME":"DIMENSIONS FRONT");

                if(supplies!=null&&!string.IsNullOrEmpty(supplies.Status))
                {
                    Rect supply=new Rect((width-280)*.5f,210,280,28);theme.Panel(supply);
                    DimensionUiMotif.DrawCornerCuts(supply,FrontEndTheme.Pixel,FrontEndTheme.Accent,FrontEndTheme.Positive);
                    theme.Center(supply,supplies.Status);
                }
            }
            finally{GUI.matrix=previous;GUI.color=color;}
        }
    }
}
