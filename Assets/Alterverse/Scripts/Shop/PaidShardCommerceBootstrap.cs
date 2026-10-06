using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Alterverse.Shop
{
    /// <summary>
    /// Connects the existing shard commerce client to the production Cloud Run purchase server
    /// using the current Unity Authentication player and access token.
    /// </summary>
    public sealed class PaidShardCommerceBootstrap : MonoBehaviour
    {
        private const string Endpoint =
            "https://dimensions-front-purchase-server-1029475752167.europe-west1.run.app";

        public static PaidShardCommerceBootstrap Instance { get; private set; }

        private bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if (Instance == null)
                new GameObject("DF_PaidShardCommerceBootstrap").AddComponent<PaidShardCommerceBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _ = ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                SubscribeAuthenticationEvents();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                string playerId = AuthenticationService.Instance.PlayerId;
                if (string.IsNullOrWhiteSpace(playerId))
                    throw new InvalidOperationException("Unity Authentication returned no PlayerId.");

                string accountHash = Sha256(playerId);

                PaidShardCommerce.Connect(
                    Endpoint,
                    () => AuthenticationService.Instance.IsSignedIn
                        ? AuthenticationService.Instance.AccessToken
                        : null,
                    accountHash);

                Debug.Log("DIMENSIONS FRONT COMMERCE: purchase server connection started.");
            }
            catch (Exception ex)
            {
                PaidShardCommerce.Disconnect();
                Debug.LogWarning("DIMENSIONS FRONT COMMERCE: could not connect purchase server. " + ex.Message);
            }
        }

        private void SubscribeAuthenticationEvents()
        {
            if (subscribed) return;

            AuthenticationService.Instance.SignedOut += OnAuthenticationLost;
            AuthenticationService.Instance.Expired += OnAuthenticationLost;
            subscribed = true;
        }

        private void OnAuthenticationLost()
        {
            PaidShardCommerce.Disconnect();
        }

        private static string Sha256(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var result = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
                result.Append(b.ToString("x2"));

            return result.ToString();
        }

        private void OnDestroy()
        {
            if (subscribed && UnityServices.State == ServicesInitializationState.Initialized)
            {
                AuthenticationService.Instance.SignedOut -= OnAuthenticationLost;
                AuthenticationService.Instance.Expired -= OnAuthenticationLost;
            }

            if (Instance == this)
                Instance = null;
        }
    }
}
