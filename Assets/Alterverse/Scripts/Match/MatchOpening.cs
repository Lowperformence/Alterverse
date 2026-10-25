using System.Collections;
using Alterverse.Multiplayer;
using Unity.Netcode;
using UnityEngine;
namespace Alterverse.Match
{
/// <summary>Authoritative opening clock. The lander supplies the complete presentation; no countdown text is drawn.</summary>
public sealed class MatchOpening:MonoBehaviour
{
[SerializeField]private float messageSeconds=2.2f;
private string display="";
private bool ownsPause,networkControlled,networkStateReceived;
private float phaseElapsed,phaseDuration;

public bool OpeningActive=>ownsPause;
public int PhaseCode=>CodeFor(display);
public float PhaseElapsed=>phaseElapsed;
public float PhaseDuration=>phaseDuration;

private void Awake()
{
    // Pause immediately, before any newly created actor can simulate its first frame.
    MatchClock.Set(PauseReason.Opening,true);ownsPause=true;
}

private IEnumerator Start()
{
    // In network matches the authoritative server owns the countdown. Remote clients never
    // advance gameplay from a local timer; they wait for replicated opening state instead.
    if(CoopAuthority.Active&&!CoopAuthority.LocalHasServerAuthority)
    {
        // A completion snapshot can arrive before this component's first Start.
        if(networkStateReceived&&!networkControlled)yield break;
        networkControlled=true;
        MatchClock.Set(PauseReason.Opening,true);ownsPause=true;
        while(networkControlled)yield return null;
        yield break;
    }

    // A headless dedicated server has no usable front-end and must never wait for menu visibility.
    if(!IsServerOnly())while(Alterverse.UI.MainMenuController.IsVisible||Alterverse.UI.MatchEntryCurtain.AwaitingArena)yield return null;

    // A verified roster is not a loaded arena. Keep the server paused until every
    // human has revealed their world, otherwise the insertion finishes behind loading.
    while(CoopAuthority.Active&&!CoopMatchStateSync.OpeningReady)yield return null;

    MatchClock.Set(PauseReason.Opening,true);ownsPause=true;

    SetPhase("REALITY COLLIDE",messageSeconds);
    yield return WaitForeground(messageSeconds);

    for(int i=3;i>=1;i--)
    {
        SetPhase(i.ToString(),.72f);
        yield return WaitForeground(.72f);
    }

    SetPhase("GO",.55f);
    yield return WaitForeground(.55f);
    RestoreTimeScale();
}

private void Update()
{
    if(networkControlled&&networkStateReceived&&!string.IsNullOrEmpty(display)&&!MatchClock.InBackground)
        phaseElapsed=Mathf.Min(phaseDuration,phaseElapsed+Time.unscaledDeltaTime);
}

public void ApplyNetworkOpeningState(bool active,int phase,float elapsed,float duration)
{
    if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority)return;
    networkStateReceived=true;

    if(!active)
    {
        networkControlled=false;
        RestoreTimeScale();
        return;
    }

    networkControlled=true;
    if(!ownsPause){MatchClock.Set(PauseReason.Opening,true);ownsPause=true;}
    display=TextFor(phase);
    phaseDuration=Mathf.Max(.01f,duration);
    phaseElapsed=Mathf.Clamp(elapsed,0f,phaseDuration);
}

private static bool IsServerOnly()
{
    NetworkManager manager=NetworkManager.Singleton;
    return manager!=null&&manager.IsListening&&manager.IsServer&&!manager.IsClient&&CoopAuthority.LocalHasServerAuthority;
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

private static int CodeFor(string text)=>text switch{"REALITY COLLIDE"=>4,"3"=>3,"2"=>2,"1"=>1,"GO"=>0,_=>-1};
private static string TextFor(int code)=>code switch{4=>"REALITY COLLIDE",3=>"3",2=>"2",1=>"1",0=>"GO",_=>""};

}
}
