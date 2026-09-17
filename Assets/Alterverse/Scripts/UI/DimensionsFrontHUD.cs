using Alterverse.Match;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed class DimensionsFrontHUD : MonoBehaviour
    {
        private DimensionsFront front;
        private GUIStyle mainStyle;
        private GUIStyle smallStyle;
        private GUIStyle resultStyle;
        private int winner = -1;

        private void Start()
        {
            front = Object.FindAnyObjectByType<DimensionsFront>();
            if (front != null) front.MatchEnded += OnMatchEnded;
        }

        private void OnDestroy(){if(front!=null)front.MatchEnded-=OnMatchEnded;}
        private void OnMatchEnded(int team)=>winner=team;

        private void OnGUI()
        {
            if(front==null)return;
            mainStyle ??= new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            smallStyle ??= new GUIStyle(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            resultStyle ??= new GUIStyle(GUI.skin.label){fontSize=34,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            float width=Mathf.Min(470f,Screen.width*.52f);float x=(Screen.width-width)*.5f;
            GUI.Box(new Rect(x,82,width,72),GUIContent.none);
            string timer=front.IsOvertime?"SUDDEN CONVERGENCE":$"{Mathf.FloorToInt(front.TimeRemaining/60f):00}:{Mathf.FloorToInt(front.TimeRemaining%60f):00}";
            GUI.Label(new Rect(x+10,86,width-20,27),timer,mainStyle);
            GUI.Label(new Rect(x+10,116,width-20,26),$"ZERO {front.ZeroControl:0.0}%   //   {front.LumenControl:0.0}% LUMEN",smallStyle);
            if(front.HasEnded&&winner>=0){Rect r=new Rect(Screen.width*.2f,Screen.height*.38f,Screen.width*.6f,120f);GUI.Box(r,GUIContent.none);GUI.Label(r,winner==0?"DIMENSION ZERO SIEGT":"LUMEN SIEGT",resultStyle);}
        }
    }
}
