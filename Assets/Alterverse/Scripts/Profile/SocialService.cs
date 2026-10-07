using System;
using System.Collections;
using System.Text;
using Alterverse.Shop;
using UnityEngine;
using UnityEngine.Networking;

namespace Alterverse.Profile
{
    /// <summary>Account-authenticated social data. Never persists another account's social cache.</summary>
    public sealed class SocialService : MonoBehaviour
    {
        [Serializable] public sealed class Player
        {
            public string playerId,name,role;
            public int level,avatar,rank;
            public long xp;
        }
        [Serializable] public sealed class Clan
        {
            public string clanId,name,tag,description;
            public bool open;
            public int memberCount,totalLevel,rank;
            public Player[] members;
        }
        [Serializable] public sealed class Snapshot
        {
            public Player me;
            public Player[] friends,incoming,outgoing,blocked,applications,players;
            public Clan clan;
            public Clan[] clans;
            public string role;
            public string[] pendingClans;
            public int selfRank,total,offset;
        }
        [Serializable] public sealed class Request
        {
            public string playerId,clanId,query,name,tag,description,role;
            public int offset;
            public bool open=true;
        }
        [Serializable] sealed class Response { public bool fulfilled; public string message; public Snapshot social; }
        public static SocialService Instance { get; private set; }
        public Snapshot State { get; private set; }
        public Snapshot Search { get; private set; }
        public Snapshot Levels { get; private set; }
        public Snapshot Clans { get; private set; }
        public Snapshot ClanLevels { get; private set; }
        public string Status { get; private set; }="Sign in to load friends and clans.";
        public bool Busy { get; private set; }
        public bool Connected=>!string.IsNullOrEmpty(endpoint)&&!string.IsNullOrEmpty(Credential());
        string endpoint;
        Func<string> token;
        int generation;
        bool loadedSyncedProfile;

        public static void Connect(string address,Func<string> credential)
        {
            if(Instance==null)new GameObject("DF_SocialService").AddComponent<SocialService>();
            var service=Instance;
            service.ResetCache();
            service.endpoint=address.TrimEnd('/');service.token=credential;
            service.Status="Loading friends and clans…";
            service.Load("/social/state");
        }
        public static void Disconnect()
        {
            if(Instance==null)return;
            Instance.ResetCache();Instance.endpoint=null;Instance.token=null;
            Instance.Status="Sign in to load friends and clans.";
        }
        void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);
        }
        void ResetCache()
        {
            generation++;StopAllCoroutines();Busy=false;
            loadedSyncedProfile=false;
            State=Search=Levels=Clans=ClanLevels=null;
        }
        string Credential(){try{return token?.Invoke();}catch(Exception){return null;}}
        void Update()
        {
            // The account profile can arrive after the first social request at sign-in.
            if(!loadedSyncedProfile&&!Busy&&Connected&&PaidShardCommerce.Instance?.Ready==true&&AccountProfilePrefs.Attached)
            {loadedSyncedProfile=true;Load("/social/state");}
        }
        public void Load(string route,Request body=null)
        {
            if(Busy)return;
            if(!Connected){Status="Connect your account to use friends, levels and clans.";return;}
            Busy=true;StartCoroutine(Send(route,body??new Request(),generation));
        }
        IEnumerator Send(string route,Request body,int version)
        {
            // Publish the latest saved name/XP before ranking reads. A failed flush never
            // sends stale local progression as an ad-hoc score or mutates the profile.
            var commerce=PaidShardCommerce.Instance;
            if(commerce!=null&&commerce.Ready&&AccountProfilePrefs.Dirty)
            {
                var save=commerce.FlushProfileAsync();
                while(!save.IsCompleted)yield return null;
                if(version!=generation)yield break;
                if(save.IsFaulted||save.IsCanceled||!save.Result)
                {Status="Account save pending. Retry after synchronization.";Busy=false;yield break;}
            }
            using var request=new UnityWebRequest(endpoint+route,"POST");
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=20;
            string credential=Credential();
            if(string.IsNullOrEmpty(credential)){Status="Sign in to reconnect.";Busy=false;yield break;}
            request.SetRequestHeader("Content-Type","application/json");
            request.SetRequestHeader("Authorization","Bearer "+credential);
            yield return request.SendWebRequest();
            if(version!=generation)yield break;
            Response response=null;
            try{response=JsonUtility.FromJson<Response>(request.downloadHandler.text);}catch(Exception){}
            if(request.result!=UnityWebRequest.Result.Success||response==null||!response.fulfilled||response.social==null)
            {
                if(request.responseCode==401){State=Search=Levels=Clans=ClanLevels=null;Status="Sign in to reconnect.";}
                else Status=request.responseCode==403?"Your clan role does not allow this action.":
                    !string.IsNullOrEmpty(response?.message)?response.message:"Social server unavailable. Tap REFRESH to retry.";
                Busy=false;yield break;
            }
            if(route=="/social/players")Search=response.social;
            else if(route=="/social/levels")Levels=response.social;
            else if(route=="/social/clans")Clans=response.social;
            else if(route=="/social/clan-levels")ClanLevels=response.social;
            else
            {
                State=response.social;
                // Rankings and search results can have changed after membership actions.
                if(route!="/social/state"){Levels=ClanLevels=Clans=Search=null;}
            }
            Status="Synchronized.";Busy=false;
        }
        void OnDestroy(){if(Instance==this){ResetCache();Instance=null;}}
    }
}
