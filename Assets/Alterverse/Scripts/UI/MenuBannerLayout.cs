using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Full, unscaled 320x50 dp banner plus a separate frame inside the safe area.</summary>
    public readonly struct MenuBannerLayout
    {
        public readonly bool Valid, MediumRectangle;
        public readonly Rect ContentSafeArea, BoardPixels, AdPixels;
        MenuBannerLayout(Rect content, Rect board, Rect ad, bool mediumRectangle = false)
        { Valid = true; MediumRectangle = mediumRectangle; ContentSafeArea = content; BoardPixels = board; AdPixels = ad; }
        public static MenuBannerLayout Calculate(Rect safe, float screenHeight, float density)
        {
            if (density <= 0 || float.IsNaN(density) || float.IsInfinity(density)) density = 1;
            // A separate right-hand board matches the reference on roomy displays.
            // Keep enough width for readable menu controls before reserving a 300x250 dp ad.
            if (safe.width >= 1000 * density && safe.height >= 400 * density)
            {
                float sideWidth = 316 * density, sideHeight = 282 * density, sideGap = 8 * density;
                Rect sideBoard = new Rect(safe.xMax - sideWidth - sideGap,
                    screenHeight - safe.yMax + (safe.height - sideHeight) * .5f, sideWidth, sideHeight);
                Rect sideAd = new Rect(sideBoard.x + 8 * density, sideBoard.y + 24 * density, 300 * density, 250 * density);
                Rect sideContent = new Rect(safe.x, safe.y, safe.width - sideWidth - sideGap * 2, safe.height);
                return new MenuBannerLayout(sideContent, sideBoard, sideAd, true);
            }
            float width = 336 * density, height = 82 * density, gap = 8 * density;
            if (safe.width < width + gap * 2 || safe.height < height + 120 * density)
                return default;
            Rect board = new Rect(safe.xMax - gap - width, screenHeight - safe.y - height, width, height);
            Rect ad = new Rect(board.x + 8 * density, board.y + 24 * density, 320 * density, 50 * density);
            Rect content = new Rect(safe.x, safe.y + height + gap, safe.width, safe.height - height - gap);
            return new MenuBannerLayout(content, board, ad);
        }
    }
}
