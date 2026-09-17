using Alterverse.Match;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed class DimensionsFrontHUD : MonoBehaviour
    {
        private DimensionsFront front;private GUIStyle mainStyle;private GUIStyle smallStyle;private GUIStyle resultStyle;private GUIStyle markerStyle;private int winner=-1;
        private readonly Color zeroColor=new(.55f,.08f,.82f),lumenColor=new(.08f,.72f,.78f);
        private void Start(){front=Object.FindAnyObjectByType<DimensionsFront>();if(front!=null)front.MatchEnded+=OnMatchEnded;}
        private void OnDestroy(){if(front!=null)front.MatchEnded-=OnMatchEnded;}
        private void OnMatchEnded(int team)=>winner=team;
        private void OnGUI()
        {
            if(front==null)return;mainStyle??=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};smallStyle??=new GUIStyle(GUI.skin.label){fontSize=14,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};resultStyle??=new GUIStyle(GUI.skin.label){fontSize=34,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};markerStyle??=new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            float width=Mathf.Min(520f,Screen.width*.58f),x=(Screen.width-width)*.5f,y=80f;GUI.Box(new Rect(x,y,width,88),GUIContent.none);string timer=front.IsOvertime?"SUDDEN CONVERGENCE":$"{Mathf.FloorToInt(front.TimeRemaining/60f):00}:{Mathf.FloorToInt(front.TimeRemaining%60f):00}";GUI.Label(new Rect(x+10,y+3,width-20,27),timer,mainStyle);
            float barX=x+24,barY=y+38,barW=width-48,barH=18;GUI.DrawTexture(new Rect(barX,barY,barW,barH),MakeTex(new Color(.08f,.08f,.1f,.9f)));float zeroW=barW*(front.ZeroControl/100f);GUI.DrawTexture(new Rect(barX,barY,zeroW,barH),MakeTex(zeroColor));GUI.DrawTexture(new Rect(barX+zeroW,barY,barW-zeroW,barH),MakeTex(lumenColor));float markerX=barX+zeroW;GUI.Label(new Rect(markerX-14,barY-8,28,34),"◆",markerStyle);GUI.Label(new Rect(x+8,y+59,width-16,23),$"ZERO  {front.ZeroControl:0}%        DIMENSIONS FRONT        {front.LumenControl:0}%  LUMEN",smallStyle);
            if(front.HasEnded&&winner>=0){Rect r=new Rect(Screen.width*.2f,Screen.height*.38f,Screen.width*.6f,120f);GUI.Box(r,GUIContent.none);GUI.Label(r,winner==0?"DIMENSION ZERO SIEGT":"LUMEN SIEGT",resultStyle);}
        }
        private static Texture2D MakeTex(Color color){Texture2D tex=new Texture2D(1,1);tex.SetPixel(0,0,color);tex.Apply();return tex;}
    }
}
