// Captures actual shop drawing commands for layout checks, without Unity or checkout.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace UnityEngine
{
    public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;} public static float Distance(Vector2 a,Vector2 b)=>MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
    public struct Rect {public float x,y,width,height;public Rect(float a,float b,float w,float h){x=a;y=b;width=w;height=h;}public float xMax=>x+width;public Vector2 center=>new(x+width/2,y+height/2);}
    public struct Color {public float r,g,b,a;public Color(float x,float y,float z,float w=1){r=x;g=y;b=z;a=w;} public static Color white=>new(1,1,1);}
    public struct Color32 {public byte r,g,b,a;public Color32(byte x,byte y,byte z,byte w){r=x;g=y;b=z;a=w;}}
    public enum TextureFormat {RGBA32} public enum FilterMode {Bilinear} public enum TextureWrapMode {Clamp} public enum HideFlags {DontSave}
    public enum TextAnchor {MiddleCenter,MiddleLeft} public enum FontStyle {Bold} public enum TextClipping {Clip} public enum ScaleMode {StretchToFill,ScaleToFit}
    public enum EventType {Repaint,Layout} public class Event {public static Event current=new();public EventType type=EventType.Repaint;}
    public class GUIStyleState {public Color textColor=Color.white;}
    public class Font {}
    public class RectOffset {public RectOffset(int l,int r,int t,int b){}}
    public class GUIContent {public string text="";}
    public class GUIStyle {public int fontSize=16;public Font font;public bool wordWrap,richText;public RectOffset padding;public TextAnchor alignment;public FontStyle fontStyle;public TextClipping clipping;public GUIStyleState normal=new();public GUIStyle(){}public GUIStyle(GUIStyle other){fontSize=other.fontSize;alignment=other.alignment;fontStyle=other.fontStyle;normal.textColor=other.normal.textColor;}public Vector2 CalcSize(GUIContent content)=>new(content.text.Length*fontSize*.62f,fontSize+2);}
    public class GUISkin {public GUIStyle label=new();}
    public class Texture2D
    {
        public string name;public int width,height;public Color32[] pixels;public FilterMode filterMode;public TextureWrapMode wrapMode;public HideFlags hideFlags;
        public static Texture2D whiteTexture=new(1,1,TextureFormat.RGBA32,false,true){name="white",pixels=new[]{new Color32(255,255,255,255)}};
        public Texture2D(int w,int h,TextureFormat f,bool m,bool linear){width=w;height=h;}
        public void SetPixels32(Color32[] values){pixels=values;}public void Apply(bool mip,bool discard){}
    }
    public static class GUI
    {
        public static GUISkin skin=new();public static Color color=Color.white;public static List<object> Commands=new();public static Dictionary<string,Texture2D> Textures=new();
        public static void DrawTexture(Rect r,Texture2D t,ScaleMode mode=ScaleMode.StretchToFill,bool alpha=true){Textures[t.name]=t;Commands.Add(new{kind="texture",rect=r,tint=color,texture=t.name,fit=mode==ScaleMode.ScaleToFit});}
        public static void Label(Rect r,string text,GUIStyle style){Commands.Add(new{kind="label",rect=r,text,size=style.fontSize,tint=style.normal.textColor,center=style.alignment==TextAnchor.MiddleCenter});}
        public static void Label(Rect r,GUIContent content,GUIStyle style)=>Label(r,content.text,style);
    }
    public static class Mathf
    {
        public static float Min(float a,float b)=>MathF.Min(a,b);public static int Min(int a,int b)=>Math.Min(a,b);public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Max(float a,float b)=>MathF.Max(a,b);public static int Clamp(int a,int min,int max)=>Math.Clamp(a,min,max);
        public static int FloorToInt(float x)=>(int)MathF.Floor(x);public static int CeilToInt(float x)=>(int)MathF.Ceiling(x);public static int RoundToInt(float x)=>(int)MathF.Round(x);
        public static float Abs(float x)=>MathF.Abs(x);public static float Sin(float x)=>MathF.Sin(x);public static float Pow(float x,float p)=>MathF.Pow(x,p);public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
    }
    public static class Time {public static float unscaledTime=1;}
    public class TextAsset {public string text;}
    public static class Resources {public static string Catalog;public static T Load<T>(string path) where T:class=>new TextAsset{text=File.ReadAllText(Catalog)} as T;}
    public static class JsonUtility {public static T FromJson<T>(string json)=>JsonSerializer.Deserialize<T>(json,new JsonSerializerOptions{IncludeFields=true});}
}
namespace Alterverse.Profile {public static class PlayerWallet {public static int Balance=>2300;}}
namespace Alterverse.Shop
{
    public class GooglePlayShardStore
    {
        public static GooglePlayShardStore Instance=new();public int SubscriptionDiscount=>25;public bool Busy,SubscriptionActive;
        public string SubscriptionStatus=>"Ready";public string Status=>"Google Play connected. Prices supplied by Google Play.";
        public string Price(string id)=>id.EndsWith("monthly")?"€7.49":id.EndsWith("12000")?"€100.00":id.EndsWith("5500")?"€59.99":id.EndsWith("1200")?"€14.99":"€5.99";
        public bool CanBuy(string id)=>true;public void Buy(string id){} public void Reconnect(){} public void ManageSubscription(){}
    }
}
namespace Alterverse.UI
{
    using UnityEngine;
    static class FrontEndTypography {public static Font Display,Body;}
    class FrontEndTheme
    {
        public GUIStyle Heading=new(){fontSize=26},Small=new(){fontSize=16};
        public void Panel(Rect r){GUI.Commands.Add(new{kind="panel",rect=r});}
        public void Label(Rect r,string text,GUIStyle style,bool muted=false){GUI.Commands.Add(new{kind="label",rect=r,text,size=style.fontSize,muted,center=false});}
        public bool Action(Rect r,string text,bool primary=false,bool selected=false,bool enabled=true){GUI.Commands.Add(new{kind="button",rect=r,text,size=19,primary,enabled});return false;}
    }
}
