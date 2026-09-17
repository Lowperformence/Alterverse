using System.Collections;
using UnityEngine;

namespace Alterverse.Match
{
    /// <summary>Prototype match intro: freezes simulation, presents the reality collision, then releases combat.</summary>
    public sealed class MatchOpening : MonoBehaviour
    {
        [SerializeField]private float messageSeconds=2.2f;private string display="";private bool visible;private bool ownsPause;private float previousScale=1f;private GUIStyle title,sub;
        private IEnumerator Start(){previousScale=Time.timeScale;Time.timeScale=0f;ownsPause=true;visible=true;display="DIE DIMENSIONEN SIND AUFEINANDERGEPRALLT.|Kämpfe für deine Realität. Führe dein Universum zum Sieg.";yield return new WaitForSecondsRealtime(messageSeconds);for(int i=3;i>=1;i--){display=i.ToString();yield return new WaitForSecondsRealtime(.72f);}display="KÄMPFT!";yield return new WaitForSecondsRealtime(.55f);visible=false;RestoreTimeScale();}
        private void RestoreTimeScale(){if(!ownsPause)return;Time.timeScale=previousScale;ownsPause=false;}
        private void OnDisable(){RestoreTimeScale();}
        private void OnDestroy(){RestoreTimeScale();}
        private void OnGUI(){if(!visible)return;title??=new GUIStyle(GUI.skin.label){fontSize=34,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=true};sub??=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=true};string[] parts=display.Split('|');Rect panel=new Rect(Screen.width*.15f,Screen.height*.32f,Screen.width*.70f,Screen.height*.30f);GUI.Box(panel,GUIContent.none);if(parts.Length==1)GUI.Label(panel,parts[0],title);else{GUI.Label(new Rect(panel.x+20,panel.y+25,panel.width-40,80),parts[0],title);GUI.Label(new Rect(panel.x+30,panel.y+112,panel.width-60,60),parts[1],sub);}}
    }
}
