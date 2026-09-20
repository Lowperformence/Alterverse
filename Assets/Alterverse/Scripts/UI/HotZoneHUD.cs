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
        private Color bannerColor=new(.95f,.58f,.20f);
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

        private void OnOpening(){banner="HOT ZONE OPENING · 10 SECONDS";bannerColor=new Color(.95f,.72f,.30f);bannerUntil=Time.time+4f;}
        private void OnOpened(){banner="HOT ZONE ACTIVE · CONTROL THE ZONE";bannerColor=Color.white;bannerUntil=Time.time+4f;}
        private void OnResolved(int winner,float zero,float lumen)
        {
            bool reward=zone!=null&&zone.RewardSpawned;
            banner=winner==0?(reward?"ZERO WINS HOT ZONE · ASSAULT TANK DEPLOYED":"ZERO WINS HOT ZONE"):
                winner==1?(reward?"LUMEN WINS HOT ZONE · ASSAULT TANK DEPLOYED":"LUMEN WINS HOT ZONE"):
                "HOT ZONE DRAW · NO TANK DEPLOYED";
            bannerColor=winner==0?new Color(.72f,.25f,1f):winner==1?new Color(.16f,.88f,1f):new Color(.72f,.74f,.80f);
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
                    FrontEndTheme.Fill(new Rect(b.x,b.y,b.width,3),bannerColor);
                    theme.Label(new Rect(b.x+10,b.y+4,b.width-20,b.height-8),banner,centerStyle);
                }

                if(showProgress)
                {
                    float y=showBanner?144:92;
                    Rect p=new((layout.Width-450)*.5f,y,450,58);
                    theme.Panel(p);
                    float lead=zone.ZeroControlSeconds-zone.LumenControlSeconds;
                    string state=zone.ZeroInside>0&&zone.LumenInside>0?"CONTESTED":
                        zone.ZeroInside>0?$"ZERO CONTROLLING · x{zone.ZeroInside}":
                        zone.LumenInside>0?$"LUMEN CONTROLLING · x{zone.LumenInside}":
                        Mathf.Abs(lead)<.05f?"UNCLAIMED":
                        lead>0?$"ZERO LEADS +{lead:0.0}":$"LUMEN LEADS +{-lead:0.0}";
                    theme.Label(new Rect(p.x+12,p.y+5,p.width-24,20),
                        $"HOT ZONE  {Mathf.CeilToInt(zone.Remaining)}s  ·  {state}",centerStyle);
                    Rect bar=new(p.x+24,p.y+34,p.width-48,8);
                    FrontEndTheme.Fill(bar,FrontEndTheme.Raised);
                    float half=bar.width*.5f;
                    float zeroFill=half*Mathf.Clamp01(zone.ZeroControlSeconds/HotZoneDirector.ActiveDuration);
                    float lumenFill=half*Mathf.Clamp01(zone.LumenControlSeconds/HotZoneDirector.ActiveDuration);
                    FrontEndTheme.Fill(new Rect(bar.x+half-zeroFill,bar.y,zeroFill,bar.height),FrontEndTheme.Zero);
                    FrontEndTheme.Fill(new Rect(bar.x+half,bar.y,lumenFill,bar.height),FrontEndTheme.Accent);
                    theme.Label(new Rect(p.x+18,p.y+43,p.width*.45f,14),$"ZERO {zone.ZeroControlSeconds:0.0}",theme.Small,true);
                    theme.Label(new Rect(p.x+p.width*.55f-18,p.y+43,p.width*.45f,14),$"LUMEN {zone.LumenControlSeconds:0.0}",rightStyle,true);
                }
            }
            finally{GUI.matrix=previous;}
        }
    }
}
