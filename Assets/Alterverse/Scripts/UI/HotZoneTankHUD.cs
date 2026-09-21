using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Compact world-space readability for the one-shot Hot Zone assault tank.</summary>
    public sealed class HotZoneTankHUD:MonoBehaviour
    {
        private GUIStyle labelStyle,hpStyle;
        private Texture2D pixel;
        private Camera cam;
        private TeamMember playerTeam;
        private float nextPlayerResolve;

        private void Awake()
        {
            pixel=Texture2D.whiteTexture;
        }

        private void OnGUI()
        {
            if(Time.timeScale<=0f||MainMenuController.IsVisible)return;
            cam??=Camera.main;if(cam==null)return;
            if((playerTeam==null||!playerTeam.isActiveAndEnabled)&&Time.unscaledTime>=nextPlayerResolve)
            {
                nextPlayerResolve=Time.unscaledTime+.5f;
                GameObject player=GameObject.Find("AV_Player");
                if(player!=null)playerTeam=player.GetComponent<TeamMember>();
            }

            if(labelStyle==null)
            {
                labelStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=12};
                labelStyle.normal.textColor=Color.white;
            }
            hpStyle??=new GUIStyle(labelStyle){fontSize=10};

            var tanks=HotZoneTank.Active;
            for(int i=0;i<tanks.Count;i++)
            {
                HotZoneTank tank=tanks[i];
                if(tank==null||tank.TankHealth==null||tank.TankHealth.IsDead||!tank.isActiveAndEnabled)continue;
                float markerHeight=tank.IsXenoTitan?3.35f:tank.IsKryonBear?3.05f:tank.IsNoxaraSpider?2.85f:2.55f;
                Vector3 screen=cam.WorldToScreenPoint(tank.transform.position+Vector3.up*markerHeight);
                if(screen.z<=0f)continue;

                float x=screen.x-58f;
                float y=Screen.height-screen.y;
                string unit=tank.IsKryonBear?(tank.Team==0?"ZERO KRYON FROST BEAR":"LUMEN KRYON FROST BEAR"):
                    tank.IsNoxaraSpider?(tank.Team==0?"ZERO NOXARA SPIDER":"LUMEN NOXARA SPIDER"):
                    tank.IsXenoTitan?(tank.Team==0?"ZERO XENO TITAN":"LUMEN XENO TITAN"):
                    tank.IsMarsSkeleton?(tank.Team==0?"ZERO MARS TITAN":"LUMEN MARS TITAN"):
                    (tank.Team==0?"ZERO ASSAULT TANK":"LUMEN ASSAULT TANK");

                // Size the world label to the actual localized/unit name instead of the old fixed
                // 116 px box. This prevents clipping for Assault Tank, Mars Titan and Xeno Titan.
                float labelWidth=Mathf.Clamp(labelStyle.CalcSize(new GUIContent(unit)).x+18f,116f,260f);
                bool enemyMonster=playerTeam!=null&&tank.Team!=playerTeam.Team;
                Rect label=new(screen.x-labelWidth*.5f,y-(enemyMonster?34f:20f),labelWidth,18);
                GUI.Label(label,unit,labelStyle);
                if(enemyMonster)DrawEnemyCrystal(screen.x,y-12f,1f);

                Rect back=new(x+8,y,100,8);
                GUI.color=new Color(.035f,.04f,.055f,.92f);GUI.DrawTexture(back,pixel);
                float normalized=Mathf.Clamp01(tank.TankHealth.Normalized);
                Color teamColor=tank.Team==0?new Color(.72f,.25f,1f):new Color(.16f,.88f,1f);
                // Enemy Hot Zone monsters always use a red HP fill for immediate combat readability.
                // Friendly reward monsters keep their existing team color.
                Color barColor=enemyMonster
                    ?new Color(1f,.10f,.10f,.98f)
                    :teamColor;
                GUI.color=barColor;GUI.DrawTexture(new Rect(back.x+1,back.y+1,(back.width-2)*normalized,back.height-2),pixel);
                GUI.color=Color.white;
                GUI.Label(new Rect(x,y+8,116,16),$"{Mathf.CeilToInt(tank.TankHealth.Current)} / {Mathf.CeilToInt(tank.TankHealth.Max)}",hpStyle);
            }
            GUI.color=Color.white;
        }

        private void DrawEnemyCrystal(float cx,float cy,float scale)
        {
            float size=10f*scale;
            Matrix4x4 oldMatrix=GUI.matrix;
            Color oldColor=GUI.color;
            GUIUtility.RotateAroundPivot(45f,new Vector2(cx,cy));
            GUI.color=new Color(1f,.035f,.035f,.98f);
            GUI.DrawTexture(new Rect(cx-size*.5f,cy-size*.5f,size,size),pixel);
            float inner=size*.46f;
            GUI.color=new Color(1f,.56f,.56f,.98f);
            GUI.DrawTexture(new Rect(cx-inner*.5f,cy-inner*.5f,inner,inner),pixel);
            GUI.matrix=oldMatrix;
            GUI.color=oldColor;
        }
    }
}
