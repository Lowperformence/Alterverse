using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Android's text engine supplies script shaping, bidi and system-font fallback offline.</summary>
    internal static class LocalizedText
    {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        static AndroidJavaClass renderer;
        static bool unavailable;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Queue<string> order = new Queue<string>();
        static int cachedBytes;
#endif
        public static bool Draw(Rect rect, string text, GUIStyle style)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            if (unavailable || string.IsNullOrEmpty(text) || rect.width <= 0 || rect.height <= 0) return false;
            bool international = false;
            foreach (char character in text) if (character > 127 && char.IsLetter(character)) { international = true; break; }
            if (!international) return false;
            if (Event.current.type != EventType.Repaint) return true;
            // Cached transparent glyph textures have no position, account or network dependencies.
            int width = Mathf.Clamp(Mathf.CeilToInt(rect.width * 2), 1, 2048);
            int height = Mathf.Clamp(Mathf.CeilToInt(rect.height * 2), 1, 512);
            int size = Mathf.Max(12, style.fontSize) * 2;
            int alignment = (int)style.alignment;
            string key = width + ":" + height + ":" + size + ":" + alignment + ":" + (int)style.fontStyle + ":" + style.wordWrap + ":" + text;
            try
            {
                if (!cache.TryGetValue(key, out var texture))
                {
                    renderer ??= new AndroidJavaClass("com.dimensionsfront.ui.LocalizedTextBitmap");
                    byte[] png = renderer.CallStatic<byte[]>("render", text, width, height, size,
                        style.fontStyle == FontStyle.Bold || style.fontStyle == FontStyle.BoldAndItalic, alignment, style.wordWrap);
                    if (png == null || png.Length == 0) return false;
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(texture, png, true)) { UnityEngine.Object.Destroy(texture); return false; }
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    texture.filterMode = FilterMode.Bilinear;
                    int bytes = texture.width * texture.height * 4;
                    while (order.Count > 0 && (order.Count >= 96 || cachedBytes + bytes > 16 * 1024 * 1024))
                    {
                        string old = order.Dequeue();
                        cachedBytes -= cache[old].width * cache[old].height * 4;
                        UnityEngine.Object.Destroy(cache[old]); cache.Remove(old);
                    }
                    cache[key] = texture; order.Enqueue(key); cachedBytes += bytes;
                }
                Color before = GUI.color;
                GUI.color *= style.normal.textColor;
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
                GUI.color = before;
                return true;
            }
            catch (Exception error)
            {
                unavailable = true;
                Debug.LogWarning("International text renderer unavailable: " + error.Message);
            }
#endif
            return false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            // Unsubscribe first for Enter Play Mode/domain reload configurations.
            Application.lowMemory -= ReleaseCachedTextures;
            Application.focusChanged -= OnFocusChanged;
            ReleaseCachedTextures();
            unavailable = false;
            Application.lowMemory += ReleaseCachedTextures;
            Application.focusChanged += OnFocusChanged;
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        static void OnFocusChanged(bool focused)
        {
            if (!focused) ReleaseCachedTextures();
        }

        static void ReleaseCachedTextures()
        {
            // Recreated lazily on the next draw. Never unload live match assets or
            // change saved graphics preferences in response to memory pressure.
            foreach (var texture in cache.Values) UnityEngine.Object.Destroy(texture);
            cache.Clear(); order.Clear(); cachedBytes = 0; renderer?.Dispose(); renderer = null;
        }
#endif
    }
}
