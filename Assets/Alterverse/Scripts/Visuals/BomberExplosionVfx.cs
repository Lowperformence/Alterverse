using Alterverse.Performance;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Shared bomb/meteor contact. Rendering is independent of damage and authority.</summary>
    public sealed class BomberExplosionVfx:MonoBehaviour
    {
        public static void PlayBomber(Vector3 position,float radius)
        {
            HeroVfxPool.Emit(HeroFxShape.ShockDome,position,position,Vector3.one,Color.white,1.1f,
                HeroFxMotion.Hold,epicKey:"BomberNuke",effectRadius:Mathf.Max(.1f,radius));
        }
        public static void Play(Vector3 position,Color fire,float radius)
        {
            radius=Mathf.Max(.1f,radius);
            Vector3 center=position+Vector3.up*.35f;
            HeroVfxPool.Emit(HeroFxShape.ShockDome,center,center,new Vector3(radius*.65f,radius*.26f,radius*.65f),
                Color.Lerp(fire,Color.white,.35f),.22f,HeroFxMotion.Expand);
            HeroVfxPool.Emit(HeroFxShape.PressureWave,position+Vector3.up*.08f,position,
                Vector3.one*radius,new Color(fire.r,fire.g,fire.b,.65f),.38f,HeroFxMotion.RadialPulse);
            HeroVfxPool.Sprite(HeroFxSprite.Fireball,center,center,new Vector2(radius*1.65f,radius*1.65f),
                fire,.55f,null);
            for(int i=0;i<2;i++)
            {
                Vector3 smoke=center+new Vector3((i==0?-.16f:.16f)*radius,.14f*i,0);
                HeroVfxPool.Sprite(HeroFxSprite.Smoke,smoke,smoke+Vector3.up*(radius*.45f),
                    new Vector2(radius*1.1f,radius*1.1f),new Color(.28f,.29f,.32f,.48f),1.1f,null,
                    HeroFxMotion.Drift,delay:.10f+i*.08f,roll:i*55f);
            }
            int count=AdaptiveVfxBudget.Count(5);
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2f/count;
                Vector3 d=new Vector3(Mathf.Cos(a),.45f,Mathf.Sin(a));
                HeroVfxPool.Emit(HeroFxShape.Spark,center,center+d*radius,
                    new Vector3(.06f,.06f,.23f),Color.Lerp(fire,Color.white,.30f),.60f,
                    HeroFxMotion.Scatter,Quaternion.LookRotation(d),decorative:true);
            }
        }
    }
}
