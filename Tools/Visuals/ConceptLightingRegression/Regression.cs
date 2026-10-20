using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
using UObject=UnityEngine.Object;

static class Regression
{
    static int assertions,scenarios;
    static readonly HashSet<string> Profiles=new();
    static void Check(bool ok,string message)
    {
        assertions++;
        if(!ok) throw new Exception(message+" ["+ArenaVariantRuntime.Current+"]");
    }
    static void Invoke(LunarLighting target,string method) => typeof(LunarLighting).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    static float Luma(Color c) => c.r*.2126f+c.g*.7152f+c.b*.0722f;
    static bool ColorBounded(Color c) => float.IsFinite(c.r)&&float.IsFinite(c.g)&&float.IsFinite(c.b)&&c.r>=0&&c.g>=0&&c.b>=0&&c.r<=1&&c.g<=1&&c.b<=1;
    static Light NewLight(string name,LightType type,bool enabled=true)
    {
        Light l=new GameObject(name).AddComponent<Light>();
        l.type=type;l.enabled=enabled;l.intensity=.63f;l.shadowStrength=.37f;
        l.shadows=LightShadows.Hard;l.color=new(.73f,.29f,.41f);l.cullingMask=unchecked((int)0xA5A5A5A5);
        l.transform.rotation=Quaternion.Euler(13,27,49);
        return l;
    }
    readonly record struct GlobalSnapshot(Color Sky,Color Equator,Color Ground,AmbientMode Mode,bool Fog,float Reflection,Light Sun)
    {
        public static GlobalSnapshot Capture() => new(RenderSettings.ambientSkyColor,RenderSettings.ambientEquatorColor,RenderSettings.ambientGroundColor,RenderSettings.ambientMode,RenderSettings.fog,RenderSettings.reflectionIntensity,RenderSettings.sun);
        public bool Same() => Equals(Capture());
    }
    readonly record struct KeySnapshot(float Intensity,float Strength,LightShadows Shadows,Color Color,int Mask,Quaternion Rotation)
    {
        public static KeySnapshot Capture(Light l) => new(l.intensity,l.shadowStrength,l.shadows,l.color,l.cullingMask,l.transform.rotation);
        public bool Same(Light l) => Equals(Capture(l));
    }
    static void Scenario(ArenaVariant planet,ShadowQuality quality,int selection,bool fog)
    {
        scenarios++;
        UObject.Reset();ArenaVariantRuntime.Current=planet;QualitySettings.shadows=quality;
        RenderSettings.ambientSkyColor=new(.12f,.23f,.34f);RenderSettings.ambientEquatorColor=new(.24f,.35f,.46f);
        RenderSettings.ambientGroundColor=new(.36f,.47f,.58f);RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.fog=fog;RenderSettings.reflectionIntensity=.83f;
        Light point=NewLight("Point",LightType.Point),disabled=NewLight("Disabled",LightType.Directional,false);
        Light active=selection<2?NewLight("Active",LightType.Directional):null;
        // 0: valid selected sun. 1: invalid selected sun with directional fallback.
        // 2: no suitable light. 3: disabled selected directional with no fallback.
        RenderSettings.sun=selection==0?active:selection==1?point:selection==3?disabled:null;
        GlobalSnapshot before=GlobalSnapshot.Capture();
        KeySnapshot pointBefore=KeySnapshot.Capture(point),disabledBefore=KeySnapshot.Capture(disabled);
        KeySnapshot? keyBefore=active==null?null:KeySnapshot.Capture(active);
        var host=new GameObject("Host");LunarLighting runtime=host.AddComponent<LunarLighting>();
        for(int cycle=0;cycle<2;cycle++)
        {
            Invoke(runtime,"OnEnable");
            Light[] keys=UObject.FindObjectsByType<Light>().Where(x=>x.enabled&&x.type==LightType.Directional).ToArray();
            Check(keys.Length==1,"lighting creates or retains duplicate directional keys");
            Light key=keys[0];
            Check(active==null?key.gameObject.name=="DF_LunarKey":ReferenceEquals(key,active),"wrong directional key selected");
            Check(RenderSettings.ambientMode==AmbientMode.Trilight,"ambient mode not trilight");
            Check(!RenderSettings.fog,"combat fog accidentally enabled");
            Check(ColorBounded(key.color)&&ColorBounded(RenderSettings.ambientSkyColor)&&ColorBounded(RenderSettings.ambientEquatorColor)&&ColorBounded(RenderSettings.ambientGroundColor),"profile contains invalid color");
            Check(float.IsFinite(key.intensity)&&key.intensity>0&&key.intensity<=2,"key intensity outside mobile/readability range");
            Check(float.IsFinite(key.shadowStrength)&&key.shadowStrength>=0&&key.shadowStrength<=1,"invalid shadow strength");
            Check(RenderSettings.reflectionIntensity>=0&&RenderSettings.reflectionIntensity<=1,"invalid reflection intensity");
            Check((key.cullingMask&(1<<31))==0,"showcase layer affected by arena key");
            Check(Luma(RenderSettings.ambientSkyColor)>Luma(RenderSettings.ambientGroundColor),"ambient lighting loses overhead depth");
            if(quality==ShadowQuality.Disable||planet==ArenaVariant.Dark00)Check(key.shadows==LightShadows.None,"disabled/dark map still requests shadows");
            else Check(key.shadows!=LightShadows.None,"lit arena loses its depth shadows");
            if(planet==ArenaVariant.Dark00)Check(key.intensity<.15f&&RenderSettings.reflectionIntensity<.10f,"Dark00 identity no longer dark");
            else Check(key.intensity>=1&&Luma(RenderSettings.ambientSkyColor)>.15f,"lit arena too dark for combat readability");
            Profiles.Add(key.color+"|"+RenderSettings.ambientSkyColor+"|"+RenderSettings.ambientEquatorColor+"|"+RenderSettings.ambientGroundColor+"|"+key.intensity+"|"+RenderSettings.reflectionIntensity);
            Check(pointBefore.Same(point)&&disabledBefore.Same(disabled),"unselected lights modified");
            Invoke(runtime,"OnDisable");
            Check(before.Same(),"global lighting state not restored");
            Check(pointBefore.Same(point)&&disabledBefore.Same(disabled),"unselected light changed at cleanup");
            if(active!=null)Check(keyBefore.Value.Same(active)&&!active.destroyed,"reused key state not restored");
            else Check(key.destroyed&&key.gameObject.destroyed,"owned fallback key leaked");
            Invoke(runtime,"OnDisable");
            Check(before.Same(),"repeat cleanup not idempotent");
        }
    }
    static void Main()
    {
        ArenaVariant[] maps=Enum.GetValues<ArenaVariant>();
        foreach(ArenaVariant planet in maps)
        foreach(ShadowQuality quality in Enum.GetValues<ShadowQuality>())
        for(int selection=0;selection<4;selection++)
        foreach(bool fog in new[]{false,true})Scenario(planet,quality,selection,fog);
        Check(maps.Length==14,"map coverage changed; update fixture coverage expectations");
        Check(Profiles.Count==maps.Length,"each map needs its own distinct lighting profile");
        Console.WriteLine(JsonSerializer.Serialize(new {maps=maps.Length,scenarios,assertions,distinctProfiles=Profiles.Count,scope="Compiled actual LunarLighting.cs with lifecycle/Unity-state substitutes. Does not test Unity rendering, URP integration or Android performance."},new JsonSerializerOptions {WriteIndented=true}));
    }
}
