using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using UnityEngine;
namespace Alterverse.UI
{
    public static class GameInviteShare
    {
        public const string StoreUrl="https://play.google.com/store/apps/details?id=com.lowperformence.dimensionsfront";
        public static bool Busy{get;private set;}
        private static string status="";private static float statusUntil;
        public static string Status=>Time.unscaledTime<statusUntil?status:"";
        private static void ShowStatus(string value){status=value;statusUntil=Time.unscaledTime+8f;}
        public static string BuildMessage(string partyCode)
        {
            string text=GameLanguage.Text("Play Dimensions Front with me!")+"\n"+StoreUrl;
            string code=(partyCode??"").Trim().ToUpperInvariant();
            if(Regex.IsMatch(code,"^[A-Z0-9_-]{4,16}$"))
                text+="\n\n"+GameLanguage.Text("Party code: ")+code+"\n"+GameLanguage.Text("Open Play Online, then join the party with this code.");
            return text;
        }
        public static async Task<bool> ShareAsync()
        {
            if(Busy)return false;
            Busy=true;
            var party=OnlinePartyService.Instance;
            string code=party!=null&&party.InParty&&!party.MatchmakingLocked&&!party.IsPartyFull?party.JoinCode:"";
            string text=BuildMessage(code);
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
                string chooserTitle=GameLanguage.Text("INVITE TO GAME");
                var completion=new TaskCompletionSource<bool>();
                AndroidJavaObject activity=null;
                try
                {
                    using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        activity=unity.GetStatic<AndroidJavaObject>("currentActivity");
                    if(activity==null)throw new InvalidOperationException("No Android activity.");
                    var shareActivity=activity;
                    shareActivity.Call("runOnUiThread",new AndroidJavaRunnable(()=>
                    {
                        try
                        {
                            using var intent=new AndroidJavaObject("android.content.Intent","android.intent.action.SEND");
                            using var mime=intent.Call<AndroidJavaObject>("setType","text/plain");
                            using var subject=intent.Call<AndroidJavaObject>("putExtra","android.intent.extra.SUBJECT","Dimensions Front");
                            using var body=intent.Call<AndroidJavaObject>("putExtra","android.intent.extra.TEXT",text);
                            using var intents=new AndroidJavaClass("android.content.Intent");
                            using var chooser=intents.CallStatic<AndroidJavaObject>("createChooser",intent,chooserTitle);
                            shareActivity.Call("startActivity",chooser);
                            completion.TrySetResult(true);
                        }
                        catch(Exception){completion.TrySetResult(false);}
                        finally{shareActivity.Dispose();}
                    }));
                    // The activity reference stays alive until the UI-thread callback finishes.
                    activity=null;
                    if(await completion.Task)
                    {ShowStatus("Choose an app to share the invitation.");return true;}
                }
                finally{activity?.Dispose();}
#else
                await Task.CompletedTask;
#endif
            }
            catch(Exception){ /* A missing sharing app must not break the menu. */ }
            finally{Busy=false;}
            GUIUtility.systemCopyBuffer=text;
            ShowStatus("Invitation copied. Paste it into WhatsApp or another app.");
            return false;
        }
    }
}
