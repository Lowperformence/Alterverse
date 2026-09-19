using Alterverse.Match;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed class HotZoneHUD:MonoBehaviour
    {
        private HotZoneDirector zone;
        private FrontEndTheme theme;
        private GUIStyle centerStyle,rightStyle;
        private string banner="";
        private float bannerUntil;

        private void OnEnable()
        {
            HotZoneDirector.HotZoneOpening+=OnOpening;
            HotZoneDirector.HotZoneOpened+=OnOpened;
            HotZoneDirector.HotZoneResolved+=OnResolved;
        }

        private void OnDisable()
        {
            HotZoneDirector.HotZoneOpening-=OnOpening;
            HotZoneDirector.HotZoneOpened-=OnOpened;
            HotZoneDirector.HotZoneResolved-=OnResolved;
        }

        private void Update()
        {
            zone??=FindAnyObjectByType<HotZoneDirector>();
        }

        private void OnOpening(){banner="HOT ZONE OPENING · 10 SECONDS";bannerUntil=Time.time+4f;}
        private void OnOpened(){banner="HOT ZONE ACTIVE · HOLD THE CENTER";bannerUntil=Time.time+4f;}
        private void OnResolved(int winner,float zero,float lumen)
        {
            banner=winner==0?"ZERO WINS HOT ZONE · ASSAULT TANK DEPLOYED":
                winner==1?"LUMEN WINS HOT ZONE · ASSAULT TANK DEPLOYED":
                "HOT ZONE DRAW · NO TANK DEPLOYED";
            bannerUntil=Time.time+5f;
        }

        private void OnGUI()
        {
            if(Time.timeScale<=0f||MainMenuController.IsVisible)return;
            zone??=FindAnyObjectByType<HotZoneDirector>();
            bool showBanner=Time.time<bannerUntil;
            bool showProgress=zone!=null&&zone.IsActive;
            if(!showBanner&&!showProgress)return;

            theme??=new FrontEndTheme();
            centerStyle??=new GUIStyle(theme.Small){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            rightStyle??=new GUIStyle(centerStyle){alignment=TextAnchor.MiddleRight};
            CombatHudLayout layout=new(Screen.safeArea);
            Matrix4x4 previous=GUI.matrix;
            try
            {
                GUI.matrix=layout.Matrix;
                if(showBanner)
                {
                    Rect b=new((layout.Width-500)*.5f,86,500,50);
                    theme.Panel(b);
                    FrontEndTheme.Fill(new Rect(b.x,b.y,b.width,2),new Color(.95f,.58f,.20f));
                    theme.Label(new Rect(b.x+10,b.y+4,b.width-20,b.height-8),banner,centerStyle);
                }

                if(showProgress)
                {
                    float y=showBanner?144:92;
                    Rect p=new((layout.Width-450)*.5f,y,450,58);
                    theme.Panel(p);
                    string state=zone.ZeroInside>0&&zone.LumenInside>0?"CONTESTED":
                        zone.ZeroInside>0?"ZERO CONTROLLING":
                        zone.LumenInside>0?"LUMEN CONTROLLING":"UNCLAIMED";
                    theme.Label(new Rect(p.x+12,p.y+5,p.width-24,20),
                        $"HOT ZONE  {Mathf.CeilToInt(zone.Remaining)}s  ·  {state}",centerStyle);
                    float total=zone.ZeroControlSeconds+zone.LumenControlSeconds;
                    Rect bar=new(p.x+24,p.y+34,p.width-48,8);
                    FrontEndTheme.Fill(bar,FrontEndTheme.Raised);
                    if(total>.01f)
                    {
                        float zeroShare=zone.ZeroControlSeconds/total;
                        FrontEndTheme.Fill(new Rect(bar.x,bar.y,bar.width*zeroShare,bar.height),FrontEndTheme.Zero);
                        FrontEndTheme.Fill(new Rect(bar.x+bar.width*zeroShare,bar.y,bar.width*(1-zeroShare),bar.height),FrontEndTheme.Accent);
                    }
                    theme.Label(new Rect(p.x+18,p.y+43,p.width*.45f,14),$"ZERO {zone.ZeroControlSeconds:0.0}s",theme.Small,true);
                    theme.Label(new Rect(p.x+p.width*.55f-18,p.y+43,p.width*.45f,14),$"LUMEN {zone.LumenControlSeconds:0.0}s",rightStyle,true);
                }
            }
            finally{GUI.matrix=previous;}
        }
    }
}
