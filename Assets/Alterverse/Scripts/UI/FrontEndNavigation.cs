using System;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Cached, multicolour navigation artwork, independent of mobile font glyphs.</summary>
    internal static class FrontEndNavigation
    {
        private const int Resolution = 96;
        private static readonly Texture2D[] icons = new Texture2D[11];
        // Each section keeps its colour when selected; the button frame indicates selection.
        private static readonly Color32[] palette =
        {
            new(102,193,255,255), // Home: sky blue
            new(91,229,166,255),  // Play: emerald
            new(184,146,255,255), // Heroes: violet
            new(255,193,100,255), // Store: amber
            new(82,216,232,255),  // Inventory: cyan
            new(143,167,255,255), // Profile: periwinkle
            new(201,229,116,255), // Daily: lime
            new(255,143,168,255), // Ranking: rose
            new(174,196,218,255), // Settings: silver blue
            new(255,224,137,255), // Battle Pass: gold
            new(255,152,109,255)  // Clans: coral
        };

        public static void DrawIcon(Rect rect, int index, Color color)
        {
            index = Mathf.Clamp(index, 0, icons.Length - 1);
            if (icons[index] == null) icons[index] = Build(index);
            Color before = GUI.color;
            try
            {
                GUI.color *= color;
                GUI.DrawTexture(rect, icons[index], ScaleMode.ScaleToFit, true);
            }
            finally { GUI.color = before; }
        }

        private static Texture2D Build(int kind)
        {
            Color accent = palette[kind];
            Color light = Color.Lerp(accent, Color.white, .55f);
            Color shade = Color.Lerp(accent, new Color(.025f,.045f,.075f), .42f);
            Color ink = new Color(.035f,.075f,.115f);
            var art = new Canvas();
            art.Box(3,3,94,94,23,WithAlpha(accent,.38f));
            art.Box(5,5,90,90,21,Color.Lerp(accent,ink,.86f),Color.Lerp(accent,ink,.96f));
            art.Line(24,9,55,9,1.2f,WithAlpha(light,.25f));

            switch (kind)
            {
                case 0: // Home: solid roof, doorway and one lit window.
                    art.Box(28,40,44,38,5,accent,shade);
                    art.Polygon(light,accent,17,43,50,17,83,43,77,51,50,31,23,51);
                    art.Box(45,58,12,20,2,ink);
                    art.Box(33,51,8,9,1.5f,light);
                    break;
                case 1: // Play: one clear, filled launch arrow.
                    art.Polygon(light,accent,35,23,76,50,35,77);
                    art.Line(39,32,39,65,2,WithAlpha(Color.white,.55f));
                    break;
                case 2: // Heroes: a helmet, visibly different from the profile portrait.
                    art.Polygon(light,accent,34,23,66,23,75,36,70,62,60,78,40,78,30,62,25,36);
                    art.Polygon(accent,shade,47,17,55,17,55,35,47,35);
                    art.Polygon(ink,ink,33,41,50,46,67,41,64,51,50,58,36,51);
                    art.Line(33,57,42,66,3,light);
                    art.Line(67,57,58,66,3,light);
                    break;
                case 3: // Store: shopping bag with a sturdy handle and clasp.
                    art.Ring(50,35,14,5,light);
                    art.Polygon(light,accent,27,37,73,37,78,78,22,78);
                    art.Line(31,44,69,44,2,WithAlpha(Color.white,.4f));
                    art.Box(44,54,12,12,3,shade);
                    art.Line(47,59,53,59,2,light);
                    break;
                case 4: // Inventory: three coloured planes of a supply crate.
                    art.Polygon(light,accent,50,19,80,35,50,51,20,35);
                    art.Polygon(accent,shade,20,39,47,54,47,82,20,66);
                    art.Polygon(shade,accent,53,54,80,39,80,66,53,82);
                    art.Line(50,23,72,35,2,WithAlpha(Color.white,.6f));
                    break;
                case 5: // Profile: portrait and shoulders.
                    art.Disc(50,34,13,light,accent);
                    art.Box(25,56,50,24,11,accent,shade);
                    art.Box(23,71,54,10,4,shade);
                    art.Line(36,61,42,58,2,light);
                    break;
                case 6: // Daily: checklist with a large completion mark.
                    art.Box(25,23,50,58,7,light,accent);
                    art.Box(38,18,24,12,4,shade);
                    art.Line(37,52,47,62,6,ink);
                    art.Line(47,62,65,41,6,ink);
                    break;
                case 7: // Ranking: a rising podium and winner's pennant.
                    art.Box(21,56,16,23,3,shade);
                    art.Box(42,39,16,40,3,light,accent);
                    art.Box(63,48,16,31,3,accent,shade);
                    art.Line(50,22,50,34,2.5f,light);
                    art.Polygon(light,accent,50,19,66,22,50,29);
                    break;
                case 8: // Settings: solid gear teeth and a dark central hub.
                    for (int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI*.25f;
                        art.Line(50+Mathf.Cos(a)*21,50+Mathf.Sin(a)*21,
                            50+Mathf.Cos(a)*29,50+Mathf.Sin(a)*29,11,accent);
                    }
                    art.Disc(50,50,24,light,accent);
                    art.Disc(50,50,10,ink);
                    art.Ring(50,50,12,2,WithAlpha(light,.65f));
                    break;
                case 9: // Battle Pass: a filled three-point season crown.
                    art.Polygon(light,accent,22,68,17,33,36,47,50,23,64,47,83,33,78,68);
                    art.Box(23,73,54,7,2,accent,shade);
                    art.Polygon(ink,ink,50,47,55,54,50,61,45,54);
                    break;
                case 10: // Clans: its own shield, rather than the Home fallback.
                    art.Polygon(light,accent,50,18,78,29,74,58,65,72,50,83,35,72,26,58,22,29);
                    art.Polygon(shade,shade,50,29,66,35,63,54,57,64,50,70,43,64,37,54,34,35);
                    art.Line(40,42,59,59,4,light);
                    art.Line(60,42,41,59,4,light);
                    break;
            }

            var texture=new Texture2D(Resolution,Resolution,TextureFormat.RGBA32,false)
            {
                name="DF_Navigation_"+kind,hideFlags=HideFlags.HideAndDontSave,
                filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp
            };
            texture.SetPixels32(art.Pixels());
            texture.Apply(false,true);
            return texture;
        }

        private static Color WithAlpha(Color value,float alpha)
        { value.a=alpha;return value; }

        // Analytic edge coverage keeps cached 96px artwork crisp at menu sizes.
        // Only a cache miss rasterizes geometry; drawing allocates nothing afterwards.
        private sealed class Canvas
        {
            private readonly Color[] pixels=new Color[Resolution*Resolution];

            private void Paint(Rect bounds,Func<Vector2,float> distance,Color top,Color bottom)
            {
                float unit=100f/Resolution;
                int minX=Mathf.Max(0,Mathf.FloorToInt((bounds.xMin-unit)/unit));
                int maxX=Mathf.Min(Resolution-1,Mathf.CeilToInt((bounds.xMax+unit)/unit));
                int minY=Mathf.Max(0,Mathf.FloorToInt((bounds.yMin-unit)/unit));
                int maxY=Mathf.Min(Resolution-1,Mathf.CeilToInt((bounds.yMax+unit)/unit));
                for (int y=minY;y<=maxY;y++)
                for (int x=minX;x<=maxX;x++)
                {
                    var point=new Vector2((x+.5f)*unit,(y+.5f)*unit);
                    float coverage=Mathf.Clamp01(.5f-distance(point)/unit);
                    if (coverage<=0)continue;
                    Color color=Color.Lerp(top,bottom,Mathf.Clamp01((point.y-bounds.y)/Mathf.Max(1,bounds.height)));
                    float alpha=color.a*coverage;
                    int index=(Resolution-1-y)*Resolution+x;
                    Color old=pixels[index];
                    float combined=alpha+old.a*(1-alpha);
                    pixels[index]=new Color(
                        (color.r*alpha+old.r*old.a*(1-alpha))/combined,
                        (color.g*alpha+old.g*old.a*(1-alpha))/combined,
                        (color.b*alpha+old.b*old.a*(1-alpha))/combined,combined);
                }
            }

            public void Box(float x,float y,float width,float height,float radius,Color top,Color? bottom=null)
            {
                var rect=new Rect(x,y,width,height);
                Paint(rect,p=>
                {
                    var q=new Vector2(Mathf.Abs(p.x-rect.center.x)-width*.5f+radius,
                        Mathf.Abs(p.y-rect.center.y)-height*.5f+radius);
                    return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+
                        Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
                },top,bottom??top);
            }
            public void Disc(float x,float y,float radius,Color top,Color? bottom=null)
                =>Paint(new Rect(x-radius,y-radius,radius*2,radius*2),
                    p=>(p-new Vector2(x,y)).magnitude-radius,top,bottom??top);
            public void Ring(float x,float y,float radius,float width,Color color)
                =>Paint(new Rect(x-radius-width,y-radius-width,(radius+width)*2,(radius+width)*2),
                    p=>Mathf.Abs((p-new Vector2(x,y)).magnitude-radius)-width*.5f,color,color);
            public void Line(float ax,float ay,float bx,float by,float width,Color color)
            {
                var a=new Vector2(ax,ay);var b=new Vector2(bx,by);
                Paint(new Rect(Mathf.Min(ax,bx)-width,Mathf.Min(ay,by)-width,
                    Mathf.Abs(ax-bx)+width*2,Mathf.Abs(ay-by)+width*2),
                    p=>Segment(p,a,b)-width*.5f,color,color);
            }
            private static float Segment(Vector2 p,Vector2 a,Vector2 b)
            {
                Vector2 delta=b-a;
                float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/Mathf.Max(.00001f,delta.sqrMagnitude));
                return (p-a-delta*t).magnitude;
            }
            public void Polygon(Color top,Color bottom,params float[] coordinates)
            {
                var points=new Vector2[coordinates.Length/2];
                float minX=100,minY=100,maxX=0,maxY=0;
                for (int i=0;i<points.Length;i++)
                {
                    points[i]=new Vector2(coordinates[i*2],coordinates[i*2+1]);
                    minX=Mathf.Min(minX,points[i].x);maxX=Mathf.Max(maxX,points[i].x);
                    minY=Mathf.Min(minY,points[i].y);maxY=Mathf.Max(maxY,points[i].y);
                }
                Paint(new Rect(minX,minY,maxX-minX,maxY-minY),p=>
                {
                    float nearest=100;bool inside=false;
                    for (int i=0,j=points.Length-1;i<points.Length;j=i++)
                    {
                        Vector2 a=points[j],b=points[i];
                        nearest=Mathf.Min(nearest,Segment(p,a,b));
                        if ((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                    }
                    return inside?-nearest:nearest;
                },top,bottom);
            }
            public Color32[] Pixels()
            {
                var result=new Color32[pixels.Length];
                for (int i=0;i<result.Length;i++)result[i]=pixels[i];
                return result;
            }
        }
    }
}
