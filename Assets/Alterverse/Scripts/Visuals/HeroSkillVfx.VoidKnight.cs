using UnityEngine;
namespace Alterverse.Visuals
{
    public sealed partial class HeroSkillVfx
    {
        public static void CancelVoidKnightWarnings(GameObject actor,bool relay=true)
        {
            HeroVfxPool.CancelWarnings(actor);
            if(relay)Alterverse.Multiplayer.CoopHeroStateSync.BroadcastVoidKnightEffect(actor,3,actor.transform.position,actor.transform.position,0,0);
        }
        public static void VoidKnightField(GameObject actor,Vector3 direction,float range,float seconds,bool warning,bool thrust=false,bool relay=true,float elapsed=0,Vector3? fixedOrigin=null)
        {
            if(actor==null||actor.GetComponent<Alterverse.Characters.HeroIdentity>()?.Hero!=Alterverse.Characters.HeroId.VoidKnight)return;
            Color color=StylizedArtDirection.HeroAccent(Alterverse.Characters.HeroId.VoidKnight);Vector3 origin=Ground(fixedOrigin??actor.transform.position);
            direction=Alterverse.Combat.AimGeometry.Direction(direction);
            if(relay)Alterverse.Multiplayer.CoopHeroStateSync.BroadcastVoidKnightEffect(actor,thrust?2:warning?0:1,origin,origin+direction,range,seconds);
            seconds=Mathf.Max(0,seconds-elapsed);if(seconds<=0)return;
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
