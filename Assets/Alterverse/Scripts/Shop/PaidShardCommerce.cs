using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alterverse.Profile;
using UnityEngine;
using UnityEngine.Networking;
namespace Alterverse.Shop
{
    public sealed class PaidShardCommerce : MonoBehaviour
    {
        [Serializable] public sealed class Snapshot
        {
            public long revision, profileRevision;
            public int balance;
            public bool voidfall,billingReady,subscriptionsReady,subscriptionActive;
            public int[] avatars;
            public string[] owned;
            public AccountProfilePrefs.Entry[] profile;
            public string operationId,accountHash,minClientVersion;
            public int minAndroidVersionCode;
        }
        [Serializable] sealed class Request
        {
            public string productId,purchaseToken,cosmeticId,operationId;
            public long expectedProfileRevision;
            public AccountProfilePrefs.Entry[] profile;
        }
        [Serializable] sealed class Response { public bool fulfilled; public Snapshot wallet; }
        [Serializable] sealed class PurchaseOperation { public string id,sku; }
        public static PaidShardCommerce Instance { get; private set; }
        public static event Action EntitlementsChanged;
        public static int Balance => Instance != null && Instance.Ready ? Instance.snapshot.balance : 0;
        public static bool Spending => Instance != null && (Instance.spending || Instance.socialSpending || Instance.fulfilling || !string.IsNullOrEmpty(Instance.AccountHash) && PlayerPrefs.HasKey(Instance.OperationKey));
        public bool Ready => ready && !string.IsNullOrEmpty(ReadCredential());
        public bool Synchronizing => refreshing || saving || spending || socialSpending || fulfilling;
        public bool PurchasesReady => Ready && snapshot.billingReady;
        public bool SubscriptionsReady => PurchasesReady && snapshot.subscriptionsReady;
        public bool SubscriptionActive => Ready && snapshot.subscriptionActive;
        public int MinAndroidVersionCode => snapshot?.minAndroidVersionCode ?? 0;
        public string MinClientVersion => snapshot?.minClientVersion ?? string.Empty;
        // Revision survives spending the entire balance; switching must not strand paid history.
        public bool HasPaidHistory=>snapshot.revision>0||snapshot.balance>0||(snapshot.owned?.Length??0)>0;
        public string AccountHash { get; private set; }
        public int SessionVersion => generation;
        public static bool OwnsVoidfall => Instance != null && Instance.Ready && Instance.snapshot.voidfall;
        public static bool OwnsAvatar(int id) => Instance != null && Instance.Ready && Instance.snapshot.avatars != null && Array.IndexOf(Instance.snapshot.avatars,id) >= 0;
        readonly HashSet<TaskCompletionSource<bool>> pending = new();
        string endpoint;
        Func<string> token;
        bool ready,saving,spending,refreshing,socialSpending,fulfilling;
        int generation;
        long lastResponseCode;
        string settledOperation; bool operationSucceeded;
        float nextSave,nextRefresh;
        Snapshot snapshot = new();
        public string Status { get; private set; } = "Account server not configured.";
        string OperationKey => "DF_CommercePurchase_" + AccountHash;
        public static PaidShardCommerce Connect(string httpsEndpoint,Func<string> accessToken,string accountHash)
        {
            if (!Uri.TryCreate(httpsEndpoint,UriKind.Absolute,out var uri) || uri.Scheme != "https" || string.IsNullOrWhiteSpace(accountHash) || accountHash.Length > 64)
                throw new ArgumentException("HTTPS account endpoint and obfuscated account ID required");
            if (Instance == null) new GameObject("DF_PaidShardCommerce").AddComponent<PaidShardCommerce>();
            var c = Instance;
            c.generation++; c.ready = false; c.CancelPending(); AccountProfilePrefs.Detach();
            c.endpoint = httpsEndpoint.TrimEnd('/'); c.token = accessToken; c.AccountHash = accountHash;
            c.snapshot = new Snapshot(); c.nextSave = 0; c.nextRefresh = Time.unscaledTime + 30;
            c.StartCoroutine(c.Sync()); EntitlementsChanged?.Invoke(); return c;
        }
        public void Refresh() { if (token != null && !refreshing && !spending && !saving && !socialSpending && !fulfilling) StartCoroutine(Sync()); }
        public static void Disconnect()
        {
            if (Instance == null) return;
            var c = Instance; c.generation++; c.ready = false; c.token = null;
            c.CancelPending(); AccountProfilePrefs.Detach(); c.AccountHash = null; c.snapshot = new Snapshot();
            c.Status = "Account disconnected."; EntitlementsChanged?.Invoke();
        }
        void CancelPending()
        {
            StopAllCoroutines(); saving = spending = refreshing = socialSpending = fulfilling = false;
            var tasks = new List<TaskCompletionSource<bool>>(pending); pending.Clear();
            foreach (var task in tasks) task.TrySetResult(false);
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }
        void Update()
        {
            if (token == null || saving || spending || refreshing || socialSpending || fulfilling) return;
            if (!Ready)
            {
                if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 15; Refresh(); }
                return;
            }
            if (PlayerPrefs.HasKey(OperationKey) && Time.unscaledTime >= nextSave)
            { if(AccountServerBootstrap.Instance?.Busy!=true)StartCoroutine(RecoverPurchase()); return; }
            if (AccountProfilePrefs.Dirty && Time.unscaledTime >= nextSave)
            { nextSave = Time.unscaledTime + 5; StartCoroutine(SaveProfile(null)); }
            else if (Time.unscaledTime >= nextRefresh)
            { nextRefresh = Time.unscaledTime + 30; Refresh(); }
        }
        IEnumerator Sync()
        {
            refreshing = true;
            if (ready && AccountProfilePrefs.Dirty)
            {
                bool saved = false; yield return SaveProfile(ok => saved = ok);
                if (!saved) { refreshing = false; yield break; }
            }
            Response response = null;
            yield return Send("/wallet",new Request(),r => response = r);
            if (response?.wallet == null) { refreshing = false; yield break; }
            ApplyWallet(response.wallet);
            if (!AccountProfilePrefs.Attached)
                AccountProfilePrefs.Attach(AccountHash,response.wallet.profileRevision,response.wallet.profile);
            else if (response.wallet.profileRevision != AccountProfilePrefs.Revision)
                AccountProfilePrefs.ResolveConflict(response.wallet.profileRevision,response.wallet.profile);
            if(AccountProfilePrefs.HadConflict)ProfileTransaction.PreserveConflictingJournal();
            else ProfileTransaction.Recover();
            ready = true; refreshing = false;
            Status = AccountProfilePrefs.HadConflict ? "Server profile loaded. Offline conflict retained on this device." : "Account synchronized.";
            EntitlementsChanged?.Invoke();
        }
        IEnumerator SaveProfile(Action<bool> done)
        {
            if (!Ready || !AccountProfilePrefs.Attached) { done?.Invoke(false); yield break; }
            // Unity coroutines execute on the main thread: serialize every profile request.
            while (saving || socialSpending || fulfilling) yield return null;
            if (!AccountProfilePrefs.Dirty) { done?.Invoke(true); yield break; }
            saving = true;
            var submitted = AccountProfilePrefs.Capture();
            Response response = null;
            yield return Send("/profile/save",new Request { expectedProfileRevision = AccountProfilePrefs.Revision, profile = submitted },r => response = r);
            if (response?.wallet != null)
            {
                ApplyWallet(response.wallet);
                AccountProfilePrefs.ApplyRemote(response.wallet.profileRevision,response.wallet.profile,submitted);
                Status = "Account saved.";
            }
            else if (lastResponseCode == 409)
            {
                Response fresh = null; yield return Send("/wallet",new Request(),r => fresh = r);
                if (fresh?.wallet != null)
                {
                    ApplyWallet(fresh.wallet);
                    AccountProfilePrefs.ResolveConflict(fresh.wallet.profileRevision,fresh.wallet.profile);
                    Status = "Another device saved first. Server profile loaded; local changes retained.";
                }
            }
            saving = false; nextSave = Time.unscaledTime + 5;
            done?.Invoke(response?.wallet != null);
        }
        public Task<bool> FlushProfileAsync()
        {
            var task = new TaskCompletionSource<bool>();
            pending.Add(task);
            StartCoroutine(SaveProfile(ok => { pending.Remove(task); task.TrySetResult(ok); }));
            return task.Task;
        }
        public Task<bool> Fulfill(string product,string purchaseToken)
        {
            var task = new TaskCompletionSource<bool>();
            if (!Ready || !ShardPacks.IsProduct(product) || string.IsNullOrEmpty(purchaseToken)) { task.SetResult(false); return task.Task; }
            pending.Add(task); StartCoroutine(FulfillPurchase(product,purchaseToken,task)); return task.Task;
        }
        IEnumerator FulfillPurchase(string product,string purchaseToken,TaskCompletionSource<bool> task)
        {
            // Serialize verification with all other account mutations. A late fulfillment
            // response must not overwrite a newer purchase wallet or another request's status.
            while(socialSpending||saving||spending||refreshing||fulfilling)yield return null;
            fulfilling=true;
            bool ok=false;
            try
            {
                Response response = null;
                yield return Send(ShardPacks.IsSubscription(product)?"/google-play/subscription":"/google-play/fulfill",new Request { productId = product,purchaseToken = purchaseToken },r => response = r);
                ok = response?.wallet != null;
                if (ok) { ApplyWallet(response.wallet); Status = "Shards added. Account balance saved on server."; }
            }
            finally
            {
                fulfilling=false;
                pending.Remove(task); task.TrySetResult(ok);
            }
        }
        public bool TryBeginSocialSpend()
        {
            if(!Ready||AccountServerBootstrap.Instance?.Busy==true||saving||spending||refreshing||socialSpending||fulfilling||PlayerPrefs.HasKey(OperationKey))return false;
            socialSpending=true;return true;
        }
        public void EndSocialSpend(int session,Snapshot value,AccountProfilePrefs.Entry[] submitted)
        {
            if(session!=generation)return;
            if(value!=null&&value.accountHash==AccountHash&&value.profile!=null&&
                value.balance>=0&&value.revision>=snapshot.revision&&value.profileRevision>=AccountProfilePrefs.Revision)
            {
                ApplyWallet(value);
                AccountProfilePrefs.ApplyRemote(value.profileRevision,value.profile,submitted);
                EntitlementsChanged?.Invoke();
            }
            socialSpending=false;
        }
        public bool BeginPurchase(string sku)
        {
            if (!Ready || AccountServerBootstrap.Instance?.Busy==true || spending || saving || refreshing || socialSpending || fulfilling || string.IsNullOrEmpty(sku) || PlayerPrefs.HasKey(OperationKey)) return false;
            // Persist the id before making the request. A restart retries the same operation,
            // including consumable purchases, rather than issuing a second debit.
            PlayerPrefs.SetString(OperationKey,JsonUtility.ToJson(new PurchaseOperation { id = Guid.NewGuid().ToString("N"),sku = sku }));
            PlayerPrefs.Save(); StartCoroutine(RecoverPurchase()); return true;
        }
        IEnumerator RecoverPurchase()
        {
            spending = true; Status = "Saving purchaseâ€¦";
            PurchaseOperation op = null;
            try { op = JsonUtility.FromJson<PurchaseOperation>(PlayerPrefs.GetString(OperationKey)); }
            catch (Exception) { }
            if (op == null || string.IsNullOrEmpty(op.id) || string.IsNullOrEmpty(op.sku))
            { spending = false; Status = "Purchase recovery needs support."; yield break; }
            bool saved = false; yield return SaveProfile(ok => saved = ok);
            if (!saved) { spending = false; nextSave = Time.unscaledTime + 5; yield break; }
            var submitted = AccountProfilePrefs.Capture();
            Response response = null;
            yield return Send("/wallet/spend",new Request { cosmeticId = op.sku, operationId = op.id, expectedProfileRevision = AccountProfilePrefs.Revision },r => response = r);
            if (response?.wallet != null && response.wallet.operationId == op.id)
            {
                ApplyWallet(response.wallet);
                AccountProfilePrefs.ApplyRemote(response.wallet.profileRevision,response.wallet.profile,submitted);
                // Flush the durable cache before removing the recovery operation.
                settledOperation=op.id;operationSucceeded=true;
                PlayerPrefs.DeleteKey(OperationKey); PlayerPrefs.Save();
                Status = "Purchase completed."; EntitlementsChanged?.Invoke();
                Alterverse.Audio.DimensionAudio.Play(Alterverse.Audio.DimensionSound.Purchase,.65f);
            }
            else if (lastResponseCode == 400)
            {
                settledOperation=op.id;operationSucceeded=false;
                PlayerPrefs.DeleteKey(OperationKey); PlayerPrefs.Save();
                Status = "Purchase rejected. Refresh your balance and retry.";
            }
            else if (lastResponseCode == 409)
            {
                Response fresh = null; yield return Send("/wallet",new Request(),r => fresh = r);
                if (fresh?.wallet != null)
                {
                    ApplyWallet(fresh.wallet); AccountProfilePrefs.ResolveConflict(fresh.wallet.profileRevision,fresh.wallet.profile);
                }
                Status = "Account changed on another device. Retrying purchase.";
            }
            spending = false; nextSave = Time.unscaledTime + 5;
        }
        // Compatibility API uses the same durable path as all other shard purchases.
        public Task<bool> BuyCosmetic(string id)
        {
            if(!BeginPurchase(id))return Task.FromResult(false);
            var task=new TaskCompletionSource<bool>();pending.Add(task);
            var op=JsonUtility.FromJson<PurchaseOperation>(PlayerPrefs.GetString(OperationKey));
            StartCoroutine(WaitForPurchase(op.id,task));return task.Task;
        }
        IEnumerator WaitForPurchase(string operation,TaskCompletionSource<bool> task)
        {
            while(PlayerPrefs.HasKey(OperationKey)&&settledOperation!=operation)yield return null;
            pending.Remove(task);task.TrySetResult(settledOperation==operation&&operationSucceeded);
        }
        IEnumerator Send(string route,Request body,Action<Response> done)
        {
            int version = generation;
            string credential = ReadCredential(); lastResponseCode = 0;
            if (string.IsNullOrEmpty(credential)) { Status = "Sign in to reconnect your account."; done(null); yield break; }
            using var request = new UnityWebRequest(endpoint + route,"POST");
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 20;
            request.SetRequestHeader("Content-Type","application/json"); request.SetRequestHeader("Authorization","Bearer " + credential);
            yield return request.SendWebRequest();
            if (version != generation) { done(null); yield break; }
            lastResponseCode = request.responseCode;
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (lastResponseCode == 401 || lastResponseCode == 403) { ready = false; snapshot = new Snapshot(); EntitlementsChanged?.Invoke(); }
                Status = "Account connection interrupted. Saved operations will retry."; done(null); yield break;
            }
            Response response = null;
            try { response = JsonUtility.FromJson<Response>(request.downloadHandler.text); }
            catch (ArgumentException) { Status = "Invalid account server response."; }
            if (response == null || !response.fulfilled || response.wallet == null || response.wallet.balance < 0 ||
                response.wallet.profileRevision < 0 || response.wallet.profile == null || response.wallet.accountHash != AccountHash)
            { done(null); yield break; }
            done(response);
        }
        string ReadCredential()
        {
            try { return token?.Invoke(); }
            catch (Exception) { ready = false; Status = "Sign in to reconnect your account."; return null; }
        }
        void ApplyWallet(Snapshot value)
        {
            if (value.revision < snapshot.revision) return;
            snapshot = value; EntitlementsChanged?.Invoke();
        }
        void OnApplicationPause(bool paused)
        {
            AccountProfilePrefs.Save();
            if (paused && Ready && !saving && !spending && !refreshing && !socialSpending && !fulfilling) StartCoroutine(SaveProfile(null));
            if (!paused) Refresh();
        }
        void OnDestroy()
        {
            if(Instance!=this)return;
            ready = false; generation++; CancelPending(); AccountProfilePrefs.Detach();
            if (Instance == this) Instance = null; EntitlementsChanged?.Invoke();
        }
    }
}

