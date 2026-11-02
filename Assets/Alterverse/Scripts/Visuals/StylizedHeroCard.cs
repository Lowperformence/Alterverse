using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    public static class StylizedHeroCard
    {
        public static void Draw(Rect rect, HeroId hero, Texture2D pixel, float time)
        {
            if (pixel == null) return;
            Color accent = StylizedArtDirection.HeroAccent(hero);
            Color old = GUI.color;
            GUI.color = new Color(0, 0, 0, .58f);
            GUI.DrawTexture(rect, pixel);
            GUI.color = new Color(accent.r, accent.g, accent.b, .16f);
            GUI.DrawTexture(new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6), pixel);
            DrawFractureFrame(rect, pixel, accent, time);

            float cx = rect.center.x, top = rect.y + rect.height * .17f, bodyY = rect.y + rect.height * .48f;
            float shoulder = (hero == HeroId.Kaelon || hero == HeroId.Rook || hero == HeroId.Bolt7 || hero == HeroId.VoidKnight) ? .27f : .20f;
            float pulse = 1f + Mathf.Sin(time * 2f) * .04f;

            GUI.color = StylizedArtDirection.Ink;
            GUI.DrawTexture(new Rect(cx - rect.width * .105f, top, rect.width * .21f, rect.height * .19f), pixel);
            GUI.DrawTexture(new Rect(cx - rect.width * shoulder, bodyY, rect.width * shoulder * 2f, rect.height * .32f), pixel);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(cx - rect.width * .07f, bodyY + rect.height * .04f, rect.width * .14f, rect.height * .07f), pixel);

            switch (hero)
            {
                case HeroId.Veyr:
                    Cut(pixel, cx - rect.width * .25f, top + rect.height * .02f, rect.width * .035f, rect.height * .43f, accent);
                    Cut(pixel, cx + rect.width * .20f, top - rect.height * .02f, rect.width * .035f, rect.height * .49f, StylizedArtDirection.ZeroEnergy);
                    Cut(pixel, cx - rect.width * .03f, top - rect.height * .06f, rect.width * .06f, rect.height * .08f, accent);
                    break;
                case HeroId.Kaelon:
                    Cut(pixel, cx - rect.width * .37f, bodyY - rect.height * .03f, rect.width * .14f, rect.height * .29f, accent);
                    Cut(pixel, cx + rect.width * .23f, bodyY - rect.height * .03f, rect.width * .14f, rect.height * .29f, accent);
                    Cut(pixel, cx - rect.width * .18f, top - rect.height * .055f, rect.width * .36f, rect.height * .05f, accent);
                    break;
                case HeroId.Nyra:
                    Cut(pixel, cx + rect.width * .18f, bodyY - rect.height * .02f, rect.width * .055f, rect.height * .48f, accent);
                    Cut(pixel, cx + rect.width * .23f, bodyY + rect.height * .35f, rect.width * .035f, rect.height * .19f, StylizedArtDirection.WarmAccent);
                    Cut(pixel, cx - rect.width * .16f, top + rect.height * .045f, rect.width * .32f, rect.height * .025f, accent);
                    break;
                case HeroId.Elyra:
                    Orb(pixel, cx - rect.width * .30f, bodyY, rect.width * .08f * pulse, accent);
                    Orb(pixel, cx + rect.width * .30f, bodyY + rect.height * .18f, rect.width * .06f * pulse, StylizedArtDirection.LumenEnergy);
                    Orb(pixel, cx, top - rect.height * .07f, rect.width * .045f * pulse, accent);
                    break;
                case HeroId.Mira:
                    Cut(pixel, cx - rect.width * .34f, bodyY + rect.height * .04f, rect.width * .18f, rect.height * .075f, accent);
                    Cut(pixel, cx + rect.width * .16f, bodyY + rect.height * .04f, rect.width * .18f, rect.height * .075f, accent);
                    Cut(pixel, cx - rect.width * .24f, top - rect.height * .04f, rect.width * .08f, rect.height * .11f, accent);
                    Cut(pixel, cx + rect.width * .16f, top - rect.height * .04f, rect.width * .08f, rect.height * .11f, accent);
                    break;
                case HeroId.Rook:
                    Cut(pixel, cx + rect.width * .21f, bodyY + rect.height * .06f, rect.width * .23f, rect.height * .25f, accent);
                    Cut(pixel, cx - rect.width * .38f, bodyY + rect.height * .12f, rect.width * .13f, rect.height * .15f, StylizedArtDirection.WarmAccent);
                    Cut(pixel, cx - rect.width * .05f, top - rect.height * .09f, rect.width * .10f, rect.height * .10f, accent);
                    break;
                case HeroId.Traveler:
                    Orb(pixel, cx, bodyY + rect.height * .05f, rect.width * .085f * pulse, accent);
                    Orb(pixel, cx - rect.width * .30f, bodyY + rect.height * .16f, rect.width * .055f * pulse, new Color(1f, .04f, .025f));
                    Cut(pixel, cx + rect.width * .23f, bodyY - rect.height * .08f, rect.width * .035f, rect.height * .50f, accent);
                    Cut(pixel, cx - rect.width * .20f, top + rect.height * .02f, rect.width * .40f, rect.height * .025f, new Color(.82f, .75f, .60f));
                    break;
                case HeroId.Nerok:
                    Cut(pixel, cx - rect.width * .22f, top - rect.height * .06f, rect.width * .44f, rect.height * .07f, accent);
                    Cut(pixel, cx - rect.width * .34f, bodyY + rect.height * .02f, rect.width * .12f, rect.height * .24f, StylizedArtDirection.LumenEnergy);
                    Cut(pixel, cx + rect.width * .22f, bodyY - rect.height * .04f, rect.width * .12f, rect.height * .32f, accent);
                    break;
                case HeroId.Timmy:
                    Orb(pixel, cx - rect.width * .22f, top + rect.height * .04f, rect.width * .065f * pulse, accent);
                    Orb(pixel, cx + rect.width * .22f, top + rect.height * .04f, rect.width * .065f * pulse, StylizedArtDirection.WarmAccent);
                    Cut(pixel, cx - rect.width * .18f, bodyY + rect.height * .14f, rect.width * .36f, rect.height * .05f, accent);
                    break;
                case HeroId.VoidKnight:
                    Cut(pixel, cx - rect.width * .16f, top - rect.height * .08f, rect.width * .07f, rect.height * .12f, accent);
                    Cut(pixel, cx + rect.width * .09f, top - rect.height * .08f, rect.width * .07f, rect.height * .12f, accent);
                    Cut(pixel, cx + rect.width * .24f, top - rect.height * .04f, rect.width * .06f, rect.height * .56f, StylizedArtDirection.ZeroEnergy);
                    break;
                case HeroId.Bolt7:
                    Cut(pixel, cx - rect.width * .36f, bodyY - rect.height * .06f, rect.width * .16f, rect.height * .22f, accent);
                    Cut(pixel, cx + rect.width * .20f, bodyY - rect.height * .06f, rect.width * .16f, rect.height * .22f, accent);
                    Orb(pixel, cx, bodyY + rect.height * .08f, rect.width * .075f * pulse, StylizedArtDirection.WarmAccent);
                    break;
            }

            GUI.color = new Color(1, 1, 1, .72f);
            GUI.DrawTexture(new Rect(rect.x + 10, rect.y + 9, rect.width * .28f, 2), pixel);
            GUI.DrawTexture(new Rect(rect.xMax - rect.width * .2f - 10, rect.yMax - 11, rect.width * .2f, 2), pixel);
            GUI.color = old;
        }

        private static void DrawFractureFrame(Rect r, Texture2D p, Color accent, float time)
        {
            float pulse = .35f + .15f * Mathf.Sin(time * 1.7f);
            GUI.color = new Color(accent.r, accent.g, accent.b, pulse);
            GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, 3, r.height * .31f), p);
            GUI.DrawTexture(new Rect(r.xMax - 7, r.yMax - r.height * .35f - 4, 3, r.height * .35f), p);
            for (int i = 0; i < 3; i++)
            {
                float y = r.y + r.height * (.18f + i * .27f);
                float w = r.width * (.10f + i * .035f);
                GUI.DrawTexture(new Rect(r.x + 7, y, w, 2), p);
                GUI.DrawTexture(new Rect(r.xMax - w - 7, y + r.height * .08f, w, 2), p);
            }
        }

        private static void Cut(Texture2D p, float x, float y, float w, float h, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), p);
        }

        private static void Orb(Texture2D p, float x, float y, float r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(x - r, y - r, r * 2, r * 2), p);
        }
    }
}

