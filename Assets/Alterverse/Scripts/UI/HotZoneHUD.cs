using Alterverse.Match;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.UI
{
    public sealed class HotZoneHUD:MonoBehaviour
    {
        private HotZoneDirector zone;
        private FrontEndTheme theme;
        private GUIStyle centerStyle,leftStyle,rightStyle,shadowStyle;
        private const float StripWidth=320f,BannerWidth=200f,ActiveHeight=34f,BannerHeight=26f;
        private string banner="";
        private Color bannerColor=FrontEndTheme.Accent;
        private float bannerUntil;
        private Transform player;
        private GameObject compassArrow;
        private Mesh compassMesh;
        private Material compassMaterial;

        private void OnEnable()
        {
            HotZoneDirector.HotZoneOpening+=OnOpening;
            HotZoneDirector.HotZoneOpened+=OnOpened;
        }

        private void OnDisable()
        {
            HotZoneDirector.HotZoneOpening-=OnOpening;
            HotZoneDirector.HotZoneOpened-=OnOpened;
            if(compassArrow!=null)compassArrow.SetActive(false);
        }

        private void Update()
        {
            zone??=FindAnyObjectByType<HotZoneDirector>();
            UpdateCompass();
        }

        private void OnOpening(){banner="HOT ZONE OPENING";bannerColor=FrontEndTheme.Accent;bannerUntil=Time.time+4f;}
        private void OnOpened(){banner="HOT ZONE ACTIVE";bannerColor=Color.white;bannerUntil=Time.time+2.5f;}
        private void UpdateCompass()
        {
            bool active=zone!=null&&zone.IsActive&&Time.timeScale>0f&&!MainMenuController.IsVisible;
            if(!active)
            {
                if(compassArrow!=null)compassArrow.SetActive(false);
                return;
            }

            if(player==null)
            {
                GameObject playerObject=Alterverse.Characters.LocalHeroContext.Find();
                if(playerObject!=null)player=playerObject.transform;
            }
            if(player==null)return;

            Vector3 direction=zone.transform.position-player.position;
            direction.y=0f;
            if(direction.sqrMagnitude<2.25f)
            {
                if(compassArrow!=null)compassArrow.SetActive(false);
                return;
            }

            EnsureCompass();
            if(compassArrow==null)return;

            direction.Normalize();
            Vector3 position=player.position+direction*1.75f;
            Vector3 surface=LunarSurfaceField.Surface(position.x,position.z);
            position.y=surface.y+.16f;
            compassArrow.transform.position=position;
            compassArrow.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            compassArrow.SetActive(true);
        }

        private void EnsureCompass()
        {
            if(compassArrow!=null)return;

            compassArrow=new GameObject("DF_HotZoneCompassArrow");
            compassArrow.transform.SetParent(transform,true);
            compassArrow.layer=GameplayCollision.Decorative;

            compassMesh=new Mesh{name="DF_HotZoneCompassArrowMesh"};
            compassMesh.vertices=new[]
            {
                new Vector3(0f,0f,.55f),
                new Vector3(-.24f,0f,.10f),
                new Vector3(-.08f,0f,.10f),
                new Vector3(-.08f,0f,-.38f),
                new Vector3(.08f,0f,-.38f),
                new Vector3(.08f,0f,.10f),
                new Vector3(.24f,0f,.10f)
            };
            compassMesh.triangles=new[]
            {
                0,2,1,
                0,5,2,
                0,6,5,
                2,4,3,
                2,5,4
            };
            compassMesh.RecalculateNormals();
            compassMesh.RecalculateBounds();

            MeshFilter filter=compassArrow.AddComponent<MeshFilter>();
            filter.sharedMesh=compassMesh;
            MeshRenderer renderer=compassArrow.AddComponent<MeshRenderer>();
            Shader shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(shader!=null)
            {
                Color red=new(.72f,.08f,.06f,1f);
                compassMaterial=new Material(shader){color=red};
                if(compassMaterial.HasProperty("_BaseColor"))compassMaterial.SetColor("_BaseColor",red);
                renderer.sharedMaterial=compassMaterial;
            }
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }

        private void OnGUI()
        {
            if(Time.timeScale<=0f||MainMenuController.IsVisible||Event.current.type!=EventType.Repaint)return;
            zone??=FindAnyObjectByType<HotZoneDirector>();
            bool showBanner=Time.time<bannerUntil;
            bool showProgress=zone!=null&&zone.IsActive;
            if(!showBanner&&!showProgress)return;

            CombatHudLayout layout=new(Screen.safeArea);
            if(layout.Safe.width<=0f||layout.Safe.height<=0f)return;
            theme??=new FrontEndTheme();
            centerStyle??=new GUIStyle(theme.Small)
            {
                font=FrontEndTypography.Display,fontSize=13,fontStyle=FontStyle.Normal,
                alignment=TextAnchor.MiddleCenter,wordWrap=false,clipping=TextClipping.Clip
            };
            leftStyle??=new GUIStyle(centerStyle){alignment=TextAnchor.MiddleLeft,fontSize=14};
            rightStyle??=new GUIStyle(centerStyle){alignment=TextAnchor.MiddleRight,fontSize=14};
            centerStyle.normal.textColor=showBanner?bannerColor:Color.Lerp(FrontEndTheme.Text,FrontEndTheme.Muted,.35f);
            leftStyle.normal.textColor=Color.Lerp(FrontEndTheme.Zero,Color.white,.45f);
            rightStyle.normal.textColor=Color.Lerp(FrontEndTheme.Accent,Color.white,.35f);
            Matrix4x4 previous=GUI.matrix;
            try
            {
                GUI.matrix=layout.Matrix;
                // Event hints and live scores share one strip instead of stacking two panels.
                float width=Mathf.Min(showProgress?StripWidth:BannerWidth,layout.Width-24f);
                // Keep a gap below the match header on phones, tablets and taller aspect ratios.
                float headerScale=Mathf.Min(layout.Safe.height/720f,layout.Safe.width/960f);
                float top=Mathf.Max(92f,98f*headerScale/layout.Scale+12f);
                Rect panel=new((layout.Width-width)*.5f,top,width,showProgress?ActiveHeight:BannerHeight);
                // Keep the scenery visible: 20% opacity, with no extra frame or panel shadow.
                FrontEndTheme.Fill(panel,new Color(.02f,.03f,.05f,.20f));
                if(showProgress)DrawProgress(panel);
                else DrawLabel(new Rect(panel.x+12f,panel.y+3f,panel.width-24f,panel.height-6f),banner,centerStyle);
            }
            finally{GUI.matrix=previous;}
        }

        private void DrawProgress(Rect panel)
        {
            const float centerWidth=90f;
            float sideWidth=(panel.width-centerWidth)*.5f-16f;
            string zero=$"ZERO {zone.ZeroControlSeconds:0.0}";
            string lumen=$"LUMEN {zone.LumenControlSeconds:0.0}";
            if(zone.ZeroInside>0)zero+=$" x{zone.ZeroInside}";
            if(zone.LumenInside>0)lumen+=$" x{zone.LumenInside}";
            DrawLabel(new Rect(panel.x+12f,panel.y+3f,sideWidth,22f),zero,leftStyle);
            DrawLabel(new Rect(panel.center.x-centerWidth*.5f,panel.y+3f,centerWidth,22f),
                Alterverse.Profile.GameLanguage.Text(zone.ZeroInside>0&&zone.LumenInside>0?"CONTESTED":(player!=null&&(player.position-zone.transform.position).sqrMagnitude<=HotZoneDirector.Radius*HotZoneDirector.Radius?"CAPTURING":"HOT ZONE")),centerStyle);
            DrawLabel(new Rect(panel.xMax-sideWidth-12f,panel.y+3f,sideWidth,22f),lumen,rightStyle);

            Rect bar=new(panel.x+12f,panel.y+27f,panel.width-24f,3f);
            FrontEndTheme.Fill(bar,new Color(0f,0f,0f,.20f));
            float half=bar.width*.5f;
            float maxTeamScore=HotZoneDirector.ActiveDuration*HotZoneDirector.MaxScoringHeroesPerTeam;
            float zeroFill=half*Mathf.Clamp01(zone.ZeroControlSeconds/maxTeamScore);
            float lumenFill=half*Mathf.Clamp01(zone.LumenControlSeconds/maxTeamScore);
            FrontEndTheme.Fill(new Rect(bar.x+half-zeroFill,bar.y,zeroFill,bar.height),FrontEndTheme.Zero);
            FrontEndTheme.Fill(new Rect(bar.x+half,bar.y,lumenFill,bar.height),FrontEndTheme.Accent);
            FrontEndTheme.Fill(new Rect(bar.x+half-.5f,bar.y,1f,bar.height),FrontEndTheme.Muted);
        }

        private void DrawLabel(Rect rect,string text,GUIStyle style)
        {
            // A small text shadow keeps the translucent HUD legible over bright arena surfaces.
            shadowStyle??=new GUIStyle(centerStyle);
            shadowStyle.alignment=style.alignment;
            shadowStyle.fontSize=style.fontSize;
            shadowStyle.fontStyle=style.fontStyle;
            shadowStyle.normal.textColor=new Color(0f,0f,0f,.85f);
            GUI.Label(new Rect(rect.x+1f,rect.y+1f,rect.width,rect.height),text,shadowStyle);
            theme.Label(rect,text,style);
        }

        private void OnDestroy()
        {
            if(compassArrow!=null)Destroy(compassArrow);
            if(compassMaterial!=null)Destroy(compassMaterial);
            if(compassMesh!=null)Destroy(compassMesh);
        }

    }
}

