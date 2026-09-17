using System;using UnityEngine;
namespace Alterverse.Multiplayer
{
public enum MultiplayerAvailability{LocalOnly,Ready,Connecting,InSession,Unavailable}
public interface IMultiplayerProvider
{
string ProviderName{get;}bool IsAvailable{get;}bool IsConnected{get;}void Connect(Action<bool,string> completed);void Disconnect();
}
public sealed class MultiplayerGate:MonoBehaviour
{
public static MultiplayerGate Instance{get;private set;}public MultiplayerAvailability State{get;private set;}=MultiplayerAvailability.LocalOnly;public string Status{get;private set;}="LOCAL PLAY READY";public bool MultiplayerEnabled=>provider!=null&&provider.IsAvailable;private IMultiplayerProvider provider;
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]private static void Ensure(){if(Instance==null)new GameObject("DF_MultiplayerGate").AddComponent<MultiplayerGate>();}
private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);SetLocalOnly();}
public void RegisterProvider(IMultiplayerProvider value){provider=value;if(provider==null||!provider.IsAvailable){State=MultiplayerAvailability.Unavailable;Status="ONLINE SERVICE UNAVAILABLE";return;}State=MultiplayerAvailability.Ready;Status="ONLINE READY • "+provider.ProviderName.ToUpperInvariant();}
public void SetLocalOnly(){provider=null;State=MultiplayerAvailability.LocalOnly;Status="LOCAL PLAY READY";}
public void Connect(Action<bool,string> completed=null){if(provider==null||!provider.IsAvailable){State=MultiplayerAvailability.LocalOnly;Status="ONLINE COMING LATER • LOCAL PLAY READY";completed?.Invoke(false,Status);return;}State=MultiplayerAvailability.Connecting;Status="CONNECTING";provider.Connect((ok,message)=>{State=ok?MultiplayerAvailability.InSession:MultiplayerAvailability.Ready;Status=string.IsNullOrEmpty(message)?(ok?"ONLINE SESSION READY":"ONLINE READY"):message;completed?.Invoke(ok,Status);});}
public void Disconnect(){provider?.Disconnect();State=provider!=null&&provider.IsAvailable?MultiplayerAvailability.Ready:MultiplayerAvailability.LocalOnly;Status=State==MultiplayerAvailability.Ready?"ONLINE READY":"LOCAL PLAY READY";}
private void OnDestroy(){if(Instance==this)Instance=null;}
}
}
