using System;
using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Cached, imported origin-world art: no per-pixel work during hero selection.</summary>
    internal sealed partial class HeroOriginBackdrop : IDisposable
    {
        internal const string ResourceFolder="UI/HeroOriginBackdrops/";
        private const float PixelsPerUnit=144f;
        private static readonly byte[] DisplayCurve=BuildDisplayCurve();
        private readonly Sprite[] sprites=new Sprite[HeroIdentity.HeroCount];
        private readonly bool[] requested=new bool[HeroIdentity.HeroCount];
        private bool disposed;

        public Sprite SpriteFor(HeroId hero)
        {
            int index=(int)hero;
            if(disposed||index<0||index>=sprites.Length)return null;
            if(requested[index])return sprites[index];
            requested[index]=true;
            // Committed PNGs are imported/compressed by Unity before Play mode or an Android build.
            // Resources shares these textures; this cache owns only its lightweight Sprite wrappers.
            Texture2D texture=Resources.Load<Texture2D>(ResourceFolder+hero);
            if(texture==null)
            {
                Debug.LogWarning("DIMENSIONS FRONT: missing hero backdrop "+hero+". Using its fixed palette.");
                return null;
            }
            Sprite sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),
                PixelsPerUnit,0,SpriteMeshType.FullRect);
            sprite.name="DF_OriginWorld_"+hero;
            return sprites[index]=sprite;
        }

        public static Color BaseColorFor(HeroId hero)
        {
            WorldStyle style=StyleFor(hero);
            Color32 colour=Clamp(style.Top*1.65f+style.Haze*.23f);
            return new Color32{r=DisplayCurve[colour.r],g=DisplayCurve[colour.g],b=DisplayCurve[colour.b],a=255};
        }

        private static WorldStyle StyleFor(HeroId hero)
        {
            switch (hero)
            {
                case HeroId.Veyr: // Zhaev Null — magenta void / fractured world
                    return new WorldStyle(
                        new Color(.060f, .010f, .045f), new Color(.015f, .003f, .012f),
                        new Color(.70f, .04f, .48f), new Color(.24f, .02f, .16f),
                        new Color(.16f, .025f, .105f), new Color(.055f, .012f, .035f),
                        new Color(1f, .18f, .78f), new Vector2(.80f, .61f), .34f, 11, false, .15f, .72f);
                case HeroId.Kaelon: // Auren Kest — ancient amber / iron world
                    return new WorldStyle(
                        new Color(.055f, .035f, .018f), new Color(.008f, .012f, .020f),
                        new Color(.70f, .32f, .07f), new Color(.28f, .11f, .025f),
                        new Color(.40f, .20f, .065f), new Color(.13f, .055f, .025f),
                        new Color(1f, .68f, .22f), new Vector2(.77f, .59f), .38f, 23, true, .72f, .18f);
                case HeroId.Nyra: // Sivra Quell — cold cyan ocean / ring world
                    return new WorldStyle(
                        new Color(.012f, .055f, .090f), new Color(.004f, .012f, .025f),
                        new Color(.04f, .62f, .68f), new Color(.025f, .20f, .26f),
                        new Color(.10f, .50f, .63f), new Color(.025f, .14f, .24f),
                        new Color(.30f, .94f, 1f), new Vector2(.79f, .62f), .36f, 37, true, .40f, .28f);
                case HeroId.Elyra: // Ione Vaska — arcane violet storm world
                    return new WorldStyle(
                        new Color(.060f, .018f, .092f), new Color(.008f, .007f, .021f),
                        new Color(.56f, .12f, .78f), new Color(.21f, .055f, .34f),
                        new Color(.34f, .075f, .48f), new Color(.095f, .025f, .18f),
                        new Color(.83f, .33f, 1f), new Vector2(.78f, .58f), .35f, 49, false, .32f, .95f);
                case HeroId.Mira: // Tahla Venn — emerald sky and jade world
                    return new WorldStyle(
                        new Color(.008f, .066f, .028f), new Color(.003f, .014f, .009f),
                        new Color(.05f, .58f, .22f), new Color(.025f, .19f, .09f),
                        new Color(.085f, .43f, .19f), new Color(.018f, .13f, .055f),
                        new Color(.35f, 1f, .60f), new Vector2(.80f, .60f), .36f, 61, true, .23f, .42f);
                case HeroId.Rook: // Brakk Orun — volcanic ember world
                    return new WorldStyle(
                        new Color(.075f, .020f, .014f), new Color(.012f, .008f, .012f),
                        new Color(.78f, .16f, .025f), new Color(.30f, .055f, .018f),
                        new Color(.33f, .075f, .025f), new Color(.095f, .025f, .018f),
                        new Color(1f, .38f, .08f), new Vector2(.78f, .61f), .39f, 73, false, .12f, 1f);
                case HeroId.Nerok: // Pelagos-9 — deep ocean and cyan bioluminescence
                    return new WorldStyle(
                        new Color(.008f,.030f,.066f), new Color(.003f,.008f,.017f),
                        new Color(.02f,.28f,.55f),new Color(.015f,.09f,.22f),
                        new Color(.02f,.31f,.39f),new Color(.01f,.065f,.12f),
                        new Color(.13f,.85f,1f),new Vector2(.78f,.60f),.37f,101,false,.5f,.32f);
                case HeroId.Traveler: // Traveler — indigo temporal eclipse
                    return new WorldStyle(
                        new Color(.018f, .025f, .090f), new Color(.005f, .007f, .025f),
                        new Color(.12f, .22f, .85f), new Color(.06f, .12f, .40f),
                        new Color(.075f, .10f, .34f), new Color(.018f, .025f, .085f),
                        new Color(.38f, .52f, 1f), new Vector2(.78f, .58f), .35f, 89, true, .08f, .72f);
                case HeroId.Timmy: // Andromeda Timmy — rose nebula and striped desert moon
                    return new WorldStyle(
                        new Color(.080f,.018f,.040f),new Color(.020f,.005f,.012f),
                        new Color(.90f,.20f,.44f),new Color(.34f,.06f,.15f),
                        new Color(.48f,.16f,.23f),new Color(.14f,.035f,.065f),
                        new Color(1f,.60f,.73f),new Vector2(.83f,.66f),.30f,113,false,.85f,.15f);
                case HeroId.VoidKnight: // Void Knight — crimson rift and dark iron planet
                    return new WorldStyle(
                        new Color(.075f,.008f,.020f),new Color(.018f,.003f,.009f),
                        new Color(.72f,.025f,.07f),new Color(.25f,.012f,.035f),
                        new Color(.28f,.035f,.055f),new Color(.065f,.012f,.025f),
                        new Color(1f,.12f,.25f),new Vector2(.75f,.54f),.40f,127,false,.05f,1f);
                case HeroId.Bolt7: // BOLT-7 — electric yellow nebula and industrial ring world
                    return new WorldStyle(
                        new Color(.070f,.060f,.010f),new Color(.018f,.015f,.004f),
                        new Color(.75f,.60f,.08f),new Color(.25f,.22f,.025f),
                        new Color(.34f,.28f,.075f),new Color(.10f,.085f,.025f),
                        new Color(1f,.92f,.38f),new Vector2(.81f,.63f),.33f,139,true,.62f,.55f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(hero),hero,"Hero needs its own origin-world style.");
            }
        }

        private static byte[] BuildDisplayCurve()
        {
            var curve = new byte[256];
            for (int i = 0; i < curve.Length; i++)
                curve[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Pow(i / 255f, .82f) * 1.12f) * 255f);
            return curve;
        }

        private static Color32 Clamp(Color c)
        {
            return new Color(
                Mathf.Clamp01(c.r),
                Mathf.Clamp01(c.g),
                Mathf.Clamp01(c.b),
                Mathf.Clamp01(c.a));
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            for(int i=0;i<sprites.Length;i++)
            {
                if(sprites[i]!=null)UnityEngine.Object.Destroy(sprites[i]);
                sprites[i]=null;
            }
        }

        private readonly struct WorldStyle
        {
            public readonly Color Top, Bottom, Haze, SecondaryHaze, PlanetA, PlanetB, Rim;
            public readonly Vector2 PlanetCenter;
            public readonly float Radius, Bands, Storm;
            public readonly int Seed;
            public readonly bool Ring;

            public WorldStyle(Color top, Color bottom, Color haze, Color secondaryHaze,
                Color planetA, Color planetB, Color rim, Vector2 planetCenter, float radius,
                int seed, bool ring, float bands, float storm)
            {
                Top = top;
                Bottom = bottom;
                Haze = haze;
                SecondaryHaze = secondaryHaze;
                PlanetA = planetA;
                PlanetB = planetB;
                Rim = rim;
                PlanetCenter = planetCenter;
                Radius = radius;
                Seed = seed;
                Ring = ring;
                Bands = bands;
                Storm = storm;
            }
        }
    }
}


