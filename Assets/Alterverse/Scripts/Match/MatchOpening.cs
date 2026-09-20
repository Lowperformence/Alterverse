using System.Collections;using UnityEngine;
namespace Alterverse.Match
{
public sealed class MatchOpening:MonoBehaviour
{
[SerializeField]private float messageSeconds=2.2f;
private string display="";
private bool visible,ownsPause;
private GUIStyle title,shadow;
private float phaseElapsed,phaseDuration;

private IEnumerator Start()
{
    while(Alterverse.UI.MainMenuController.IsVisible)yield return null;
    MatchClock.Set(PauseReason.Opening,true);ownsPause=true;visible=true;

    SetPhase("REALITY COLLIDE",messageSeconds);
    yield return WaitForeground(messageSeconds);

    for(int i=3;i>=1;i--)
    {
        SetPhase(i.ToString(),.72f);
        yield return WaitForeground(.72f);
    }

    SetPhase("LOS",.55f);
    yield return WaitForeground(.55f);
    visible=false;
    RestoreTimeScale();
}

private void SetPhase(string text,float duration)
{
    display=text;
    phaseElapsed=0f;
    phaseDuration=Mathf.Max(.01f,duration);
}

private IEnumerator WaitForeground(float seconds)
{
    float remaining=seconds;
    while(remaining>0)
    {
        yield return null;
        if(MatchClock.InBackground)continue;
        float dt=Time.unscaledDeltaTime;
        remaining-=dt;
        phaseElapsed+=dt;
    }
}

private void RestoreTimeScale(){if(!ownsPause)return;MatchClock.Set(PauseReason.Opening,false);ownsPause=false;}
private void OnDisable(){RestoreTimeScale();}
private void OnDestroy(){RestoreTimeScale();}

private void OnGUI()
{
    if(!visible||string.IsNullOrEmpty(display))return;

    float uiScale=Mathf.Clamp(Screen.height/900f,.72f,1.30f);
    EnsureStyles(uiScale);

    float progress=Mathf.Clamp01(phaseElapsed/Mathf.Max(.01f,phaseDuration));
    float fadeIn=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress/.18f));
    float fadeOut=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((progress-.78f)/.22f));
    float alpha=Mathf.Clamp01(fadeIn*fadeOut);
    float punch=1f+(.105f*(1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress/.32f))));

    bool headline=display=="REALITY COLLIDE";
    bool go=display=="LOS";
    float width=Mathf.Min(Screen.width*.82f,headline?980f*uiScale:520f*uiScale);
    float height=(headline?112f:150f)*uiScale;
    Rect rect=new((Screen.width-width)*.5f,(Screen.height-height)*.47f,width,height);

    Color before=GUI.color;
    Matrix4x4 matrix=GUI.matrix;

    // Quiet cinematic veil: no panel, no frame, no secondary copy.
    GUI.color=new Color(.008f,.012f,.025f,.38f*alpha);
    GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);

    // Thin ZERO/LUMEN energy accents replace the old grey box.
    float lineWidth=headline?Mathf.Min(250f*uiScale,width*.27f):Mathf.Min(95f*uiScale,width*.22f);
    float lineY=rect.center.y+height*.28f;
    DrawAccentLine(new Rect(rect.center.x-lineWidth-18f*uiScale,lineY,lineWidth,2f*uiScale),
        new Color(.52f,.27f,.95f,.68f*alpha),false);
    DrawAccentLine(new Rect(rect.center.x+18f*uiScale,lineY,lineWidth,2f*uiScale),
        new Color(.18f,.84f,1f,.68f*alpha),true);

    Vector2 center=rect.center;
    GUI.matrix=Matrix4x4.TRS(center,Quaternion.identity,Vector3.one*punch)*
               Matrix4x4.TRS(-center,Quaternion.identity,Vector3.one);

    shadow.fontSize=title.fontSize;
    GUI.color=new Color(0f,0f,0f,.72f*alpha);
    GUI.Label(new Rect(rect.x+2f*uiScale,rect.y+4f*uiScale,rect.width,rect.height),display,shadow);

    Color textColor=go?new Color(.94f,1f,1f,alpha):new Color(.92f,.96f,1f,alpha);
    GUI.color=textColor;
    GUI.Label(rect,display,title);

    // Small controlled energy sheen, only at the lower edge of the glyph area.
    GUI.matrix=matrix;
    float sheenWidth=(headline?160f:72f)*uiScale;
    float sheenX=Mathf.Lerp(rect.center.x-sheenWidth,rect.center.x,Mathf.SmoothStep(0f,1f,progress));
    GUI.color=new Color(.42f,.88f,1f,.22f*alpha);
    GUI.DrawTexture(new Rect(sheenX,rect.yMax-8f*uiScale,sheenWidth,1f*uiScale),Texture2D.whiteTexture);

    GUI.color=before;
}

private static void DrawAccentLine(Rect rect,Color color,bool reverse)
{
    Color before=GUI.color;
    GUI.color=color;
    GUI.DrawTexture(rect,Texture2D.whiteTexture);
    float cap=rect.height*2f;
    Rect end=reverse?new Rect(rect.xMax-cap,rect.y-cap*.25f,cap,rect.height*1.5f):
                     new Rect(rect.x,rect.y-cap*.25f,cap,rect.height*1.5f);
    GUI.DrawTexture(end,Texture2D.whiteTexture);
    GUI.color=before;
}

private void EnsureStyles(float scale)
{
    int headlineSize=Mathf.RoundToInt(58*scale);
    if(title!=null&&title.fontSize==headlineSize)return;

    title=new GUIStyle(GUI.skin.label)
    {
        fontSize=headlineSize,
        fontStyle=FontStyle.Bold,
        alignment=TextAnchor.MiddleCenter,
        wordWrap=false
    };
    title.normal.textColor=Color.white;

    shadow=new GUIStyle(title)
    {
        fontStyle=FontStyle.Bold
    };
    shadow.normal.textColor=Color.black;
}
}
}
