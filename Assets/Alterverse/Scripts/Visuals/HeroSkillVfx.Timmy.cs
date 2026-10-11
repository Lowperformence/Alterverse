using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class HeroSkillVfx
    {
        private static void TimmyClaws(GameObject actor,Vector3 point,int slot)
        {
            Color cyan=Accent(actor);Quaternion facing=Face(actor.transform.position,point);
            Vector3 center=point+Vector3.up*.12f;
            float size=slot<0?.40f:slot==3?.60f:.55f;
            for(int i=-1;i<=1;i++)
                Fx(HeroFxShape.Slash,center+actor.transform.right*(i*.075f),
                    i==0?Color.Lerp(cyan,Color.white,.75f):cyan,size,.14f,
                    HeroFxMotion.Sweep,facing*Quaternion.Euler(25f,0f,-25f),actor);
        }
        private static void TimmyCast(GameObject actor,int slot)
        {
            Color cyan=Accent(actor);Vector3 p=Chest(actor);
            if(slot==0)TimmyClaws(actor,p+actor.transform.forward*.5f,slot);
            else if(slot==1)
            {
                Fx(HeroFxShape.Runes,p,cyan,.28f,.18f,HeroFxMotion.Orbit,actor:actor);
                Fx(HeroFxShape.Shard,p,Color.white,.16f,.14f,HeroFxMotion.Contract,actor:actor);
            }
            else Fx(HeroFxShape.Burst,p-actor.transform.forward*.25f,new Color(1f,.58f,.18f),.26f,.16f,actor:actor);
        }
        private static void TimmyJet(GameObject actor,Vector3 from,Vector3 to)
        {
            Quaternion facing=Face(from,to);Color cyan=Accent(actor);
            HeroVfxPool.Emit(HeroFxShape.Streak,from+Vector3.up*.1f,to+Vector3.up*.1f,
                new Vector3(.16f,.16f,.65f),cyan,.4f,HeroFxMotion.Travel,facing,actor);
            Fx(HeroFxShape.Burst,from-actor.transform.forward*.15f,new Color(1f,.54f,.15f),.30f,.16f,actor:actor);
            Fx(HeroFxShape.Ring,Ground(to),new Color(cyan.r,cyan.g,cyan.b,.45f),.4f,.22f,actor:actor);
            actor.GetComponent<HeroVisualMotion>()?.PulseDash();
        }
    }
}
