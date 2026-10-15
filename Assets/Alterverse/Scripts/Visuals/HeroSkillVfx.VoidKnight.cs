using UnityEngine;
namespace Alterverse.Visuals
{
    public sealed partial class HeroSkillVfx
    {
        public static void VoidKnightField(GameObject actor,Vector3 direction,float range,float seconds,bool warning,bool thrust=false)
        {
            Color color=StylizedArtDirection.HeroAccent(Alterverse.Characters.HeroId.VoidKnight);Vector3 origin=Ground(actor.transform.position);
            if(thrust)
            {
                Vector3 end=origin+direction*range;HeroVfxPool.Emit(HeroFxShape.Streak,(origin+end)*.5f,end,new Vector3(.18f,.06f,range),color,seconds,HeroFxMotion.Hold,Face(origin,end),actor,warning);return;
            }
            for(int i=0;i<=8;i++)
            {
                Vector3 ray=Quaternion.AngleAxis(-40f+i*10f,Vector3.up)*direction;Vector3 endpoint=origin+ray*range;
                Fx(warning?HeroFxShape.Shard:HeroFxShape.Burst,endpoint,color,warning?.16f:.25f,seconds,warning?HeroFxMotion.Hold:HeroFxMotion.Contract,actor:actor,warning:warning);
                if(i==0||i==8||!warning)HeroVfxPool.Emit(HeroFxShape.Streak,warning?(origin+endpoint)*.5f:origin,endpoint,new Vector3(.08f,.08f,warning?range:.25f),color,seconds,warning?HeroFxMotion.Hold:HeroFxMotion.Travel,Face(origin,endpoint),actor,warning);
            }
        }
    }
}
