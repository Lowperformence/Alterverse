using System.Collections;using UnityEngine;
namespace Alterverse.Match
{
public sealed class MatchOpening:MonoBehaviour
{
[SerializeField]private float messageSeconds=2.2f;
private const string Rules="SCORE KILLS TO WIN\nCAPTURE THE HOT ZONE FOR MECH SUPPORT\nCAPTURE PLANETS FOR +1 MINUTE";
private const string BomberWarning="WARNING: SPACE BOMBERS DAMAGE EVERY PLAYER";
private string display="";
private bool visible,ownsPause;
private GUIStyle title,shadow,rules,rulesShadow,warning,warningShadow;
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

    SetPhase("GO",.55f);
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

    bool headline=display=="REALITY COLLIDE";
    bool go=display=="GO";
    bool countdown=display=="3"||display=="2"||display=="1";
    int phaseSize=Mathf.RoundToInt((headline?58f:go?86f:104f)*uiScale);
    title.fontSize=phaseSize;
    shadow.fontSize=phaseSize;

    float progress=Mathf.Clamp01(phaseElapsed/Mathf.Max(.01f,phaseDuration));
    float fadeIn=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress/.18f));
    float fadeOut=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((progress-.78f)/.22f));
    float alpha=Mathf.Clamp01(fadeIn*fadeOut);
    float punch=1f+(.105f*(1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress/.32f))));

    float width=Mathf.Min(Screen.width*.88f,headline?980f*uiScale:620f*uiScale);
    float height=(headline?112f:150f)*uiScale;
    Rect rect=new((Screen.width-width)*.5f,(Screen.height-height)*.40f,width,height);

    Color before=GUI.color;
    Matrix4x4 matrix=GUI.matrix;

    // Quiet cinematic veil keeps the rules readable without bringing back a heavy UI panel.
    GUI.color=new Color(.008f,.012f,.025f,.46f*alpha);
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

    GUI.matrix=matrix;

    // Keep the instructions readable for the entire 3-2-1 sequence.
    // Their opacity is deliberately independent from the animated countdown phase alpha,
    // so changing from 3 -> 2 -> 1 cannot fade the tutorial out and back in.
    if(countdown)
    {
        const float tutorialAlpha=1f;
        float rulesWidth=Mathf.Min(Screen.width*.90f,820f*uiScale);
        float rulesHeight=118f*uiScale;
        float rulesY=rect.yMax+18f*uiScale;
        Rect rulesRect=new((Screen.width-rulesWidth)*.5f,rulesY,rulesWidth,rulesHeight);

        GUI.color=new Color(0f,0f,0f,.74f*tutorialAlpha);
        GUI.Label(new Rect(rulesRect.x+1.5f*uiScale,rulesRect.y+2f*uiScale,rulesRect.width,rulesRect.height),Rules,rulesShadow);
        GUI.color=new Color(.90f,.95f,1f,.94f*tutorialAlpha);
        GUI.Label(rulesRect,Rules,rules);

        Rect warningRect=new(rulesRect.x,rulesRect.yMax+3f*uiScale,rulesRect.width,32f*uiScale);
        GUI.color=new Color(0f,0f,0f,.78f*tutorialAlpha);
        GUI.Label(new Rect(warningRect.x+1.5f*uiScale,warningRect.y+2f*uiScale,warningRect.width,warningRect.height),BomberWarning,warningShadow);
        GUI.color=new Color(1f,.66f,.30f,.98f*tutorialAlpha);
        GUI.Label(warningRect,BomberWarning,warning);
    }

    // Small controlled energy sheen, only at the lower edge of the glyph area.
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

    rules=new GUIStyle(GUI.skin.label)
    {
        fontSize=Mathf.RoundToInt(20f*scale),
        fontStyle=FontStyle.Bold,
        alignment=TextAnchor.MiddleCenter,
        wordWrap=false,
        richText=false
    };
    rules.normal.textColor=Color.white;

    rulesShadow=new GUIStyle(rules);
    rulesShadow.normal.textColor=Color.black;

    warning=new GUIStyle(GUI.skin.label)
    {
        fontSize=Mathf.RoundToInt(21f*scale),
        fontStyle=FontStyle.Bold,
        alignment=TextAnchor.MiddleCenter,
        wordWrap=false,
        richText=false
    };
    warning.normal.textColor=Color.white;

    warningShadow=new GUIStyle(warning);
    warningShadow.normal.textColor=Color.black;
}
}
}
