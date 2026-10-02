using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.Visuals
{
    /// <summary>Layered bomb-only impact. Visual lifetime is independent of damage and network authority.</summary>
    public sealed class BomberExplosionVfx : MonoBehaviour
    {
        Material material;Texture2D texture;
        public static void Play(Vector3 position,Color fire,float radius)
        {
            var go=new GameObject("DF_BomberExplosion");go.transform.position=position;
            go.AddComponent<BomberExplosionVfx>().Build(fire,radius);
        }
        void Build(Color fire,float radius)
        {
            Shader shader=Resources.Load<Shader>("HeroArt/BombPlume");
            if(shader==null){Destroy(gameObject);return;}
            texture=new Texture2D(64,64,TextureFormat.RGBA32,false,true){name="DF_SoftExplosionPlume",wrapMode=TextureWrapMode.Clamp};
            var colors=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float u=(x+.5f)/32f-1,v=(y+.5f)/32f-1,r=Mathf.Sqrt(u*u+v*v);
                float noise=.72f+.28f*Mathf.PerlinNoise(x*.14f+7,y*.14f+19);
                float a=Mathf.Pow(Mathf.Clamp01(1-r),1.35f)*noise;
                colors[y*64+x]=new Color(noise,noise,noise,a);
            }
            texture.SetPixels(colors);texture.Apply(false,true);
            material=new Material(shader);material.SetTexture("_MainTex",texture);
            Layer("Flash",1,.16f,radius*.80f,0,new Color(2.3f,1.9f,1.35f,1),new Color(fire.r,fire.g,fire.b,0),0,.0f,0);
            Layer("Fireballs",14,.63f,radius*.43f,radius*.60f,Color.Lerp(fire,Color.white,.38f),new Color(fire.r*.34f,fire.g*.19f,fire.b*.14f,0),.20f,.20f,1.4f);
            Layer("RollingSmoke",20,2.25f,radius*.53f,radius*.35f,new Color(.24f,.23f,.23f,.70f),new Color(.14f,.145f,.15f,0),.42f,.34f,1.3f);
            Layer("GroundDust",32,1.25f,radius*.30f,radius*1.8f,new Color(.38f,.32f,.27f,.58f),new Color(.26f,.24f,.22f,0),.23f,.06f,.15f,true);
            Layer("HotEmbers",18,.80f,.08f,radius*2.1f,new Color(1.8f,.75f,.12f,1),new Color(.7f,.18f,.03f,0),.16f,.16f,.6f);
            Destroy(gameObject,2.65f);
        }
        void Layer(string name,int count,float life,float size,float speed,Color first,Color last,float spread,float delay,float rise,bool ground=false)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=p.main;main.loop=false;main.playOnAwake=false;main.duration=.1f;main.startDelay=delay;
            main.startLifetime=new ParticleSystem.MinMaxCurve(life*.75f,life);main.startSize=new ParticleSystem.MinMaxCurve(size*.65f,size);
            main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.55f,speed);main.startColor=first;
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=count;
            var emission=p.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var shape=p.shape;shape.shapeType=ground?ParticleSystemShapeType.Circle:ParticleSystemShapeType.Hemisphere;shape.radius=spread;
            go.transform.localRotation=Quaternion.Euler(-90,0,0);
            var velocity=p.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=rise;
            var color=p.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(first,0),new GradientColorKey(last,1)},new[]{new GradientAlphaKey(first.a,0),new GradientAlphaKey(first.a*.75f,.25f),new GradientAlphaKey(0,1)});color.color=gradient;
            var growth=p.sizeOverLifetime;growth.enabled=true;growth.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.35f),new Keyframe(.22f,1),new Keyframe(1,1.7f)));
            var renderer=p.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortMode=ParticleSystemSortMode.Distance;
            p.Play();
        }
        void OnDestroy(){if(material!=null)Destroy(material);if(texture!=null)Destroy(texture);}
    }
}
