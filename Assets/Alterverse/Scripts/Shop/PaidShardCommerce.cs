using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
namespace Alterverse.Shop
{
    // Paid currency is a server-owned wallet, deliberately separate from offline earned shards.
    public sealed class PaidShardCommerce:MonoBehaviour
    {
        [Serializable] public sealed class Snapshot {public long revision;public int balance;public bool voidfall;public int[] avatars;}
        [Serializable] sealed class Request {public string productId,purchaseToken,cosmeticId;}
        [Serializable] sealed class Response {public bool fulfilled;public Snapshot wallet;}
        public static PaidShardCommerce Instance{get;private set;}
        public static int Balance=>Instance!=null&&Instance.Ready?Instance.snapshot.balance:0;
        public bool Ready=>ready&&token!=null&&!string.IsNullOrEmpty(token());
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
            var c=Instance;c.generation++;c.ready=false;c.CancelPending();c.endpoint=httpsEndpoint.TrimEnd('/');c.token=accessToken;c.AccountHash=accountHash;c.ready=false;c.snapshot=new Snapshot();c.StartCoroutine(c.Sync());return c;
        }
        public void Refresh(){if(token!=null)StartCoroutine(Sync());}
        public static void Disconnect(){if(Instance==null)return;var c=Instance;c.generation++;c.ready=false;c.token=null;c.AccountHash=null;c.snapshot=new Snapshot();c.CancelPending();c.Status="Store account disconnected.";}
        void CancelPending(){StopAllCoroutines();var tasks=new List<TaskCompletionSource<bool>>(pending);pending.Clear();foreach(var task in tasks)task.TrySetResult(false);}
        void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
        IEnumerator Sync(){yield return Send("/wallet",new Request(),r=>{if(r!=null&&r.wallet!=null){Apply(r.wallet);ready=true;Status="Store account connected.";}});}
        public Task<bool> Fulfill(string product,string purchaseToken)
        {
            var task=new TaskCompletionSource<bool>();
            if(!Ready||!ShardPacks.Contains(product)||string.IsNullOrEmpty(purchaseToken)){task.SetResult(false);return task.Task;}
            pending.Add(task);StartCoroutine(Send("/google-play/fulfill",new Request{productId=product,purchaseToken=purchaseToken},r=>{bool ok=r!=null&&r.fulfilled&&r.wallet!=null;if(ok)Apply(r.wallet);pending.Remove(task);task.TrySetResult(ok);}));return task.Task;
        }
        public Task<bool> BuyCosmetic(string id)
        {
            var task=new TaskCompletionSource<bool>();if(!Ready){task.SetResult(false);return task.Task;}
            pending.Add(task);StartCoroutine(Send("/cosmetics/buy",new Request{cosmeticId=id},r=>{bool ok=r!=null&&r.fulfilled&&r.wallet!=null;if(ok)Apply(r.wallet);pending.Remove(task);task.TrySetResult(ok);}));return task.Task;
        }
        IEnumerator Send(string route,Request body,Action<Response> done)
        {
            int requestGeneration=generation;string credential=token?.Invoke();if(string.IsNullOrEmpty(credential)){Status="Sign in to reconnect the store.";done(null);yield break;}
            using(var request=new UnityWebRequest(endpoint+route,"POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));request.downloadHandler=new DownloadHandlerBuffer();request.timeout=20;
                request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+credential);
                yield return request.SendWebRequest();
                if(requestGeneration!=generation){done(null);yield break;}
                if(request.result!=UnityWebRequest.Result.Success){Status="Store connection interrupted. Purchases remain pending.";done(null);yield break;}
                Response response=null;try{response=JsonUtility.FromJson<Response>(request.downloadHandler.text);}catch(ArgumentException){Status="Invalid store response.";}
                if(response?.wallet==null||response.wallet.balance<0){done(null);yield break;}done(response);
            }
        }
        void Apply(Snapshot value)
        {
            if(value.revision<snapshot.revision)return;snapshot=value;
            // Paid entitlements belong only to this authenticated session; never write offline ownership keys.
        }
        void OnDestroy(){ready=false;generation++;CancelPending();if(Instance==this)Instance=null;}
    }
}
