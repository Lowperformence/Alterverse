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

        private void Awake()
        {
            pixel=Texture2D.whiteTexture;
        }

        private void OnGUI()
        {
            if(Time.timeScale<=0f||MainMenuController.IsVisible)return;
            cam??=Camera.main;if(cam==null)return;

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
                float markerHeight=tank.IsXenoTitan?3.35f:2.55f;
                Vector3 screen=cam.WorldToScreenPoint(tank.transform.position+Vector3.up*markerHeight);
                if(screen.z<=0f)continue;

                float x=screen.x-58f;
                float y=Screen.height-screen.y;
                Rect label=new(x,y-20,116,18);
                string unit=tank.IsXenoTitan?(tank.Team==0?"ZERO XENO TITAN":"LUMEN XENO TITAN"):tank.IsMarsSkeleton?(tank.Team==0?"ZERO MARS TITAN":"LUMEN MARS TITAN"):(tank.Team==0?"ZERO ASSAULT TANK":"LUMEN ASSAULT TANK");
                GUI.Label(label,unit,labelStyle);

                Rect back=new(x+8,y,100,8);
                GUI.color=new Color(.035f,.04f,.055f,.92f);GUI.DrawTexture(back,pixel);
                float normalized=Mathf.Clamp01(tank.TankHealth.Normalized);
                Color teamColor=tank.Team==0?new Color(.72f,.25f,1f):new Color(.16f,.88f,1f);
                GUI.color=teamColor;GUI.DrawTexture(new Rect(back.x+1,back.y+1,(back.width-2)*normalized,back.height-2),pixel);
                GUI.color=Color.white;
                GUI.Label(new Rect(x,y+8,116,16),$"{Mathf.CeilToInt(tank.TankHealth.Current)} / {Mathf.CeilToInt(tank.TankHealth.Max)}",hpStyle);
            }
            GUI.color=Color.white;
        }
    }
}
