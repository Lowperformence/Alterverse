using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Purchasing;
#endif
namespace Alterverse.Shop
{
    public sealed class GooglePlayShardStore:MonoBehaviour
    {
        public static GooglePlayShardStore Instance{get;private set;}
        public string Status{get;private set;}="Google Play purchases unavailable until store setup is complete.";
        public bool Ready{get;private set;}
        public bool Busy{get;private set;}
        public bool PendingRecovery{get;private set;}
        readonly Dictionary<string,string> prices=new();
        public string Price(string id)=>prices.TryGetValue(id,out string price)?price:"Unavailable";
        public bool CanBuy(string id)=>Ready&&!Busy&&!PendingRecovery&&prices.ContainsKey(id)&&PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.PurchasesReady;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Create(){if(FindAnyObjectByType<GooglePlayShardStore>()==null)new GameObject("DF_GooglePlayShardStore").AddComponent<GooglePlayShardStore>();}
        void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
#if UNITY_ANDROID && !UNITY_EDITOR
        StoreController store;bool connecting;float nextConnect;
        readonly HashSet<string> processing=new();
        readonly HashSet<string> unconfirmed=new();
        string connectedAccount;int connectedSession;
        void Update()
        {
            var backend=PaidShardCommerce.Instance;
            if(store!=null&&!connecting&&(backend==null||!backend.Ready||backend.AccountHash!=connectedAccount||backend.SessionVersion!=connectedSession))
            {
                Ready=false;
                if(processing.Count==0){Unbind();store=null;prices.Clear();unconfirmed.Clear();PendingRecovery=true;Busy=false;}
            }
            if(store==null&&!connecting&&Time.unscaledTime>=nextConnect&&PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.PurchasesReady)Initialize();
        }
        async void Initialize()
        {
            connecting=true;
            try
            {
                connectedAccount=PaidShardCommerce.Instance.AccountHash;connectedSession=PaidShardCommerce.Instance.SessionVersion;PendingRecovery=true;
                store=new StoreController("GooglePlay");store.ProcessPendingOrdersOnPurchasesFetched(false);store.OnProductsFetched+=Products;store.OnProductsFetchFailed+=ProductsFailed;
                store.OnPurchasePending+=Pending;store.OnPurchaseConfirmed+=Confirmed;store.OnPurchaseFailed+=Failed;store.OnPurchaseDeferred+=Deferred;
                store.OnPurchasesFetched+=Fetched;store.OnPurchasesFetchFailed+=FetchFailed;store.OnStoreDisconnected+=Disconnected;
                store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(PaidShardCommerce.Instance.AccountHash);
                var openingStore=store;
                await openingStore.Connect();if(this==null)return;
                var currentBackend=PaidShardCommerce.Instance;
                if(store!=openingStore||currentBackend==null||!currentBackend.Ready||currentBackend.AccountHash!=connectedAccount||currentBackend.SessionVersion!=connectedSession)
                {
                    Unbind();store=null;Ready=false;prices.Clear();Busy=false;
                    Status="Account changed. Reconnecting the store.";return;
                }
                var definitions=new List<ProductDefinition>();foreach(var p in ShardPacks.All)definitions.Add(new ProductDefinition(p.Id,ProductType.Consumable));store.FetchProducts(definitions);
            }
            catch(Exception){if(this==null)return;Status="Google Play connection failed. Reconnect to retry.";Unbind();store=null;nextConnect=Time.unscaledTime+15;}
            finally{if(this!=null)connecting=false;}
        }
        void Products(List<Product> products){prices.Clear();foreach(var p in products)if(p.availableToPurchase&&ShardPacks.Contains(p.definition.id))prices[p.definition.id]=p.metadata.localizedPriceString;Ready=prices.Count>0;Status=Ready?"Prices supplied by Google Play.":"Products unavailable.";store.FetchPurchases();}
        void ProductsFailed(ProductFetchFailed failure){Ready=false;Status="Store products unavailable.";}
        void Disconnected(StoreConnectionFailureDescription failure){Ready=false;Busy=false;Status="Store disconnected. Reconnect to retry.";}
        void FetchFailed(PurchasesFetchFailureDescription failure){PendingRecovery=true;Busy=processing.Count>0;Status="Purchase recovery failed. Reconnect to retry.";}
        void Confirmed(Order order){string token=order.Info.Google?.PurchaseToken;if(!(order is FailedOrder)&&token!=null)unconfirmed.Remove(token);PendingRecovery=unconfirmed.Count>0;Status=PendingRecovery?"Store confirmation pending. Reconnect to retry.":"Purchase completed.";}
        void Failed(FailedOrder order){Busy=false;Status=order.FailureReason==PurchaseFailureReason.UserCancelled?"Purchase cancelled.":"Purchase failed. No shards granted.";}
        void Deferred(DeferredOrder order){Busy=false;Status="Payment pending. Shards unlock after confirmation.";}
        void Fetched(Orders orders){foreach(var order in orders.PendingOrders){string token=order.Info.Google?.PurchaseToken;if(!string.IsNullOrEmpty(token))unconfirmed.Add(token);}PendingRecovery=unconfirmed.Count>0||orders.PendingOrders.Count>0;foreach(var order in orders.PendingOrders)Pending(order);}
        async void Pending(PendingOrder order)
        {
            string token=order.Info.Google?.PurchaseToken;if(string.IsNullOrEmpty(token)||!processing.Add(token))return;
            unconfirmed.Add(token);Busy=true;PendingRecovery=true;Status="Verifying purchase…";
            try
            {
                var items=order.CartOrdered.Items();if(items.Count!=1){Status="Unexpected order. Contact support.";return;}
                string product=items[0].Product.definition.id;
                var backend=PaidShardCommerce.Instance;
                int session=connectedSession;
                if(backend==null||backend.AccountHash!=connectedAccount||backend.SessionVersion!=session||!await backend.Fulfill(product,token)){Status="Verification pending. Reconnect to recover purchase.";return;}
                if(this==null||store==null||backend==null||backend!=PaidShardCommerce.Instance||backend.AccountHash!=connectedAccount||backend.SessionVersion!=session)return;
                // Server verified the token and durably credited the account wallet.
                // Consume/acknowledge only after that durable server credit.
                store.ConfirmPurchase(order);Status="Shards added to your balance.";
                Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            }
            catch(Exception){if(this==null)return;Status="Purchase retained for recovery. No local grant was made.";}
            finally{processing.Remove(token);if(this!=null)Busy=processing.Count>0;}
        }
        void Unbind(){if(store==null)return;store.OnProductsFetched-=Products;store.OnProductsFetchFailed-=ProductsFailed;store.OnPurchasePending-=Pending;store.OnPurchaseConfirmed-=Confirmed;store.OnPurchaseFailed-=Failed;store.OnPurchaseDeferred-=Deferred;store.OnPurchasesFetched-=Fetched;store.OnPurchasesFetchFailed-=FetchFailed;store.OnStoreDisconnected-=Disconnected;}
#endif
        public void Buy(string id)
        {
            if(!CanBuy(id))return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if(PaidShardCommerce.Instance.AccountHash!=connectedAccount||PaidShardCommerce.Instance.SessionVersion!=connectedSession){Ready=false;Status="Account changed. Reconnect the store.";return;}
            Busy=true;Status="Waiting for Google Play…";try{store.PurchaseProduct(id);}catch(Exception){Busy=false;Status="Could not open Google Play.";}
#endif
        }
        public void Reconnect()
        {
            PaidShardCommerce.Instance?.Refresh();
#if UNITY_ANDROID && !UNITY_EDITOR
            if(Busy||connecting||processing.Count>0)return;Unbind();store=null;prices.Clear();Ready=false;PendingRecovery=true;nextConnect=0;
#endif
        }
        void OnDestroy(){if(Instance==this)Instance=null;
#if UNITY_ANDROID && !UNITY_EDITOR
            Unbind();
#endif
        }
    }
}
