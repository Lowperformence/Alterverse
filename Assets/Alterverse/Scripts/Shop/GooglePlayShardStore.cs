using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
                var account=Alterverse.Profile.AccountServerBootstrap.Instance;
                if(account==null||!account.GooglePlayAccountReady)
                    return account?.GoogleStatus??"Connect Google Play Games in Profile before purchasing shards.";
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
        readonly Dictionary<string,decimal> numericPrices=new();
        readonly Dictionary<string,string> currencies=new();
        public bool SubscriptionActive=>PaidShardCommerce.Instance?.SubscriptionActive==true;
        public string SubscriptionStatus=>SubscriptionActive?"Active · manage in Google Play":
            PaidShardCommerce.Instance?.SubscriptionsReady==true?"Renews monthly · cancel in Google Play":"Subscription setup in progress";

        // Compare current, purchasable prices in the same currency. Never invent a crossed-out price.
        public int SubscriptionDiscount
        {
            get
            {
                var sub=ShardPacks.Subscription;
                if(sub==null||sub.Amount<=0||!numericPrices.TryGetValue(sub.Id,out decimal recurring)||recurring<=0||
                   !currencies.TryGetValue(sub.Id,out string currency)||string.IsNullOrEmpty(currency))return 0;
                decimal best=decimal.MaxValue;
                foreach(var pack in ShardPacks.All)
                    if(pack.Amount>0&&numericPrices.TryGetValue(pack.Id,out decimal value)&&value>0&&
                       currencies.TryGetValue(pack.Id,out string code)&&code==currency)best=Math.Min(best,value/pack.Amount);
                return best==decimal.MaxValue?0:Math.Max(0,(int)decimal.Floor(100m*(1m-recurring/sub.Amount/best)));
            }
        }

        public void ManageSubscription()=>Application.OpenURL("https://play.google.com/store/account/subscriptions?sku=dimensions_shards_monthly&package=com.lowperformence.dimensionsfront");

        public string Price(string id)=>prices.TryGetValue(id,out string price)?price:"Unavailable";

        public bool CanBuy(string id)
        {
            if(ShardPacks.IsSubscription(id)&&(LocalBillingTestMode||
               PaidShardCommerce.Instance?.SubscriptionsReady!=true||SubscriptionActive))return false;
            bool productionReady=PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.PurchasesReady&&
                Alterverse.Profile.AccountServerBootstrap.Instance?.GooglePlayAccountReady==true;
            return Ready&&!Busy&&!PendingRecovery&&Alterverse.Profile.AccountServerBootstrap.Instance?.Busy!=true&&
                prices.ContainsKey(id)&&(LocalBillingTestMode||productionReady);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create(){if(FindAnyObjectByType<GooglePlayShardStore>()==null)new GameObject("DF_GooglePlayShardStore").AddComponent<GooglePlayShardStore>();}

        void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}

#if UNITY_ANDROID && !UNITY_EDITOR
        StoreController store;bool connecting,retryRequested;float nextConnect;
        const float StoreCallbackTimeout=30f;
        enum StartupStage { None, Connecting, Products, Purchases }
        StartupStage startupStage;float startupDeadline;
        int connectionGeneration;
        TaskCompletionSource<bool> connectionInterrupted;
        Action<List<Product>> onProducts;
        Action<ProductFetchFailed> onProductsFailed;
        Action<PendingOrder> onPending;
        Action<Order> onConfirmed;
        Action<FailedOrder> onFailed;
        Action<DeferredOrder> onDeferred;
        Action<Orders> onFetched;
        Action<PurchasesFetchFailureDescription> onFetchFailed;
        Action<StoreConnectionFailureDescription> onDisconnected;
        readonly HashSet<string> processing=new();
        readonly HashSet<string> unconfirmed=new();
        string connectedAccount;int connectedSession;

        bool CheckoutPrerequisitesReady()
        {
#if DF_GOOGLE_PLAY_SHARD_TEST
            return true;
#else
            return PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.PurchasesReady&&
                Alterverse.Profile.AccountServerBootstrap.Instance?.GooglePlayAccountReady==true;
#endif
        }

        bool SessionMatches()
        {
#if DF_GOOGLE_PLAY_SHARD_TEST
            return true;
#else
            var backend=PaidShardCommerce.Instance;
            return backend!=null&&backend.Ready&&backend.AccountHash==connectedAccount&&backend.SessionVersion==connectedSession&&
                Alterverse.Profile.AccountServerBootstrap.Instance?.GooglePlayAccountReady==true;
#endif
        }

        void Update()
        {
            // Native callbacks can be lost. Bound every startup stage, including
            // the Connect task, without abandoning a payment being verified.
            if(startupStage!=StartupStage.None&&Time.unscaledTime>=startupDeadline&&processing.Count==0&&!Busy)
            {
                string stage=startupStage==StartupStage.Connecting?"connection":startupStage==StartupStage.Products?"product loading":"purchase recovery";
                Status="Google Play "+stage+" timed out. Retrying…";
                ResetConnection();PendingRecovery=true;
                nextConnect=Time.unscaledTime+15;
                return;
            }
            if(retryRequested&&!connecting&&processing.Count==0&&Time.unscaledTime>=nextConnect)
            {
                ResetConnection();
                PendingRecovery=true;retryRequested=false;
            }

            if(store!=null&&!connecting&&!SessionMatches())
            {
                Ready=false;
                if(processing.Count==0)
                {
                    // Losing identity alone is not an outstanding payment. Keep actual recovery
                    // state, otherwise a clean Google account switch can never be restored.
                    PendingRecovery=PendingRecovery||unconfirmed.Count>0||Busy;
                    ResetConnection();unconfirmed.Clear();
                }
            }

            if(store==null&&!connecting&&Time.unscaledTime>=nextConnect&&CheckoutPrerequisitesReady())Initialize();
        }

        async void Initialize()
        {
            connecting=true;retryRequested=false;
            int attempt=++connectionGeneration;
            StoreController openingStore=null;
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
                openingStore=store;
                store.ProcessPendingOrdersOnPurchasesFetched(false);
                Bind(openingStore,attempt);
#if !DF_GOOGLE_PLAY_SHARD_TEST
                store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(PaidShardCommerce.Instance.AccountHash);
#endif
                BeginStartup(StartupStage.Connecting,"Connecting to Google Play…");
                var interrupted=new TaskCompletionSource<bool>();
                connectionInterrupted=interrupted;
                Task connection=openingStore.Connect();
                if(await Task.WhenAny(connection,interrupted.Task)!=connection)
                {
                    // Observe a possible late fault; it must never resume a retired
                    // controller or load prices into a newer account's connection.
                    _=ObserveRetiredConnection(connection);
                    return;
                }
                await connection;
                if(!Current(openingStore,attempt))return;
                connectionInterrupted=null;

                if(store!=openingStore||!SessionMatches())
                {
                    ResetConnection();
                    Status="Account changed. Reconnecting the store.";
                    return;
                }

                var definitions=new List<ProductDefinition>();
                foreach(var p in ShardPacks.All)definitions.Add(new ProductDefinition(p.Id,ProductType.Consumable));
                if(ShardPacks.Subscription!=null)definitions.Add(new ProductDefinition(ShardPacks.Subscription.Id,ProductType.Subscription));
                BeginStartup(StartupStage.Products,"Loading Google Play prices…");
                store.FetchProducts(definitions);
            }
            catch(Exception)
            {
                if(!Current(openingStore,attempt))return;
                Status="Google Play connection failed. Reconnect to retry.";
                ResetConnection();nextConnect=Time.unscaledTime+15;
            }
            finally{if(this!=null&&attempt==connectionGeneration)connecting=false;}
        }

        bool Current(StoreController source,int attempt)=>this!=null&&store==source&&attempt==connectionGeneration;
        static async Task ObserveRetiredConnection(Task task){try{await task;}catch(Exception){}}
        void BeginStartup(StartupStage stage,string message)
        {startupStage=stage;startupDeadline=Time.unscaledTime+StoreCallbackTimeout;Status=message;}
        void Bind(StoreController source,int attempt)
        {
            // Capture the actual controller. A late old callback cannot alter the
            // prices, pending-token set or status of a replacement connection.
            onProducts=p=>{if(Current(source,attempt))Products(p);};
            onProductsFailed=p=>{if(Current(source,attempt))ProductsFailed(p);};
            onPending=p=>{if(Current(source,attempt))Pending(p);};
            onConfirmed=p=>{if(Current(source,attempt))Confirmed(p);};
            onFailed=p=>{if(Current(source,attempt))Failed(p);};
            onDeferred=p=>{if(Current(source,attempt))Deferred(p);};
            onFetched=p=>{if(Current(source,attempt))Fetched(p);};
            onFetchFailed=p=>{if(Current(source,attempt))FetchFailed(p);};
            onDisconnected=p=>{if(Current(source,attempt))Disconnected(p);};
            source.OnProductsFetched+=onProducts;source.OnProductsFetchFailed+=onProductsFailed;
            source.OnPurchasePending+=onPending;source.OnPurchaseConfirmed+=onConfirmed;
            source.OnPurchaseFailed+=onFailed;source.OnPurchaseDeferred+=onDeferred;
            source.OnPurchasesFetched+=onFetched;source.OnPurchasesFetchFailed+=onFetchFailed;
            source.OnStoreDisconnected+=onDisconnected;
        }
        void ResetConnection()
        {
            ++connectionGeneration;
            Unbind();store=null;prices.Clear();numericPrices.Clear();currencies.Clear();Ready=false;Busy=processing.Count>0;
            connecting=false;retryRequested=false;startupStage=StartupStage.None;
            var interrupted=connectionInterrupted;connectionInterrupted=null;
            interrupted?.TrySetResult(false);
        }

        void Products(List<Product> products)
        {
            startupStage=StartupStage.None;
            prices.Clear();numericPrices.Clear();currencies.Clear();
            foreach(var p in products)
            {
                if(!p.availableToPurchase||!ShardPacks.IsProduct(p.definition.id))continue;
                // This catalog uses one monthly, recurring base plan, without trial or intro offers.
                // Refuse mismatched metadata rather than misstate a recurring price.
                if(ShardPacks.IsSubscription(p.definition.id)&&
                   (!(p.metadata is GoogleProductMetadata meta)||meta.subscriptionPeriod!="P1M"||
                    !string.IsNullOrEmpty(meta.freeTrialPeriod)||meta.introductoryPriceCycles>0))continue;
                prices[p.definition.id]=p.metadata.localizedPriceString;
                numericPrices[p.definition.id]=p.metadata.localizedPrice;
                currencies[p.definition.id]=p.metadata.isoCurrencyCode;
            }

            Ready=prices.Count>0;
#if DF_GOOGLE_PLAY_SHARD_TEST
            Status=Ready
                ?"TEST MODE: Google Play prices loaded. Purchases credit local test shards only."
                :"TEST MODE: products unavailable. Check Play Console product IDs and test-track install.";
#else
            Status=Ready?"Prices supplied by Google Play.":"Products unavailable. Retrying…";
#endif
            if(!Ready)ScheduleRetry();
            if(Ready)BeginStartup(StartupStage.Purchases,"Restoring Google Play purchases…");
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

        void ScheduleRetry()
        {
            startupStage=StartupStage.None;
            retryRequested=true;nextConnect=Time.unscaledTime+15;
            connectionInterrupted?.TrySetResult(false);
        }

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
            startupStage=StartupStage.None;
            if(processing.Count==0)unconfirmed.Clear();
            foreach(var order in orders.PendingOrders)
            {
                string token=order.Info.Google?.PurchaseToken;
                if(!string.IsNullOrEmpty(token))unconfirmed.Add(token);
            }
            PendingRecovery=unconfirmed.Count>0||orders.PendingOrders.Count>0;
            foreach(var order in orders.PendingOrders)Pending(order);
#if !DF_GOOGLE_PLAY_SHARD_TEST
            foreach(var order in orders.ConfirmedOrders)RestoreSubscription(order);
#endif
            if(Ready&&!PendingRecovery)Status=LocalBillingTestMode?
                "TEST MODE: Google Play connected. Purchases credit local test shards only.":
                "Google Play connected. Prices supplied by Google Play.";
        }

        async void RestoreSubscription(Order order)
        {
            var items=order.CartOrdered.Items();
            string token=order.Info.Google?.PurchaseToken;
            if(items.Count!=1||!ShardPacks.IsSubscription(items[0].Product.definition.id)||
               string.IsNullOrEmpty(token)||!processing.Add(token))return;
            Busy=true;unconfirmed.Add(token);PendingRecovery=true;
            try
            {
                var backend=PaidShardCommerce.Instance;
                if(!SessionMatches()||backend==null||!await backend.Fulfill(items[0].Product.definition.id,token))
                {Status="Subscription recovery pending. Retrying…";ScheduleRetry();return;}
                if(!SessionMatches())return;
                unconfirmed.Remove(token);PendingRecovery=unconfirmed.Count>0;
            }
            catch(Exception){Status="Subscription recovery pending. Retrying…";ScheduleRetry();}
            finally{processing.Remove(token);if(this!=null)Busy=processing.Count>0;}
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
                if(ShardPacks.IsSubscription(product)){Status="TEST MODE: subscriptions require server verification.";return;}
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
                if(!SessionMatches()||backend==null||backend.AccountHash!=connectedAccount||backend.SessionVersion!=session||!await backend.Fulfill(product,token))
                {
                    Status="Verification pending. Retrying purchase recovery…";
                    ScheduleRetry();return;
                }
                if(this==null||store==null||!SessionMatches()||backend==null||backend!=PaidShardCommerce.Instance||
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
            store.OnProductsFetched-=onProducts;
            store.OnProductsFetchFailed-=onProductsFailed;
            store.OnPurchasePending-=onPending;
            store.OnPurchaseConfirmed-=onConfirmed;
            store.OnPurchaseFailed-=onFailed;
            store.OnPurchaseDeferred-=onDeferred;
            store.OnPurchasesFetched-=onFetched;
            store.OnPurchasesFetchFailed-=onFetchFailed;
            store.OnStoreDisconnected-=onDisconnected;
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
            if(Busy||processing.Count>0)return;
            ResetConnection();PendingRecovery=true;
            Status="Reconnecting to Google Play…";
            retryRequested=false;nextConnect=0;
#endif
        }

        void OnDestroy()
        {
            if(Instance==this)Instance=null;
#if UNITY_ANDROID && !UNITY_EDITOR
            ResetConnection();
#endif
        }
    }
}


