using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

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
        private Transform player;
        private GameObject compassArrow;
        private Mesh compassMesh;
        private Material compassMaterial;

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
            if(compassArrow!=null)compassArrow.SetActive(false);
        }

        private void Update()
        {
            zone??=FindAnyObjectByType<HotZoneDirector>();
            UpdateCompass();
        }

        private void OnOpening(){banner="HOT ZONE OPENING · 10 SECONDS";bannerColor=new Color(.95f,.72f,.30f);bannerUntil=Time.time+4f;}
        private void OnOpened(){banner="HOT ZONE ACTIVE";bannerColor=Color.white;bannerUntil=Time.time+2.5f;}
        private void OnResolved(int winner,float zero,float lumen)
        {
            bool reward=zone!=null&&zone.RewardSpawned;
            banner=winner==0?(reward?"ZERO WINS HOT ZONE · ASSAULT TANK DEPLOYED":"ZERO WINS HOT ZONE"):
                winner==1?(reward?"LUMEN WINS HOT ZONE · ASSAULT TANK DEPLOYED":"LUMEN WINS HOT ZONE"):
                "HOT ZONE DRAW · NO TANK DEPLOYED";
            bannerColor=winner==0?new Color(.72f,.25f,1f):winner==1?new Color(.16f,.88f,1f):new Color(.72f,.74f,.80f);
            bannerUntil=Time.time+5f;
        }

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
                GameObject playerObject=GameObject.Find("AV_Player");
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
                    string state=zone.ZeroInside>0&&zone.LumenInside>0?$"BOTH SCORING · ZERO x{zone.ZeroInside} · LUMEN x{zone.LumenInside}":
                        zone.ZeroInside>0?$"ZERO SCORING · x{zone.ZeroInside}":
                        zone.LumenInside>0?$"LUMEN SCORING · x{zone.LumenInside}":
                        Mathf.Abs(lead)<.05f?"":
                        lead>0?$"ZERO LEADS +{lead:0.0}":$"LUMEN LEADS +{-lead:0.0}";
                    string headline=$"HOT ZONE  {Mathf.CeilToInt(zone.Remaining)}s";
                    if(!string.IsNullOrEmpty(state))headline+=$"  ·  {state}";
                    theme.Label(new Rect(p.x+12,p.y+5,p.width-24,20),headline,centerStyle);
                    Rect bar=new(p.x+24,p.y+34,p.width-48,8);
                    FrontEndTheme.Fill(bar,FrontEndTheme.Raised);
                    float half=bar.width*.5f;
                    float maxTeamScore=HotZoneDirector.ActiveDuration*HotZoneDirector.MaxScoringHeroesPerTeam;
                    float zeroFill=half*Mathf.Clamp01(zone.ZeroControlSeconds/maxTeamScore);
                    float lumenFill=half*Mathf.Clamp01(zone.LumenControlSeconds/maxTeamScore);
                    FrontEndTheme.Fill(new Rect(bar.x+half-zeroFill,bar.y,zeroFill,bar.height),FrontEndTheme.Zero);
                    FrontEndTheme.Fill(new Rect(bar.x+half,bar.y,lumenFill,bar.height),FrontEndTheme.Accent);
                    theme.Label(new Rect(p.x+18,p.y+43,p.width*.45f,14),$"ZERO {zone.ZeroControlSeconds:0.0}",theme.Small,true);
                    theme.Label(new Rect(p.x+p.width*.55f-18,p.y+43,p.width*.45f,14),$"LUMEN {zone.LumenControlSeconds:0.0}",rightStyle,true);
                }
            }
            finally{GUI.matrix=previous;}
        }
        private void OnDestroy()
        {
            if(compassArrow!=null)Destroy(compassArrow);
            if(compassMaterial!=null)Destroy(compassMaterial);
            if(compassMesh!=null)Destroy(compassMesh);
        }

    }
}
