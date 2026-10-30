using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Full-size lower-right banner with a separate frame inside the safe area.</summary>
    public readonly struct MenuBannerLayout
    {
        public readonly bool Valid, LargeBanner;
        public readonly Rect BoardPixels, AdPixels;
        MenuBannerLayout(Rect board, Rect ad, bool largeBanner)
        { Valid = true; LargeBanner = largeBanner; BoardPixels = board; AdPixels = ad; }
        public static MenuBannerLayout Calculate(Rect safe, float screenHeight, float density)
        {
            if (density <= 0 || float.IsNaN(density) || float.IsInfinity(density)) density = 1;
            bool large = safe.height >= 340 * density;
            float adHeight = (large ? 100 : 50) * density;
            float width = 336 * density, height = adHeight + 32 * density, gap = 8 * density;
            // Leave enough space for the three existing footer actions to the left.
            if (safe.width < 520 * density || safe.height < 260 * density)
                return default;
            Rect board = new Rect(safe.xMax - gap - width, screenHeight - safe.y - height, width, height);
            Rect ad = new Rect(board.x + 8 * density, board.y + 24 * density, 320 * density, adHeight);
            return new MenuBannerLayout(board, ad, large);
        }
        public Rect InMenuCoordinates(Rect safe, float screenHeight)
        {
            if (!Valid) return default;
            float scale = Mathf.Min(safe.height / 720f, safe.width / 1500f);
            float top = screenHeight - safe.yMax + (safe.height - 720 * scale) * .5f;
            return new Rect((BoardPixels.x - safe.x) / scale, (BoardPixels.y - top) / scale,
                BoardPixels.width / scale, BoardPixels.height / scale);
        }
    }
}
