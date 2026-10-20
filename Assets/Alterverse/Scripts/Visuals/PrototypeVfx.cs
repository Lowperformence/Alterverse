using Alterverse.Performance;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Shared unit/objective effects use the bounded hero scheduler, with no
    // per-effect GameObjects, components, materials, coroutines or colliders.
    public static class PrototypeVfx
    {
        public static void RadialBurst(Vector3 position,Color color,float radius)=>Shockwave(position,color,radius);
        public static void Burst(Vector3 position,Color color,float size=1f,int pieces=7)
        {
            if(size<=0||pieces<=0)return;
            Vector3 p=position+Vector3.up*.8f;
            HeroVfxPool.Emit(HeroFxShape.Burst,p,p,Vector3.one*(size*.45f),color,.20f,decorative:true);
            HeroVfxPool.Sprite(HeroFxSprite.Burst,p,p,new Vector2(size*.8f,size*.8f),color,.22f,null);
            int count=Mathf.Min(8,AdaptiveVfxBudget.Count(pieces));
            for(int i=0;i<count;i++)
            {
                Vector3 direction=Quaternion.Euler(0,360f/count*i,0)*Vector3.forward;
                HeroVfxPool.Emit(HeroFxShape.Spark,p,p+direction*(size*.65f),
                    new Vector3(.06f,.06f,.20f)*size,color,.24f,HeroFxMotion.Drift,
                    Quaternion.LookRotation(direction),decorative:true);
            }
        }
        public static void Ring(Vector3 position,Color color,float radius=1.5f,float duration=.35f)
        {
            Vector3 p=position+Vector3.up*.065f;
            // A hollow outline: no filled floor disc or per-use GameObject.
            HeroVfxPool.Emit(HeroFxShape.Ring,p,p,Vector3.one*Mathf.Max(.01f,radius),color,duration,HeroFxMotion.RadialPulse);
        }
        public static void Slash(Transform source,Color color,float length=1.7f)
        {
            if(source==null)return;
            Vector3 p=source.position+Vector3.up*.75f+source.forward;
            HeroVfxPool.Emit(HeroFxShape.Slash,p,p,new Vector3(length*.6f,.7f,length*.6f),
                color,.18f,HeroFxMotion.Sweep,source.rotation);
            HeroVfxPool.Sprite(HeroFxSprite.SlashLight,p,p,new Vector2(length,length),color,.18f,source.gameObject,
                rotation:source.rotation*Quaternion.Euler(90f,0,0),billboard:false);
        }
        public static void ShadowTrail(Vector3 from,Vector3 to,Color color)=>Beam(from+Vector3.up*.45f,to+Vector3.up*.45f,color,.65f,.28f);
        public static void SoulLink(Vector3 from,Vector3 to,Color color)=>Beam(from+Vector3.up*.8f,to+Vector3.up*.8f,color,.08f,.32f);
        public static void FutureMark(Transform target,Color color,float duration)
        {
            if(target==null)return;
            Vector3 p=target.position+Vector3.up*2.25f;
            HeroVfxPool.Emit(HeroFxShape.Runes,p,p,Vector3.one*.55f,color,duration,
                HeroFxMotion.Orbit,owner:target.gameObject,follow:target);
            Ring(target.position,color,1.4f,.55f);
        }
        public static void ProjectileLine(Vector3 from,Vector3 to,Color color,float width=.11f)
        {
            Vector3 muzzle=from+Vector3.up*.65f;
            Beam(muzzle,to+Vector3.up*.65f,color,width,.16f);
            HeroVfxPool.Sprite(HeroFxSprite.Muzzle,muzzle,muzzle,new Vector2(.25f,.25f),color,.08f,null);
            Burst(to,color,.55f,4);
        }
        public static void BioBolt(Vector3 from,Vector3 to,Color bio,Color teamAccent)
        {
            if((to-from).sqrMagnitude<.01f)return;
            Beam(from,to,bio,.15f,.16f);Beam(from,to,Color.Lerp(bio,Color.white,.38f),.055f,.13f);
            Burst(from,teamAccent,.10f,2);Burst(to,bio,.38f,4);Ring(to,bio,.34f,.18f);
        }
        public static void Shockwave(Vector3 position,Color color,float radius=2.4f)
        {
            Ring(position,color,radius,.3f);
            Vector3 p=position+Vector3.up*.18f;
            HeroVfxPool.Sprite(HeroFxSprite.ShockRing,p,p,new Vector2(radius,radius),color,.28f,null,
                HeroFxMotion.Expand,Quaternion.Euler(90f,0,0),false);
            int count=AdaptiveVfxBudget.Count(6);
            for(int i=0;i<count;i++)
            {
                Vector3 d=Quaternion.Euler(0,i*360f/count,0)*Vector3.forward;
                HeroVfxPool.Emit(HeroFxShape.Shard,p+d*.4f,p+d*(radius*.72f),new Vector3(.08f,.12f,.08f),
                    color,.28f,HeroFxMotion.Scatter,Quaternion.LookRotation(d),decorative:true);
            }
        }
        public static void GravityField(Vector3 position,Color color,float radius=2f)
        {
            Ring(position,color,radius,.48f);Ring(position+Vector3.up*.08f,Color.Lerp(color,Color.white,.4f),radius*.65f,.42f);
            int count=AdaptiveVfxBudget.Count(4);
            for(int i=0;i<count;i++)
            {
                float a=i*360f/count*Mathf.Deg2Rad;
                Vector3 p=position+new Vector3(Mathf.Cos(a)*radius*.55f,.45f,Mathf.Sin(a)*radius*.55f);
                HeroVfxPool.Emit(HeroFxShape.Orb,p,p+Vector3.up*.3f,Vector3.one*.18f,color,.45f,HeroFxMotion.Drift,decorative:true);
            }
        }
        public static void Bloom(Vector3 position,Color color,float radius=2f)
        {
            Ring(position,color,radius,.45f);
            Vector3 p=position+Vector3.up*.2f;
            HeroVfxPool.Emit(HeroFxShape.Petals,p,p,Vector3.one*(radius*.55f),color,.42f,HeroFxMotion.Expand,decorative:true);
            HeroVfxPool.Sprite(HeroFxSprite.SoftStar,p,p,new Vector2(.8f,.8f),color,.32f,null);
        }
        public static void Guard(Vector3 position,Color color,float radius=1.5f)
        {
            Vector3 p=position+Vector3.up*.75f;
            HeroVfxPool.Emit(HeroFxShape.Shield,p,p,new Vector3(radius*.72f,.65f,radius*.72f),color,.34f,HeroFxMotion.Expand);
            HeroVfxPool.Sprite(HeroFxSprite.ShieldGlyph,p,p,new Vector2(.65f,.65f),color,.24f,null);
        }
        private static void Beam(Vector3 from,Vector3 to,Color color,float width,float duration)
        {
            if((to-from).sqrMagnitude>=.01f)HeroVfxPool.Trace(from,to,color,width,duration);
        }
    }
}
