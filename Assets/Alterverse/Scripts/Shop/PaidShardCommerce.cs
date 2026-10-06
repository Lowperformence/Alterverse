using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alterverse.Profile;
using UnityEngine;
using UnityEngine.Networking;
namespace Alterverse.Shop
{
    // Google-verified purchases are held server-side only until they can be claimed safely
    // into the game's single shard balance. Players never see separate shard currencies.
    public sealed class PaidShardCommerce:MonoBehaviour
    {
        [Serializable] public sealed class Snapshot {public long revision;public int balance;public bool voidfall;public int[] avatars;public string claimId;public int claimAmount;}
        [Serializable] sealed class Request {public string productId,purchaseToken,cosmeticId,claimId;}
        [Serializable] sealed class Response {public bool fulfilled;public Snapshot wallet;}
        public static PaidShardCommerce Instance{get;private set;}
        public static event Action EntitlementsChanged;
        public static int Balance=>Instance!=null&&Instance.Ready?Instance.snapshot.balance:0;
        public bool Ready=>ready&&!string.IsNullOrEmpty(ReadCredential());
        public string AccountHash{get;private set;}
        public int SessionVersion=>generation;
        public static bool OwnsVoidfall=>Instance!=null&&Instance.Ready&&Instance.snapshot.voidfall;
        public static bool OwnsAvatar(int id)=>Instance!=null&&Instance.Ready&&Instance.snapshot.avatars!=null&&Array.IndexOf(Instance.snapshot.avatars,id)>=0;
        readonly HashSet<TaskCompletionSource<bool>> pending=new();
        string endpoint;Func<string> token;bool ready;int generation;Snapshot snapshot=new();
        public string Status{get;private set;}="Store account not connected.";
        public static PaidShardCommerce Connect(string httpsEndpoint,Func<string> accessToken,string accountHash)
        {
            if(!Uri.TryCreate(httpsEndpoint,UriKind.Absolute,out var uri)||uri.Scheme!="https"||string.IsNullOrWhiteSpace(accountHash)||accountHash.Length>64)throw new ArgumentException("HTTPS commerce endpoint and obfuscated account ID required");
            if(Instance==null)new GameObject("DF_PaidShardCommerce").AddComponent<PaidShardCommerce>();
            var c=Instance;c.generation++;c.ready=false;c.CancelPending();c.endpoint=httpsEndpoint.TrimEnd('/');c.token=accessToken;c.AccountHash=accountHash;c.ready=false;c.snapshot=new Snapshot();EntitlementsChanged?.Invoke();c.StartCoroutine(c.Sync());return c;
        }
        public void Refresh(){if(token!=null)StartCoroutine(Sync());}
        public static void Disconnect(){if(Instance==null)return;var c=Instance;c.generation++;c.ready=false;c.token=null;c.AccountHash=null;c.snapshot=new Snapshot();c.CancelPending();c.Status="Store account disconnected.";EntitlementsChanged?.Invoke();}
        void CancelPending(){StopAllCoroutines();var tasks=new List<TaskCompletionSource<bool>>(pending);pending.Clear();foreach(var task in tasks)task.TrySetResult(false);}
        void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
        IEnumerator Sync()
        {
            Response response=null;
            yield return Send("/wallet",new Request(),r=>response=r);
            if(response==null||response.wallet==null)yield break;
            Apply(response.wallet);ready=true;EntitlementsChanged?.Invoke();Status="Store account connected.";
            Debug.Log("DIMENSIONS FRONT COMMERCE: authenticated wallet connected.");
            bool claimed=false;
            yield return ClaimVerifiedShards(ok=>claimed=ok);
            if(claimed)Status="Shard balance synchronized.";
        }
        public Task<bool> Fulfill(string product,string purchaseToken)
        {
            var task=new TaskCompletionSource<bool>();
            if(!Ready||!ShardPacks.Contains(product)||string.IsNullOrEmpty(purchaseToken)){task.SetResult(false);return task.Task;}
            pending.Add(task);StartCoroutine(FulfillAndClaim(product,purchaseToken,task));return task.Task;
        }
        IEnumerator FulfillAndClaim(string product,string purchaseToken,TaskCompletionSource<bool> task)
        {
            Response response=null;
            yield return Send("/google-play/fulfill",new Request{productId=product,purchaseToken=purchaseToken},r=>response=r);
            bool ok=response!=null&&response.fulfilled&&response.wallet!=null;
            if(ok)
            {
                Apply(response.wallet);
                bool claimed=false;
                yield return ClaimVerifiedShards(value=>claimed=value);
                ok=claimed;
            }
            pending.Remove(task);task.TrySetResult(ok);
        }
        public Task<bool> BuyCosmetic(string id)
        {
            var task=new TaskCompletionSource<bool>();if(!Ready){task.SetResult(false);return task.Task;}
            pending.Add(task);StartCoroutine(Send("/cosmetics/buy",new Request{cosmeticId=id},r=>{bool ok=r!=null&&r.fulfilled&&r.wallet!=null;if(ok)Apply(r.wallet);pending.Remove(task);task.TrySetResult(ok);}));return task.Task;
        }
        IEnumerator ClaimVerifiedShards(Action<bool> done)
        {
            // Drain retry-safe claims until the verified server buffer is empty.
            // This also handles a new Google purchase arriving while an older claim was still awaiting acknowledgement.
            for(int pass=0;pass<4;pass++)
            {
                Response response=null;
                yield return Send("/wallet/claim",new Request(),r=>response=r);
                if(response==null||response.wallet==null){done(false);yield break;}
                Apply(response.wallet);

                string claimId=response.wallet.claimId;
                int amount=response.wallet.claimAmount;
                if(string.IsNullOrEmpty(claimId)||amount<=0)
                {
                    Status="Shard balance synchronized.";
                    done(true);yield break;
                }

                string appliedKey="DF_ShardClaim_"+AccountHash;
                try
                {
                    if(PlayerPrefs.GetString(appliedKey,"")!=claimId)
                    {
                        ProfileTransaction.Commit(
                            new ProfileTransaction.Write("DF_Shards",ProfileTransaction.Add(PlayerWallet.Balance,amount)),
                            new ProfileTransaction.Write(appliedKey,claimId));
                    }
                }
                catch(Exception)
                {
                    Status="Verified shards are waiting to be synchronized.";
                    done(false);yield break;
                }

                Response ack=null;
                yield return Send("/wallet/claim/ack",new Request{claimId=claimId},r=>ack=r);
                if(ack==null)
                {
                    Status="Shards added. Store sync will finish on reconnect.";
                    done(true);yield break;
                }
                if(ack.wallet!=null)Apply(ack.wallet);
            }

            // More than four queued claims is abnormal; keep the local grants safe and finish on the next refresh.
            Status="Shards added. Finishing store sync on reconnect.";
            done(true);
        }

        IEnumerator Send(string route,Request body,Action<Response> done)
        {
            int requestGeneration=generation;string credential=ReadCredential();if(string.IsNullOrEmpty(credential)){Status="Sign in to reconnect the store.";done(null);yield break;}
            using(var request=new UnityWebRequest(endpoint+route,"POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));request.downloadHandler=new DownloadHandlerBuffer();request.timeout=20;
                request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+credential);
                yield return request.SendWebRequest();
                if(requestGeneration!=generation){done(null);yield break;}
                if(request.result!=UnityWebRequest.Result.Success){if(request.responseCode==401||request.responseCode==403){ready=false;snapshot=new Snapshot();}Status="Store connection interrupted. Purchases remain pending.";done(null);yield break;}
                Response response=null;try{response=JsonUtility.FromJson<Response>(request.downloadHandler.text);}catch(ArgumentException){Status="Invalid store response.";}
                if(response?.wallet==null||response.wallet.balance<0){done(null);yield break;}done(response);
            }
        }
        string ReadCredential()
        {
            try{return token?.Invoke();}
            catch(Exception)
            {
                // External login/refresh providers may fail; never throw from a shop getter.
                ready=false;Status="Sign in to reconnect the store.";return null;
            }
        }
        void Apply(Snapshot value)
        {
            if(value.revision<snapshot.revision)return;snapshot=value;EntitlementsChanged?.Invoke();
            // Paid entitlements belong only to this authenticated session; never write offline ownership keys.
        }
        void OnDestroy(){ready=false;generation++;CancelPending();if(Instance==this)Instance=null;EntitlementsChanged?.Invoke();}
    }
}
