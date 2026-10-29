using System;
using System.Collections.Generic;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>One authored composition per arena; effects never change scene lighting.</summary>
    internal readonly struct CosmicSkyProfile
    {
        public const string MenuPath="FrontEnd/CosmicMenu";
        public readonly string ResourcePath;
        public readonly Color Dust;
        public readonly float Seed,Exposure,MeteorInterval,MeteorStrength,NebulaStrength;
        public readonly bool Menu;

        CosmicSkyProfile(string resource,int seed,Color dust,float exposure=.82f,
            float interval=25f,float meteors=.72f,float nebula=.018f,bool menu=false)
        {ResourcePath=resource;Seed=seed;Dust=dust;Exposure=exposure;
            MeteorInterval=interval;MeteorStrength=meteors;NebulaStrength=nebula;Menu=menu;}

        public static CosmicSkyProfile MainMenu=>new(MenuPath,173,new(.40f,.52f,1f),
            .92f,13f,.85f,.022f,true);

        public static CosmicSkyProfile ForArena(ArenaVariant arena)=>arena switch
        {
            ArenaVariant.Moon=>Arena("Moon",11,new(.48f,.62f,1f),.78f,29f),
            ArenaVariant.Mars=>Arena("Mars",23,new(1f,.49f,.22f),.76f,24f),
            ArenaVariant.GreenPlanet=>Arena("GreenPlanet",37,new(.28f,1f,.72f),.78f,26f),
            ArenaVariant.Noxara=>Arena("Noxara",43,new(.61f,.32f,1f),.78f,32f),
            ArenaVariant.Kryon=>Arena("Kryon",59,new(.45f,.85f,1f),.76f,28f),
            ArenaVariant.Earth=>Arena("Earth",67,new(.38f,.65f,1f),.80f,27f),
            ArenaVariant.Pyraxis=>Arena("Pyraxis",79,new(1f,.32f,.12f),.72f,23f),
            ArenaVariant.Neptune=>Arena("Neptune",83,new(.23f,.65f,1f),.78f,29f),
            ArenaVariant.Dark00=>new("MapPanoramas/Dark00",97,new(.32f,.18f,.60f),.72f,43f,.32f,.008f),
            ArenaVariant.DiamondCore=>Arena("DiamondCore",101,new(.72f,.62f,1f),.76f,31f),
            ArenaVariant.GJ504b=>Arena("GJ504b",113,new(1f,.37f,.71f),.76f,26f),
            ArenaVariant.Wasp76b=>Arena("Wasp76b",127,new(1f,.64f,.25f),.74f,24f),
            ArenaVariant.Trappist1e=>Arena("Trappist1e",139,new(.40f,.70f,.78f),.80f,30f),
            ArenaVariant.Toi715b=>Arena("Toi715b",151,new(1f,.55f,.31f),.77f,28f),
            _=>throw new ArgumentOutOfRangeException(nameof(arena))
        };

        static CosmicSkyProfile Arena(string name,int seed,Color dust,float exposure,float interval)
            =>new("MapPanoramas/"+name,seed,dust,exposure,interval);
    }

    /// <summary>Shared texture leases protect the menu still while its animated target closes.</summary>
    internal static class CosmicSkyAssets
    {
        sealed class Entry { public Texture2D Texture;public int References; }
        static readonly Dictionary<string,Entry> textures=new();

        public static Texture2D Acquire(string path)
        {
            if(!CosmicSkyMaterial.RenderingAllowed)return null;
            if(textures.TryGetValue(path,out Entry entry)&&entry.Texture!=null)
            {entry.References++;return entry.Texture;}
            Texture2D texture=Resources.Load<Texture2D>(path);
            if(texture!=null)textures[path]=new Entry{Texture=texture,References=1};
            return texture;
        }

        public static void Release(string path,Texture2D texture)
        {
            if(texture==null||!textures.TryGetValue(path,out Entry entry)||entry.Texture!=texture)return;
            if(--entry.References>0)return;
            textures.Remove(path);
            Resources.UnloadAsset(texture);
        }
    }

    /// <summary>Deterministic, at most two short streaks. Does not consume Unity's gameplay RNG.</summary>
    internal static class CosmicSkyMotion
    {
        public static Vector4 Meteor(float time,float interval,float seed,float aspect,float strength)
        {
            float clock=Mathf.Max(0f,time)+seed*.113f;
            float cycle=Mathf.Floor(clock/interval);
            float local=clock-cycle*interval;
            const float duration=.95f;
            if(local>=duration)return Vector4.zero;
            float progress=local/duration;
            float fade=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress/.14f))*
                Mathf.SmoothStep(0f,1f,Mathf.Clamp01((1f-progress)/.36f));
            float x=.10f+Hash(cycle*7f+seed)*.56f;
            float y=.84f+Hash(cycle*13f+seed*3f)*.12f;
            // Coordinates use screen-height units, matching the shader's aspect-correct trail.
            x+=progress*.54f/Mathf.Max(.5f,aspect);
            y-=progress*.261534f;
            return new Vector4(x,y,fade*strength,.14f+.045f*Hash(cycle+seed*5f));
        }
        static float Hash(float value)=>Mathf.Repeat(Mathf.Sin(value*12.9898f)*43758.5453f,1f);
    }

    /// <summary>Owns one selected texture and material, plus a small shared noise-data texture.</summary>
    internal sealed class CosmicSkyMaterial : IDisposable
    {
        public const string ShaderPath="MapPanoramas/CosmicSky";
        const string NoisePath="MapPanoramas/CosmicNoise";
        static readonly int PanoramaId=Shader.PropertyToID("_Panorama");
        static readonly int NoiseId=Shader.PropertyToID("_CosmicNoise");
        static readonly int ImageAspectId=Shader.PropertyToID("_ImageAspect");
        static readonly int FrameAspectId=Shader.PropertyToID("_FrameAspect");
        static readonly int ParametersId=Shader.PropertyToID("_CosmicParams");
        static readonly int DustId=Shader.PropertyToID("_DustColor");
        static readonly int MeteorAId=Shader.PropertyToID("_MeteorA");
        static readonly int MeteorBId=Shader.PropertyToID("_MeteorB");
        readonly CosmicSkyProfile profile;
        Texture2D noise;
        public Texture2D Texture {get;private set;}
        public Material Material {get;private set;}

        public static bool RenderingAllowed
        {
            get
            {
#if UNITY_SERVER
                return false;
#else
                return !Application.isBatchMode;
#endif
            }
        }

        CosmicSkyMaterial(CosmicSkyProfile value){profile=value;}
        public static CosmicSkyMaterial Create(CosmicSkyProfile profile)
        {
            if(!RenderingAllowed)return null;
            var result=new CosmicSkyMaterial(profile);
            result.Texture=CosmicSkyAssets.Acquire(profile.ResourcePath);
            Shader shader=Resources.Load<Shader>(ShaderPath);
            if(result.Texture==null||shader==null||!shader.isSupported)
            {result.Dispose();return null;}
            result.noise=CosmicSkyAssets.Acquire(NoisePath);
            result.Material=new Material(shader){name="DF_CosmicSky_"+profile.ResourcePath};
            result.Material.SetTexture(PanoramaId,result.Texture);
            if(result.noise!=null)result.Material.SetTexture(NoiseId,result.noise);
            result.Material.SetFloat(ImageAspectId,result.Texture.width/(float)Mathf.Max(1,result.Texture.height));
            result.Material.SetColor(DustId,profile.Dust);
            result.Update(0f,2f);
            return result;
        }

        public void Update(float time,float aspect)
        {
            if(Material==null)return;
            if(float.IsNaN(time)||float.IsInfinity(time))time=0f;
            if(float.IsNaN(aspect)||float.IsInfinity(aspect))aspect=2f;
            aspect=Mathf.Max(.5f,aspect);
            Material.SetFloat(FrameAspectId,aspect);
            Material.SetVector(ParametersId,new Vector4(profile.Exposure,profile.Seed,
                Mathf.Repeat(Mathf.Max(0f,time),2048f),noise!=null?profile.NebulaStrength:0f));
            Material.SetVector(MeteorAId,CosmicSkyMotion.Meteor(time,profile.MeteorInterval,
                profile.Seed,aspect,profile.MeteorStrength));
            Material.SetVector(MeteorBId,CosmicSkyMotion.Meteor(time,profile.MeteorInterval*1.618f,
                profile.Seed+79f,aspect,profile.MeteorStrength*.70f));
        }

        public void Dispose()
        {
            if(Material!=null)UnityEngine.Object.Destroy(Material);
            Material=null;
            CosmicSkyAssets.Release(profile.ResourcePath,Texture);Texture=null;
            CosmicSkyAssets.Release(NoisePath,noise);noise=null;
        }
    }
}
