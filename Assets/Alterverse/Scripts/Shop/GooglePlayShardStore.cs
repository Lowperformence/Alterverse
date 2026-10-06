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

        public static bool LocalBillingTestMode
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && DF_GOOGLE_PLAY_SHARD_TEST
                return true;
#else
                return false;
#endif
            }
        }

        string status="Connecting to Google Play…";
        public string Status
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
#if DF_GOOGLE_PLAY_SHARD_TEST
                return status;
#else
                var backend=PaidShardCommerce.Instance;
                if(backend==null)return Alterverse.Profile.AccountServerBootstrap.Instance?.Status??"Account service unavailable.";
                if(!backend.Ready)return backend.Status;
                if(!backend.PurchasesReady)return "Shard purchases are unavailable: account server billing is not enabled.";
                return status;
#endif
#else
                return "Shard purchases require the Android Google Play build. Checkout is unavailable here.";
#endif
            }
            private set=>status=value;
        }

        public bool Ready{get;private set;}
        public bool Busy{get;private set;}
        public bool PendingRecovery{get;private set;}
        readonly Dictionary<string,string> prices=new();

        public string Price(string id)=>prices.TryGetValue(id,out string price)?price:"Unavailable";

        public bool CanBuy(string id)
        {
            bool productionReady=PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.PurchasesReady;
            return Ready&&!Busy&&!PendingRecovery&&prices.ContainsKey(id)&&(LocalBillingTestMode||productionReady);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create(){if(FindAnyObjectByType<GooglePlayShardStore>()==null)new GameObject("DF_GooglePlayShardStore").AddComponent<GooglePlayShardStore>();}

        void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}

#if UNITY_ANDROID && !UNITY_EDITOR
        StoreController store;bool connecting,retryRequested;float nextConnect;
        readonly HashSet<string> processing=new();
        readonly HashSet<string> unconfirmed=new();
        string connectedAccount;int connectedSession;

        bool CheckoutPrerequisitesReady()
        {
#if DF_GOOGLE_PLAY_SHARD_TEST
            return true;
#else
            return PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.PurchasesReady;
#endif
        }

        bool SessionMatches()
        {
#if DF_GOOGLE_PLAY_SHARD_TEST
            return true;
#else
            var backend=PaidShardCommerce.Instance;
            return backend!=null&&backend.Ready&&backend.AccountHash==connectedAccount&&backend.SessionVersion==connectedSession;
#endif
        }

        void Update()
        {
            if(retryRequested&&!connecting&&processing.Count==0&&Time.unscaledTime>=nextConnect)
            {
                Unbind();store=null;prices.Clear();Ready=false;Busy=false;
                PendingRecovery=true;retryRequested=false;
            }

            if(store!=null&&!connecting&&!SessionMatches())
            {
                Ready=false;
                if(processing.Count==0)
                {
                    Unbind();store=null;prices.Clear();unconfirmed.Clear();PendingRecovery=true;Busy=false;
                }
            }

            if(store==null&&!connecting&&Time.unscaledTime>=nextConnect&&CheckoutPrerequisitesReady())Initialize();
        }

        async void Initialize()
        {
            connecting=true;retryRequested=false;
            try
            {
#if DF_GOOGLE_PLAY_SHARD_TEST
                connectedAccount="internal-google-play-test";
                connectedSession=0;
                Status="TEST MODE: connecting directly to Google Play…";
#else
                connectedAccount=PaidShardCommerce.Instance.AccountHash;
                connectedSession=PaidShardCommerce.Instance.SessionVersion;
#endif
                PendingRecovery=true;
                store=new StoreController("GooglePlay");
                store.ProcessPendingOrdersOnPurchasesFetched(false);
                store.OnProductsFetched+=Products;
                store.OnProductsFetchFailed+=ProductsFailed;
                store.OnPurchasePending+=Pending;
                store.OnPurchaseConfirmed+=Confirmed;
                store.OnPurchaseFailed+=Failed;
                store.OnPurchaseDeferred+=Deferred;
                store.OnPurchasesFetched+=Fetched;
                store.OnPurchasesFetchFailed+=FetchFailed;
                store.OnStoreDisconnected+=Disconnected;
#if !DF_GOOGLE_PLAY_SHARD_TEST
                store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(PaidShardCommerce.Instance.AccountHash);
#endif
                var openingStore=store;
                await openingStore.Connect();
                if(this==null)return;

                if(store!=openingStore||!SessionMatches())
                {
                    Unbind();store=null;Ready=false;prices.Clear();Busy=false;
                    Status="Account changed. Reconnecting the store.";
                    return;
                }

                var definitions=new List<ProductDefinition>();
                foreach(var p in ShardPacks.All)definitions.Add(new ProductDefinition(p.Id,ProductType.Consumable));
                store.FetchProducts(definitions);
            }
            catch(Exception)
            {
                if(this==null)return;
                Status="Google Play connection failed. Reconnect to retry.";
                Unbind();store=null;nextConnect=Time.unscaledTime+15;
            }
            finally{if(this!=null)connecting=false;}
        }

        void Products(List<Product> products)
        {
            prices.Clear();
            foreach(var p in products)
                if(p.availableToPurchase&&ShardPacks.Contains(p.definition.id))
                    prices[p.definition.id]=p.metadata.localizedPriceString;

            Ready=prices.Count>0;
#if DF_GOOGLE_PLAY_SHARD_TEST
            Status=Ready
                ?"TEST MODE: Google Play prices loaded. Purchases credit local test shards only."
                :"TEST MODE: products unavailable. Check Play Console product IDs and test-track install.";
#else
            Status=Ready?"Prices supplied by Google Play.":"Products unavailable. Retrying…";
#endif
            if(!Ready)ScheduleRetry();
            store.FetchPurchases();
        }

        void ProductsFailed(ProductFetchFailed failure)
        {
            Ready=false;
#if DF_GOOGLE_PLAY_SHARD_TEST
            Status="TEST MODE: Google Play products unavailable. Check product activation and tester account.";
#else
            Status="Store products unavailable. Retrying…";
#endif
            ScheduleRetry();
        }

        void Disconnected(StoreConnectionFailureDescription failure)
        {
            Ready=false;Busy=processing.Count>0;
            Status="Store disconnected. Retrying…";
            ScheduleRetry();
        }

        void FetchFailed(PurchasesFetchFailureDescription failure)
        {
            PendingRecovery=true;Busy=processing.Count>0;
            Status="Purchase recovery failed. Retrying…";
            ScheduleRetry();
        }

        void ScheduleRetry(){retryRequested=true;nextConnect=Time.unscaledTime+15;}

        void Confirmed(Order order)
        {
            string token=order.Info.Google?.PurchaseToken;
            if(order is FailedOrder||string.IsNullOrEmpty(token))
            {
                PendingRecovery=true;Status="Store confirmation pending. Retrying…";ScheduleRetry();return;
            }
            unconfirmed.Remove(token);
            PendingRecovery=unconfirmed.Count>0;
#if DF_GOOGLE_PLAY_SHARD_TEST
            Status=PendingRecovery?"TEST MODE: another purchase is still pending.":"TEST MODE: purchase completed and test shards credited.";
#else
            Status=PendingRecovery?"Store confirmation pending.":"Purchase completed.";
#endif
        }

        void Failed(FailedOrder order)
        {
            Busy=false;
            Status=order.FailureReason==PurchaseFailureReason.UserCancelled
                ?"Purchase cancelled."
                :"Purchase failed. No shards granted.";
        }

        void Deferred(DeferredOrder order)
        {
            Busy=false;
            Status="Payment pending. Shards unlock after confirmation.";
        }

        void Fetched(Orders orders)
        {
            if(processing.Count==0)unconfirmed.Clear();
            foreach(var order in orders.PendingOrders)
            {
                string token=order.Info.Google?.PurchaseToken;
                if(!string.IsNullOrEmpty(token))unconfirmed.Add(token);
            }
            PendingRecovery=unconfirmed.Count>0||orders.PendingOrders.Count>0;
            foreach(var order in orders.PendingOrders)Pending(order);
        }

        async void Pending(PendingOrder order)
        {
            string token=order.Info.Google?.PurchaseToken;
            if(string.IsNullOrEmpty(token)||!processing.Add(token))return;

            unconfirmed.Add(token);
            Busy=true;PendingRecovery=true;
#if DF_GOOGLE_PLAY_SHARD_TEST
            Status="TEST MODE: confirming Google Play test purchase…";
#else
            Status="Verifying purchase…";
#endif
            try
            {
                var items=order.CartOrdered.Items();
                if(items.Count!=1){Status="Unexpected order. Contact support.";return;}
                string product=items[0].Product.definition.id;

#if DF_GOOGLE_PLAY_SHARD_TEST
                if(!Alterverse.Profile.PlayerWallet.GrantGooglePlayBillingTest(product,token))
                {
                    Status="TEST MODE: purchase received but local shard credit failed.";
                    return;
                }
                if(this==null||store==null)return;

                // Internal-license-test build only: confirm locally so a consumable can be
                // purchased repeatedly. Normal builds never compile this bypass.
                store.ConfirmPurchase(order);
                Status="TEST MODE: shards added locally from Google Play purchase.";
                Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
#else
                var backend=PaidShardCommerce.Instance;
                int session=connectedSession;
                if(backend==null||backend.AccountHash!=connectedAccount||backend.SessionVersion!=session||!await backend.Fulfill(product,token))
                {
                    Status="Verification pending. Retrying purchase recovery…";
                    ScheduleRetry();return;
                }
                if(this==null||store==null||backend==null||backend!=PaidShardCommerce.Instance||
                   backend.AccountHash!=connectedAccount||backend.SessionVersion!=session)return;

                // Server verified the token and durably credited the account wallet.
                // Consume/acknowledge only after that durable server credit.
                store.ConfirmPurchase(order);
                Status="Shards added to your balance.";
                Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
#endif
            }
            catch(Exception)
            {
                if(this==null)return;
                Status=LocalBillingTestMode
                    ?"TEST MODE: purchase retained for recovery. Tap reconnect."
                    :"Purchase retained for recovery. Retrying…";
                ScheduleRetry();
            }
            finally
            {
                processing.Remove(token);
                if(this!=null)Busy=processing.Count>0;
            }
        }

        void Unbind()
        {
            if(store==null)return;
            store.OnProductsFetched-=Products;
            store.OnProductsFetchFailed-=ProductsFailed;
            store.OnPurchasePending-=Pending;
            store.OnPurchaseConfirmed-=Confirmed;
            store.OnPurchaseFailed-=Failed;
            store.OnPurchaseDeferred-=Deferred;
            store.OnPurchasesFetched-=Fetched;
            store.OnPurchasesFetchFailed-=FetchFailed;
            store.OnStoreDisconnected-=Disconnected;
        }
#endif

        public void Buy(string id)
        {
            if(!CanBuy(id))return;
#if UNITY_ANDROID && !UNITY_EDITOR
#if !DF_GOOGLE_PLAY_SHARD_TEST
            if(PaidShardCommerce.Instance==null||
               PaidShardCommerce.Instance.AccountHash!=connectedAccount||
               PaidShardCommerce.Instance.SessionVersion!=connectedSession)
            {
                Ready=false;Status="Account changed. Reconnect the store.";return;
            }
#endif
            Busy=true;
            Status=LocalBillingTestMode?"TEST MODE: waiting for Google Play…":"Waiting for Google Play…";
            try{store.PurchaseProduct(id);}
            catch(Exception){Busy=false;Status="Could not open Google Play.";}
#endif
        }

        public void Reconnect()
        {
#if !DF_GOOGLE_PLAY_SHARD_TEST
            PaidShardCommerce.Instance?.Refresh();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            if(Busy||connecting||processing.Count>0)return;
            Unbind();store=null;prices.Clear();Ready=false;PendingRecovery=true;
            retryRequested=false;nextConnect=0;
#endif
        }

        void OnDestroy()
        {
            if(Instance==this)Instance=null;
#if UNITY_ANDROID && !UNITY_EDITOR
            Unbind();
#endif
        }
    }
}
