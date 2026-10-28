using System;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
using Google.Play.Review;
#endif

namespace Alterverse.UI
{
    internal enum StoreReviewResult { Unavailable, Failed, FlowFinished }

    internal static class NativeStoreReview
    {
        // FlowFinished says only that control returned. Google does not reveal
        // whether the card appeared, a rating was submitted, or it was dismissed.
        public static async Task<StoreReviewResult> RequestAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            try
            {
                var manager = new ReviewManager();
                var request = manager.RequestReviewFlow();
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!request.IsDone)
                {
                    if (Time.realtimeSinceStartup >= deadline) return StoreReviewResult.Failed;
                    await Task.Delay(100);
                }
                if (request.Error != ReviewErrorCode.NoError) return StoreReviewResult.Failed;
                var launch = manager.LaunchReviewFlow(request.GetResult());
                // A player writing their review may take as long as they need.
                while (!launch.IsDone) await Task.Delay(100);
                return launch.Error == ReviewErrorCode.NoError
                    ? StoreReviewResult.FlowFinished : StoreReviewResult.Failed;
            }
            catch (Exception)
            {
                Debug.LogWarning("DIMENSIONS FRONT: native Google Play review is unavailable.");
                return StoreReviewResult.Failed;
            }
#else
            await Task.CompletedTask;
            return StoreReviewResult.Unavailable;
#endif
        }

        public static void OpenStore()
        {
            string id = Uri.EscapeDataString(Application.identifier);
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "market://details?id=" + id);
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", uri);
                intent.Call<AndroidJavaObject>("setPackage", "com.android.vending").Dispose();
                activity.Call("startActivity", intent);
                return;
            }
            catch (Exception) { /* Browser fallback for devices without the Play Store. */ }
#endif
            Application.OpenURL("https://play.google.com/store/apps/details?id=" + id);
        }
    }
}
