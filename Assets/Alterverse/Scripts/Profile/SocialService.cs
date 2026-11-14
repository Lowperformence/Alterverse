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
            public int memberCount,totalLevel,rank,avatar,descriptionId;
            public Player[] members;
        }
        [Serializable] public sealed class Snapshot
        {
            public int stickerProtocolVersion;
            public StickerGift[] receivedStickers;
            public PartyInvitation[] partyInvites;
            public Player me;
            public PaidShardCommerce.Snapshot wallet;
            public Player[] friends,incoming,outgoing,blocked,applications,players;
            public Clan clan;
            public Clan[] clans;
            public string role;
            public string[] pendingClans;
            public string[] clanDescriptions;
            public int selfRank,total,offset,clanCreationCost,clanProtocolVersion,identityProtocolVersion;
            public bool hasClan;
            public bool chatEnabled,chatAccepted,leaderAccepted,isChatAdmin,unchanged;
            public string chatRulesVersion,leaderRulesVersion,chatRules,leaderRules,chatRevision,clanId,reportId,sentMessageId;
            public string[] reportReasons;
            public long mutedUntil,chatAcceptedAt,leaderAcceptedAt,expiresBefore;
            public int chatProtocolVersion,maxMessageLength,warnings;
            public Message[] messages;
            public Report[] reports;
        }
        [Serializable] public sealed class PartyInvitation
        {public string inviteId,senderName,partyCode;public long expiresAtUnix;}
        [Serializable] public sealed class StickerGift
        { public string giftId,senderName; public int senderHero,stickerId; public long receivedAtUnix; }
        [Serializable] public sealed class Message
        {public string messageId,playerId,name,text;public long createdAt;}
        [Serializable] public sealed class Evidence
        {public string playerId,name,text;public long createdAt;}
        [Serializable] public sealed class Report
        {public string reportId,clanId,messageId,reporterId,playerId,reason,status;public long createdAt,reviewedAt;public Evidence evidence;}
        [Serializable] public sealed class Request
        {
            public string playerId,clanId,query,name,tag,description,role,operationId;
            public string giftId,inviteId,partyCode,partySessionId;
            public int stickerId;
            public long expectedProfileRevision;
            public int avatar,descriptionId;
            public int offset;
            public bool open=true;
            public bool accepted;
            public string consentVersion,ageBand,text,messageId,reason,action,reportId,knownRevision;
            public int minutes=60;
        }
        [Serializable] sealed class Response { public bool fulfilled,conflict; public string message; public Snapshot social; }
        public static SocialService Instance { get; private set; }
        public Snapshot State { get; private set; }
        public Snapshot Search { get; private set; }
        public Snapshot Levels { get; private set; }
        public Snapshot Clans { get; private set; }
        public Snapshot ClanLevels { get; private set; }
        public Snapshot Chat { get; private set; }
        public Snapshot Reports { get; private set; }
        public Snapshot Friends { get; private set; }
        public string FriendsLastAction { get; private set; }
        public string SentOperationId { get; private set; }
        public string Status { get; private set; }="Sign in to load friends and clans.";
        public string ChatStatus { get; private set; }="Connecting to clan chat…";
        public bool Busy { get; private set; }
        public bool Connected=>!string.IsNullOrEmpty(endpoint)&&!string.IsNullOrEmpty(Credential());
        string ClanOperationKey=>"DF_CommerceClanCreate_"+PaidShardCommerce.Instance?.AccountHash;
        string StickerOperationKey=>"DF_CommerceStickerGift_"+PaidShardCommerce.Instance?.AccountHash;
        public bool HasPendingStickerGift=>PaidShardCommerce.Instance?.Ready==true&&PlayerPrefs.HasKey(StickerOperationKey);
        public bool SendStickerGift(Request draft)
        {
            if(Busy||!Connected||PaidShardCommerce.Instance?.Ready!=true)return false;
            Request body=draft;
            if(HasPendingStickerGift)
            {
                try{body=JsonUtility.FromJson<Request>(PlayerPrefs.GetString(StickerOperationKey));}
                catch(Exception){Status="Gift recovery needs support.";return false;}
            }
            if(body==null||string.IsNullOrEmpty(body.playerId))return false;
            if(string.IsNullOrEmpty(body.operationId))body.operationId=Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(StickerOperationKey,JsonUtility.ToJson(body));PlayerPrefs.Save();
            Load("/social/sticker/send",body);return true;
        }
        static bool FriendRoute(string route)=>route.StartsWith("/social/friend/",StringComparison.Ordinal)||route.StartsWith("/social/sticker/",StringComparison.Ordinal)||route.StartsWith("/social/party/",StringComparison.Ordinal);
        public bool HasPendingClanCreation=>PaidShardCommerce.Instance!=null&&!string.IsNullOrEmpty(PaidShardCommerce.Instance.AccountHash)&&PlayerPrefs.HasKey(ClanOperationKey);
        public Request PendingClanCreation
        {
            get
            {
                if(!HasPendingClanCreation)return null;
                try{return JsonUtility.FromJson<Request>(PlayerPrefs.GetString(ClanOperationKey));}catch(Exception){return null;}
            }
        }
        bool clanSpending;
        int clanSpendSession;
        AccountProfilePrefs.Entry[] clanSubmitted;
        string endpoint;
        Func<string> token;
        int generation;
        bool loadedSyncedProfile;
        System.Threading.Tasks.Task<bool> pendingReadSave;
        const float ReadSaveWaitSeconds=5f;
        float chatVisibleUntil,nextChatPoll;
        bool chatFocused=true,chatPaused,chatPolling;
        public void KeepChatOpen(){chatVisibleUntil=Time.unscaledTime+.3f;}
        void OnApplicationFocus(bool focused){chatFocused=focused;if(!focused)ClearChat();}
        void OnApplicationPause(bool paused){chatPaused=paused;if(paused)ClearChat();}
        void ClearChat(string message="Connecting to clan chat…"){Chat=null;ChatStatus=message;if(State!=null)State.messages=null;chatPolling=false;nextChatPoll=Time.unscaledTime+2f;}

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
            if(clanSpending)PaidShardCommerce.Instance?.EndSocialSpend(clanSpendSession,null,null);
            clanSpending=false;clanSubmitted=null;
            generation++;StopAllCoroutines();Busy=false;chatPolling=false;
            loadedSyncedProfile=false;pendingReadSave=null;
            State=Search=Levels=Clans=ClanLevels=null;
            Chat=Reports=Friends=null;FriendsLastAction=null;SentOperationId=null;chatVisibleUntil=nextChatPoll=0;
            ChatStatus="Connecting to clan chat…";
        }
        string Credential(){try{return token?.Invoke();}catch(Exception){return null;}}
        void Update()
        {
            // The account profile can arrive after the first social request at sign-in.
            if(!loadedSyncedProfile&&!Busy&&Connected&&PaidShardCommerce.Instance?.Ready==true&&AccountProfilePrefs.Attached)
            {loadedSyncedProfile=true;Load("/social/state");}
            if(!Busy&&!chatPolling&&Connected&&chatFocused&&!chatPaused&&Time.unscaledTime<chatVisibleUntil&&Time.unscaledTime>=nextChatPoll&&
                State?.chatEnabled==true&&State.chatAccepted&&State.clan!=null&&(State.role!="leader"||State.leaderAccepted))
            {
                if(Chat==null)
                {
                    nextChatPoll=Time.unscaledTime+3f;
                    Load("/social/chat/read",new Request{clanId=State.clan.clanId});
                }
                else
                {
                    nextChatPoll=Time.unscaledTime+4f;
                    chatPolling=true;
                    StartCoroutine(Send("/social/chat/read",new Request{clanId=State.clan.clanId,knownRevision=Chat.chatRevision},generation));
                }
            }
        }
        public void CreateClan(Request draft)
        {
            if(Busy||!Connected||PaidShardCommerce.Instance?.Ready!=true)return;
            if(State?.clanCreationCost!=500||State.clanProtocolVersion<3){Status="Update the account server to enable generated clan identities.";return;}
            Request body=PendingClanCreation??draft;
            if(State.chatEnabled)
            {
                if(!draft.accepted||draft.consentVersion!=State.leaderRulesVersion){Status="Accept the moderation terms first.";return;}
                body.accepted=draft.accepted;body.consentVersion=draft.consentVersion;
            }
            if(string.IsNullOrEmpty(body.operationId))body.operationId=Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(ClanOperationKey,JsonUtility.ToJson(body));PlayerPrefs.Save();
            Load("/social/clan/create",body);
        }
        public void Load(string route,Request body=null)
        {
            if(Busy)return;
            if(!Connected){Status="Connect your account to use friends, levels and clans.";return;}
            chatPolling=false;
            if(FriendRoute(route))Status="Synchronizing friends and gifts…";
            Busy=true;StartCoroutine(Send(route,body??new Request(),generation));
        }
        IEnumerator Send(string route,Request body,int version)
        {
            bool silentPoll=chatPolling&&!Busy&&route=="/social/chat/read"&&Chat!=null;
            try
            {
            // Try to publish current progression first. Social state and standings may
            // still read authoritative saved data when an unrelated profile save fails.
            var commerce=PaidShardCommerce.Instance;
            bool giftSpend=route=="/social/sticker/send"||route=="/social/sticker/claim";
            bool accountSpend=route=="/social/clan/create"||giftSpend;
            bool usingSavedProfile=false;
            bool needsProfile=route=="/social/state"||route=="/social/levels"||route=="/social/clan-levels"||accountSpend||route=="/social/friend/state";
            if(needsProfile&&commerce!=null&&commerce.Ready&&AccountProfilePrefs.Dirty)
            {
                // Read-only requests can use saved server data if a local save stalls.
                // Reuse the pending task to avoid starting a new save on every refresh.
                if(pendingReadSave!=null&&pendingReadSave.IsCompleted)pendingReadSave=null;
                var save=accountSpend?commerce.FlushProfileAsync():
                    pendingReadSave??(pendingReadSave=commerce.FlushProfileAsync());
                float deadline=Time.realtimeSinceStartup+ReadSaveWaitSeconds;
                while(!save.IsCompleted&&(accountSpend||Time.realtimeSinceStartup<deadline))
                    yield return null;
                if(version!=generation){chatPolling=false;yield break;}
                if(!accountSpend&&save.IsCompleted)pendingReadSave=null;
                if(!save.IsCompleted||save.IsFaulted||save.IsCanceled||!save.Result)
                {
                    if(accountSpend)
                    {Status="Account save pending. Retry after synchronization.";Busy=false;chatPolling=false;yield break;}
                    usingSavedProfile=true;
                }
            }
            if(accountSpend)
            {
                if(commerce==null||!commerce.TryBeginSocialSpend())
                {Status="Account busy. Retry after synchronization.";Busy=false;chatPolling=false;yield break;}
                clanSpending=true;clanSpendSession=commerce.SessionVersion;
                clanSubmitted=AccountProfilePrefs.Capture();body.expectedProfileRevision=AccountProfilePrefs.Revision;
            }
            using var request=new UnityWebRequest(endpoint+route,"POST");
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=20;
            string credential=Credential();
            if(string.IsNullOrEmpty(credential))
            {
                if(clanSpending)commerce?.EndSocialSpend(clanSpendSession,null,null);
                clanSpending=false;Status="Sign in to reconnect.";
                if(route.StartsWith("/social/chat/",StringComparison.Ordinal))ClearChat(Status);
                Busy=false;chatPolling=false;yield break;
            }
            request.SetRequestHeader("Content-Type","application/json");
            request.SetRequestHeader("Authorization","Bearer "+credential);
            yield return request.SendWebRequest();
            if(version!=generation){chatPolling=false;yield break;}
            Response response=null;
            try{response=JsonUtility.FromJson<Response>(request.downloadHandler.text);}catch(Exception){}
            if(clanSpending)
            {
                commerce?.EndSocialSpend(clanSpendSession,response?.fulfilled==true?response.social?.wallet:null,clanSubmitted);
                clanSpending=false;clanSubmitted=null;
            }
            if(request.result!=UnityWebRequest.Result.Success||response==null||!response.fulfilled||response.social==null)
            {
                if(route=="/social/sticker/send"&&(request.responseCode==400||request.responseCode==403))
                {PlayerPrefs.DeleteKey(StickerOperationKey);PlayerPrefs.Save();}
                if(giftSpend)commerce?.Refresh();
                if(route=="/social/clan/create")
                {
                    if(request.responseCode==400&&!(response?.message??"").StartsWith("Too many",StringComparison.Ordinal))
                    {PlayerPrefs.DeleteKey(ClanOperationKey);PlayerPrefs.Save();}
                    commerce?.Refresh();
                }
                if(request.responseCode==409){Status="Account changed. Refreshing your balance; retry after synchronization.";commerce?.Refresh();}
                else if(request.responseCode==401){State=Search=Levels=Clans=ClanLevels=Reports=Friends=null;ClearChat();Status="Sign in to reconnect.";}
                else Status=!string.IsNullOrEmpty(response?.message)?response.message:
                    request.responseCode==403?"Your clan role does not allow this action.":"Social server unavailable. Tap REFRESH to retry.";
                if(route.StartsWith("/social/chat/",StringComparison.Ordinal))
                {
                    ClearChat(Status);nextChatPoll=Time.unscaledTime+5f;
                    if(request.responseCode==403){loadedSyncedProfile=false;State=null;Reports=null;}
                }
                Busy=false;chatPolling=false;yield break;
            }
            if(FriendRoute(route))
            {
                var wallet=response.social.wallet;
                if(response.social.stickerProtocolVersion!=1||giftSpend&&(wallet==null||wallet.accountHash!=commerce?.AccountHash||wallet.profile==null))
                {Status="Account server update required for friends and gifts.";commerce?.Refresh();Busy=false;chatPolling=false;yield break;}
                if(route=="/social/sticker/send")
                {PlayerPrefs.DeleteKey(StickerOperationKey);PlayerPrefs.Save();}
                Friends=response.social;
                FriendsLastAction=route;
                Status=route=="/social/sticker/send"?"Gift delivered to your friend.":route=="/social/sticker/claim"?"Gift converted to Shards and saved.":"Friends and gifts synchronized.";
                Busy=false;chatPolling=false;yield break;
            }
            // Unity's JSON serializer may materialize null class fields as empty objects.
            // An actual server-issued ID is required for membership and wallet data.
            if(response.social.clanProtocolVersion>=2&&!response.social.hasClan||response.social.clan!=null&&string.IsNullOrEmpty(response.social.clan.clanId))response.social.clan=null;
            if(route=="/social/clan/create")
            {
                if(response.social.wallet==null||string.IsNullOrEmpty(response.social.wallet.accountHash))
                {Status="Clan response incomplete. Refresh your account before retrying.";commerce?.Refresh();Busy=false;chatPolling=false;yield break;}
                PlayerPrefs.DeleteKey(ClanOperationKey);PlayerPrefs.Save();
            }
            CleanSnapshot(response.social);
            if(route=="/social/chat/admin/reports")Reports=response.social;
            else if(route=="/social/chat/admin/action"||route=="/social/chat/admin/delivery")
            {Reports=null;ClearChat();loadedSyncedProfile=false;}
            else if(route=="/social/chat/read"&&response.social.unchanged)
            {
                if(Chat!=null&&Chat.clanId==response.social.clanId&&Chat.messages!=null)
                {
                    var kept=new System.Collections.Generic.List<Message>();
                    foreach(var message in Chat.messages)if(message.createdAt>=response.social.expiresBefore)kept.Add(message);
                    Chat.messages=kept.ToArray();
                }
            }
            else if(route.StartsWith("/social/chat/",StringComparison.Ordinal)&&response.social.messages!=null)
            {Chat=response.social;State=response.social;}
            else if(route=="/social/players")Search=response.social;
            else if(route=="/social/levels")Levels=response.social;
            else if(route=="/social/clans")Clans=response.social;
            else if(route=="/social/clan-levels")ClanLevels=response.social;
            else
            {
                if(State?.clan?.clanId!=response.social.clan?.clanId||route=="/social/block"||route=="/social/unblock")ClearChat();
                State=response.social;
                // Rankings and search results can have changed after membership actions.
                if(route!="/social/state"){Levels=ClanLevels=Clans=Search=null;}
            }
            if(route=="/social/chat/send")SentOperationId=body.operationId;
            if(Chat!=null&&route.StartsWith("/social/chat/",StringComparison.Ordinal))ChatStatus="Connected to clan chat.";
            if(route=="/social/chat/report")nextChatPoll=Time.unscaledTime+5f;
            if(!silentPoll)
                Status=route=="/social/chat/report"?"Report saved for the game administration.":
                    usingSavedProfile?"Showing saved server data. Account synchronization pending.":"Synchronized.";
            Busy=false;chatPolling=false;
            }
            finally
            {
                // An unexpected exception must not leave Refresh permanently disabled.
                if(version==generation)
                {
                    if(!silentPoll&&Busy)
                    {Busy=false;Status="Social request interrupted. Tap REFRESH to retry.";}
                    chatPolling=false;
                }
            }
        }
        // Also protects displays during a staggered client/server rollout.
        internal static void CleanSnapshot(Snapshot value)
        {
            void CleanPlayer(Player player)
            {if(player!=null)player.name=SafeIdentityCatalog.DisplayPlayerName(player.name,player.playerId);}
            void CleanPlayers(Player[] players)
            {if(players!=null)foreach(var player in players)CleanPlayer(player);}
            void CleanClan(Clan clan)
            {
                if(clan==null)return;
                clan.name=SafeIdentityCatalog.DisplayClanName(clan.name,clan.clanId);
                clan.tag=SafeIdentityCatalog.DisplayClanTag(clan.tag,clan.clanId);
                clan.descriptionId=SafeIdentityCatalog.DescriptionIndex(clan.description);
                clan.description=SafeIdentityCatalog.Descriptions[clan.descriptionId];
                CleanPlayers(clan.members);
            }
            CleanPlayer(value.me);CleanPlayers(value.friends);CleanPlayers(value.incoming);
            CleanPlayers(value.outgoing);CleanPlayers(value.blocked);CleanPlayers(value.applications);CleanPlayers(value.players);
            CleanClan(value.clan);
            if(value.clans!=null)foreach(var clan in value.clans)CleanClan(clan);
            value.clanDescriptions=new string[SafeIdentityCatalog.Descriptions.Count];
            for(int i=0;i<value.clanDescriptions.Length;i++)value.clanDescriptions[i]=SafeIdentityCatalog.Descriptions[i];
        }
        void OnDestroy(){if(Instance==this){ResetCache();Instance=null;}}
    }
}





