using System;
using System.Collections;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Networking;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Gracefully releases an Edgegap deployment after the authoritative match has ended.
    /// Edgegap injects a deployment-scoped one-time self-stop URL/token, so no account API token
    /// is stored in the image. Local dedicated-server runs are left untouched.
    /// </summary>
    [DefaultExecutionOrder(-23000)]
    public sealed class EdgegapDedicatedServerLifecycle : MonoBehaviour
    {
        private const string DeleteUrlVariable = "ARBITRIUM_DELETE_URL";
        private const string DeleteTokenVariable = "ARBITRIUM_DELETE_TOKEN";
        private const float ResultGraceSeconds = 20f;
        private const int MaxAttempts = 3;
        private const int RequestTimeoutSeconds = 20;

        private DimensionsFront front;
        private bool subscribed;
        private bool stopping;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            // Do not depend on ordering between multiple BeforeSceneLoad initializers. This tiny
            // component may exist in a client build too, but remains completely dormant unless the
            // dedicated-server bootstrap has actually requested server mode.
            if (FindAnyObjectByType<EdgegapDedicatedServerLifecycle>() == null)
                new GameObject("DF_EdgegapDedicatedServerLifecycle").AddComponent<EdgegapDedicatedServerLifecycle>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (subscribed || !DedicatedServerNetworkRuntime.Requested) return;
            front = FindAnyObjectByType<DimensionsFront>();
            if (front == null) return;
            front.MatchEnded += OnMatchEnded;
            subscribed = true;
        }

        private void OnMatchEnded(int winner)
        {
            if (stopping || !EdgegapInjectedMatchContext.IsEdgegapDeployment) return;
            stopping = true;
            StartCoroutine(SelfStopAfterResults());
        }

        private IEnumerator SelfStopAfterResults()
        {
            Debug.Log($"DIMENSIONS FRONT SERVER: match ended; Edgegap self-stop scheduled in {ResultGraceSeconds:0}s.");
            float stopAt = Time.realtimeSinceStartup + ResultGraceSeconds;
            while (Time.realtimeSinceStartup < stopAt) yield return null;

            string url = Environment.GetEnvironmentVariable(DeleteUrlVariable) ?? "";
            string token = Environment.GetEnvironmentVariable(DeleteTokenVariable) ?? "";
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            {
                Debug.LogWarning("DIMENSIONS FRONT SERVER: Edgegap self-stop variables are unavailable; deployment guardrail will stop the server later.");
                yield break;
            }

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                using UnityWebRequest request = UnityWebRequest.Delete(url.Trim());
                request.timeout = RequestTimeoutSeconds;
                request.SetRequestHeader("Authorization", token.Trim());
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300)
                {
                    Debug.Log("DIMENSIONS FRONT SERVER: Edgegap self-stop accepted.");
                    yield break;
                }

                Debug.LogWarning($"DIMENSIONS FRONT SERVER: Edgegap self-stop attempt {attempt}/{MaxAttempts} failed ({request.responseCode}): {request.error}");
                if (attempt < MaxAttempts)
                {
                    float retryAt = Time.realtimeSinceStartup + attempt * 2f;
                    while (Time.realtimeSinceStartup < retryAt) yield return null;
                }
            }

            Debug.LogError("DIMENSIONS FRONT SERVER: Edgegap self-stop failed after retries; deployment guardrail remains the fallback.");
        }

        private void OnDestroy()
        {
            if (subscribed && front != null) front.MatchEnded -= OnMatchEnded;
            subscribed = false;
        }
    }
}
