using UnityEngine;
using UnityEngine.Video;

namespace Alterverse.UI
{
    /// <summary>Local, silent menu loop. Its decoder and render target exist only while the menu is open.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuBackgroundVideo : MonoBehaviour
    {
        private const string ClipPath = "FrontEnd/InterstellarBackdropAnimated";
        private const float PrepareTimeout = 15f;
        private VideoPlayer player;
        private VideoClip clip;
        private RenderTexture image;
        private bool firstFrame, paused, unfocused, preparing;
        private float prepareStartedAt;

        public Texture Image => firstFrame && image != null && image.IsCreated() ? image : null;

        private void OnEnable()
        {
#if !UNITY_SERVER
            if (!Application.isBatchMode) StartPlayback();
#endif
        }

        private void StartPlayback()
        {
            if (player == null)
            {
                player = gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.source = VideoSource.VideoClip;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.isLooping = true;
                player.waitForFirstFrame = true;
                player.skipOnDrop = true;
                // MatchClock pauses game time in menus. This clock still advances the video.
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                player.prepareCompleted += OnPrepared;
                player.frameReady += OnFirstFrame;
                player.errorReceived += OnError;
            }

            clip = Resources.Load<VideoClip>(ClipPath);
            if (clip == null) return;
            image = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32)
            {
                name = "DF_MenuBackgroundVideo",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            if (!image.Create()) { FailPlayback("Could not create the menu video render target."); return; }
            firstFrame = false;
            player.clip = clip;
            player.targetTexture = image;
            player.sendFrameReadyEvents = true;
            prepareStartedAt = Time.unscaledTime;
            preparing = true;
            player.Prepare();
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (!isActiveAndEnabled || source != player || clip == null) return;
            preparing = false;
            if (!paused && !unfocused) source.Play();
        }

        private void OnFirstFrame(VideoPlayer source, long frame)
        {
            if (!isActiveAndEnabled || source != player || image == null) return;
            // Keep the still visible until the decoder has produced an actual frame.
            firstFrame = true;
            source.sendFrameReadyEvents = false;
        }

        private void Update()
        {
            if (paused || unfocused) return;
            if (preparing && Time.unscaledTime - prepareStartedAt > PrepareTimeout)
                FailPlayback("Menu video preparation timed out.");
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            SyncPlayback();
        }

        private void OnApplicationFocus(bool focused)
        {
            unfocused = !focused;
            SyncPlayback();
        }

        private void SyncPlayback()
        {
            if (!isActiveAndEnabled || player == null || clip == null) return;
            if (paused || unfocused) { player.Pause(); return; }
            // Android can discard render targets while the app is in the background.
            if (image != null && !image.IsCreated())
            {
                StopAndRelease();
                StartPlayback();
                return;
            }
            prepareStartedAt = Time.unscaledTime;
            if (player.isPrepared) player.Play();
        }

        private void OnError(VideoPlayer source, string message)
        {
            if (source == player && clip != null) FailPlayback(message);
        }

        private void FailPlayback(string message)
        {
            StopAndRelease();
            Debug.LogWarning("DIMENSIONS FRONT: menu video unavailable; using still background. " + message, this);
        }

        private void OnDisable() => StopAndRelease();

        private void StopAndRelease()
        {
            firstFrame = false;
            preparing = false;
            if (player != null)
            {
                player.sendFrameReadyEvents = false;
                player.Stop();
                player.targetTexture = null;
                player.clip = null;
            }
            if (image != null)
            {
                image.Release();
                Destroy(image);
                image = null;
            }
            if (clip != null)
            {
                Resources.UnloadAsset(clip);
                clip = null;
            }
        }

        private void OnDestroy()
        {
            StopAndRelease();
            if (player == null) return;
            player.prepareCompleted -= OnPrepared;
            player.frameReady -= OnFirstFrame;
            player.errorReceived -= OnError;
        }
    }
}
